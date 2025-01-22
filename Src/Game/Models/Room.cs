using Core.Client.Interfaces;
using Core.Protocol.Messages;
using Core.Protocol.Packets;
using Game.Services;

namespace Game.Models;

public class Room
{
  private readonly IClientManager _clientManager;
  private readonly IGameStatsService _gameStatsService;
  private readonly SemaphoreSlim _roomLock = new SemaphoreSlim(1, 1);

  public int Id { get; set; }
  public int OwnerId { get; set; }
  public string Name { get; set; }
  public int MaxUserNum { get; set; }
  public RoomStateType State { get; set; }
  public Dictionary<int, User> Users { get; } = new();
  private readonly CardDeck _cardDeck;
  private readonly SpawnPointPool _spawnPointPool = new();
  private Timer? _phaseTimer;
  private PhaseType _currentPhase;
  private bool _isGameEnd = false;

  public Room(RoomData roomData, IClientManager clientManager, IGameStatsService gameStatsService)
  {
    _clientManager = clientManager;
    _gameStatsService = gameStatsService;

    Id = roomData.Id;
    OwnerId = roomData.OwnerId;
    Name = roomData.Name;
    MaxUserNum = roomData.MaxUserNum;
    State = roomData.State;
    _cardDeck = new CardDeck();
    _currentPhase = PhaseType.Day;
  }

  public async Task<bool> EnterRoomAsync(Func<Task<bool>> action)
  {
    try
    {
      await _roomLock.WaitAsync();
      return await action();
    }
    finally
    {
      _roomLock.Release();
    }
  }

  /// <summary>
  /// 유저 추가
  /// </summary>
  public void AddUser(User user)
  {
    // 초기 위치 할당
    var spawnPoint = _spawnPointPool.GetRandomSpawnPoint();
    if (spawnPoint.HasValue)
    {
      user.X = spawnPoint.Value.X;
      user.Y = spawnPoint.Value.Y;
    }
    Users[user.Id] = user;
  }

  /// <summary>
  /// 유저 조회
  /// </summary>
  public User? GetUser(int userId)
  {
    return Users.GetValueOrDefault(userId);
  }
  
  /// <summary>
  /// 방에 있는 모든 유저 조회
  /// </summary>
  public List<User> GetUsers()
  {
    return Users.Values.ToList();
  }

  public async Task RemoveUser(int userId)
  {
    try
    {
      await _roomLock.WaitAsync();

      if(Users.Remove(userId))
      {
        if (Users.Count == 1 && !_isGameEnd)
        {
          var lastUser = Users.Values.First();

          // 승리 타입
          var winType = lastUser.Character.RoleType switch
          {
            RoleType.Target or RoleType.Bodyguard => WinType.TargetAndBodyguardWin,
            RoleType.Hitman => WinType.HitmanWin,
            RoleType.Psychopath => WinType.PsychopathWin,
            _ => WinType.TargetAndBodyguardWin // 기본값
          };

          // 승리 카운트 증가
          await _gameStatsService.IncrementWinCountAsync(lastUser.Id, lastUser.Character.CharacterType);

          // 게임 종료
          var notification = new S2CGameEndNotification
          {
            WinType = winType
          };
          notification.Winners.Add(lastUser.Id);

          var gamePacket = new GamePacket();
          gamePacket.GameEndNotification = notification;

          var session = _clientManager.GetSessionByUserId(lastUser.Id);
          if (session != null)
          {
            await session.MessageQueue.EnqueueSend(PacketId.GameEndNotification, SequenceGenerator.GetNextSequence(), gamePacket);
          }
        }
      }
    }
    finally
    {
      _roomLock.Release();
    }
  }

  public async Task<(bool isGameEnd, WinType? winType, List<int> winners)> CheckGameEnd()
  {
    try
    {
      await _roomLock.WaitAsync();

      var winners = new List<int>();

      // 히트맨 승리 조건
      var target = Users.Values.FirstOrDefault(u => u.Character.RoleType == RoleType.Target);
      if (target != null && target.Character.Hp <= 0)
      {
        var hitman = Users.Values.FirstOrDefault(u => u.Character.RoleType == RoleType.Hitman);
        if (hitman != null)
        {
          winners.Add(hitman.Id);
          _isGameEnd = true;
          await _gameStatsService.IncrementWinCountAsync(hitman.Id, hitman.Character.CharacterType);
          return (true, WinType.HitmanWin, winners);
        }
      }

      // 사이코패스 승리 조건
      var psychopath = Users.Values.FirstOrDefault(u => u.Character.RoleType == RoleType.Psychopath);
      if (psychopath != null)
      {
        var otherUsers = Users.Values.Where(u => u.Id != psychopath.Id);
        if (otherUsers.All(u => u.Character.Hp <= 0))
        {
          winners.Add(psychopath.Id);
          _isGameEnd = true;
          await _gameStatsService.IncrementWinCountAsync(psychopath.Id, psychopath.Character.CharacterType);
          return (true, WinType.PsychopathWin, winners);
        }
      }

      // 타겟과 보디가드 승리 조건
      var hitmanAndPsychopath = Users.Values.Where(u => u.Character.RoleType == RoleType.Hitman || u.Character.RoleType == RoleType.Psychopath);
      if (hitmanAndPsychopath.All(u => u.Character.Hp <= 0))
      {
        var targetAndBodyguard = Users.Values.Where(u => u.Character.RoleType == RoleType.Target || u.Character.RoleType == RoleType.Bodyguard);
        winners.AddRange(targetAndBodyguard.Select(u => u.Id));
        _isGameEnd = true;
        foreach (var winner in targetAndBodyguard)
        {
          await _gameStatsService.IncrementWinCountAsync(winner.Id, winner.Character.CharacterType);
        }
        return (true, WinType.TargetAndBodyguardWin, winners);
      }
      return (false, null, winners);
    }
    finally
    {
      _roomLock.Release();
    }
  }

  /// <summary>
  /// 초기 카드 분배
  /// </summary>
  public void DealInitialCards(User user)
  {
    int cardCount = user.Character.Hp;
    var cards = _cardDeck.DrawCards(cardCount);
    user.Character.HandCards.AddRange(cards);
    user.Character.HandCardsCount = cards.Count;
  }

  /// <summary>
  /// 카드 사용
  /// </summary>
  public bool UseCard(User user, User? targetUser, CardType cardType)
  {
    // 해당 카드 찾기
    var cardIndex = user.Character.HandCards.FindIndex(c => c.Type == cardType);
    if (cardIndex == -1)
      return false;

    // 카드 사용 처리
    var usedCard = user.Character.HandCards[cardIndex];
    user.Character.HandCards.RemoveAt(cardIndex);
    user.Character.HandCardsCount--;

    // 사용한 카드 덱 맨 아래에 추가
    _cardDeck.AddUsedCard(usedCard);
    return true;
  }

  /// <summary>
  /// 방에 있는 모든 유저 위치 정보 반환
  /// </summary>
  public List<CharacterPositionData> GetAllUserPositions()
  {
    return Users.Values.Select(u => u.ToPositionData()).ToList();
  }

  /// <summary>
  /// 유저 위치 업데이트
  /// </summary>
  public void UpdateUserPosition(int userId, double x, double y)
  {
    if (Users.TryGetValue(userId, out var user))
    {
      user.X = x;
      user.Y = y;
    }
  }

  /// <summary>
  /// 페이즈 타이머 시작
  /// </summary>
  public void StartPhaseTimer()
  {
    UpdatePhase();
  }

  /// <summary>
  /// 페이즈 업데이트 및 다음 페이즈 예약
  /// </summary>
  private async void UpdatePhase()
  {
    try
    {
      await _roomLock.WaitAsync();

      var nextPhaseTime = _currentPhase == PhaseType.Day ? TimeSpan.FromMinutes(3) : TimeSpan.FromMinutes(1);
      var nextPhaseAt = DateTimeOffset.UtcNow.Add(nextPhaseTime);
      
      // 밤이 될 경우, 캐릭터 위치 재배치
      if (_currentPhase == PhaseType.Day)
      {
        _spawnPointPool.Reset();
        foreach (var user in Users.Values)
        {
          var spawnPoint = _spawnPointPool.GetRandomSpawnPoint();
          if (spawnPoint.HasValue)
          {
            user.X = spawnPoint.Value.X;
            user.Y = spawnPoint.Value.Y;
          }
        }
      }

      // 페이즈 업데이트 알림 생성
      var notification = new S2CPhaseUpdateNotification
      {
        PhaseType = _currentPhase,
        NextPhaseAt = nextPhaseAt.ToUnixTimeMilliseconds()
      };
      notification.CharacterPositions.AddRange(GetAllUserPositions());

      var gamePacket = new GamePacket();
      gamePacket.PhaseUpdateNotification = notification;

      // 브로드캐스트 메시지 생성
      var userIds = Users.Values.Select(u => u.Id).ToList();
      var broadcastMessage = HandlerResponse.CreateBroadcast(
        PacketId.PhaseUpdateNotification,
        gamePacket,
        userIds
      );

      // 브로드캐스트 메시지를 각 클라이언트의 메시지 큐에 추가
      foreach (var userId in userIds)
      {
        var session = _clientManager.GetSessionByUserId(userId);
        if (session != null)
        {
          await session.MessageQueue.EnqueueSend(
            broadcastMessage.PacketId,
            broadcastMessage.Sequence,
            broadcastMessage.Message
          );
        }
      }

      // 다음 페이즈 타이머 설정
      _phaseTimer?.Dispose();
      _phaseTimer = new Timer(_ =>
      {
        _currentPhase = _currentPhase switch
        {
          PhaseType.Day => PhaseType.Evening,
          PhaseType.Evening => PhaseType.Day,
          _ => PhaseType.Day
        };
        UpdatePhase();
      }, null, nextPhaseTime, Timeout.InfiniteTimeSpan);
    }
    finally
    {
      _roomLock.Release();
    }
  }

  /// <summary>
  /// 방 종료 시 정리
  /// </summary>
  public void Dispose()
  {
    _phaseTimer?.Dispose();
    _roomLock.Dispose();
  }
}