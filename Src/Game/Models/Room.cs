using Core.Protocol.Packets;

namespace Game.Models;

public class Room
{
  public int Id { get; set; }
  public int OwnerId { get; set; }
  public string Name { get; set; }
  public int MaxUserNum { get; set; }
  public RoomStateType State { get; set; }
  public Dictionary<int, User> Users { get; } = new();
  private readonly SpawnPointPool _spawnPointPool = new();
  private readonly CardDeck _cardDeck;

  public Room(RoomData roomData)
  {
    Id = roomData.Id;
    OwnerId = roomData.OwnerId;
    Name = roomData.Name;
    MaxUserNum = roomData.MaxUserNum;
    State = roomData.State;
    _cardDeck = new CardDeck();
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
}