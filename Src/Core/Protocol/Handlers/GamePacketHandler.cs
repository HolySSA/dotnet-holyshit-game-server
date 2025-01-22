using Core.Protocol.Packets;
using Core.Client;
using Microsoft.Extensions.Logging;
using Core.Protocol.Messages;
using Utils.Security;
using Game.Managers;
using Game.Models;

namespace Core.Protocol.Handlers;

public class GamePacketHandler
{
  private readonly JwtTokenValidator _tokenValidator;
  private readonly ILogger<GamePacketHandler> _logger;
  private readonly IRoomManager _roomManager;
  private readonly IUserManager _userManager;

  public GamePacketHandler(JwtTokenValidator tokenValidator, ILogger<GamePacketHandler> logger, IRoomManager roomManager, IUserManager userManager)
  {
    _tokenValidator = tokenValidator;
    _logger = logger;
    _roomManager = roomManager;
    _userManager = userManager;
  }

  public async Task<HandlerResponse> HandleGameServerInitRequest(ClientSession client, uint sequence, C2SGameServerInitRequest request)
  {
    try
    {
      // 토큰 검증
      if (!await _tokenValidator.ValidateToken(request.Token))
      {
        return HandlerResponse.CreateResponse(
          PacketId.GameServerInitResponse,
          sequence,
          new S2CGameServerInitResponse
          {
            Success = false,
            FailCode = GlobalFailCode.AuthenticationFailed
          });
      }

      // 세션에 userId와 roomId 저장
      client.SetUserId(request.UserId);
      client.SetRoomId(request.RoomData.Id);

      // 방 생성(이미 존재 시 조회)
      var room = _roomManager.CreateRoom(request.RoomData);

      if (room == null)
      {
        _logger.LogError("방 생성 실패: RoomId = {RoomId}", request.RoomData.Id);
        return HandlerResponse.CreateResponse(PacketId.GameServerInitResponse, sequence, new S2CGameServerInitResponse
        {
          Success = false,
          FailCode = GlobalFailCode.RoomNotFound
        });
      }

      // 동시성 제어를 위한 락 획득
      await room.EnterRoomAsync(async () =>
      {
        // RoomData에 존재하는 유저인지 확인
        var userData = request.RoomData.Users.FirstOrDefault(u => u.Id == request.UserId);
        if (userData == null)
        {
          _logger.LogError("유저 데이터 없음: UserId = {UserId}", request.UserId);
          return false;
        }

        // 유저 생성
        var user = _userManager.CreateUser(userData);
        if (user == null)
        {
          _logger.LogError("유저 생성 실패: UserId = {UserId}", request.UserId);
          return false;
        }

        room.AddUser(user); // 방에 유저 추가
        room.DealInitialCards(user); // 초기 카드 분배
        return true;
      });

      var response = new S2CGameServerInitResponse
      {
        Success = true,
        FailCode = GlobalFailCode.NoneFailcode
      };

      var responseGamePacket = new GamePacket();
      responseGamePacket.GameServerInitResponse = response;

      var initResponse = HandlerResponse.CreateResponse(PacketId.GameServerInitResponse, sequence, responseGamePacket);

      // 로비 데이터의 유저들과 실제 접속한 유저들 비교
      var lobbyUsers = request.RoomData.Users;
      var connectedUsers = room.GetUsers();

      // 모든 로비 유저가 게임 서버에 접속했는지 확인
      if (lobbyUsers.Count == connectedUsers.Count)
      {
        // 페이즈 타이머 시작
        room.StartPhaseTimer();
        // 게임 상태 정보 생성
        var gameState = new GameStateData
        {
          PhaseType = PhaseType.Day, // 낮
          NextPhaseAt = DateTimeOffset.UtcNow.AddMinutes(3).ToUnixTimeMilliseconds() // 3분 후 다음 페이즈
        };

        // 모든 유저 초기 위치
        var characterPositions = room.GetAllUserPositions();
        // 게임 시작 알림 패킷 생성
        var notification = new S2CGameServerInitNotification
        {
          GameState = gameState
        };
        notification.Users.AddRange(connectedUsers.Select(u => new UserData
        {
          Id = u.Id,
          Nickname = u.Nickname,
          Character = u.Character.ToCharacterData()
        }));
        notification.CharacterPositions.AddRange(characterPositions);

        var notificationGamePacket = new GamePacket();
        notificationGamePacket.GameServerInitNotification = notification;

        // 방의 모든 유저 ID 가져오기
        var userIds = connectedUsers.Select(u => u.Id).ToList();
        // 브로드캐스트 응답 생성
        var broadcastResponse = HandlerResponse.CreateBroadcast(
          PacketId.GameServerInitNotification,
          notificationGamePacket,
          userIds
        );

        return initResponse.SetNextResponse(broadcastResponse);
      }

      return initResponse;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "게임 서버 초기화 실패: UserId = {UserId}", request.UserId);
      return HandlerResponse.CreateResponse(PacketId.GameServerInitResponse, sequence, new S2CGameServerInitResponse
      {
        Success = false,
        FailCode = GlobalFailCode.CharacterNotFound
      });
    }
  }

  public async Task<HandlerResponse> HandlePositionUpdateRequest(ClientSession client, uint sequence, C2SPositionUpdateRequest request)
  {
    var room = _roomManager.GetRoom(client.RoomId);
    if (room == null)
    {
      _logger.LogError("방을 찾을 수 없음: RoomId = {RoomId}", client.RoomId);
      return HandlerResponse.CreateResponse(PacketId.PositionUpdateNotification, sequence, new S2CPositionUpdateNotification());
    }

    // 위치 업데이트
    room.UpdateUserPosition(client.UserId, request.X, request.Y);

    // 위치 업데이트 알림 패킷 생성
    var notification = new S2CPositionUpdateNotification();
    notification.CharacterPositions.AddRange(room.GetAllUserPositions());

    var notificationGamePacket = new GamePacket();
    notificationGamePacket.PositionUpdateNotification = notification;

    var userIds = room.GetUsers().Select(u => u.Id).ToList();

    // 브로드캐스트 전송
    return HandlerResponse.CreateBroadcast(
      PacketId.PositionUpdateNotification,
      notificationGamePacket,
      userIds
    );
  }

  public async Task<HandlerResponse> HandleUseCardRequest(ClientSession client, uint sequence, C2SUseCardRequest request)
  {
    var room = _roomManager.GetRoom(client.RoomId);
    if (room == null)
    {
      _logger.LogError("방을 찾을 수 없음: RoomId = {RoomId}", client.RoomId);
      return HandlerResponse.CreateResponse(PacketId.UseCardResponse, sequence, new S2CUseCardResponse
      {
        Success = false,
        FailCode = GlobalFailCode.RoomNotFound
      });
    }

    // 카드를 사용하려는 유저 조회
    var user = room.GetUser(client.UserId);
    if (user == null)
    {
      _logger.LogError("유저를 찾을 수 없음: UserId = {UserId}", client.UserId);
      return HandlerResponse.CreateResponse(PacketId.UseCardResponse, sequence, new S2CUseCardResponse
      {
        Success = false,
        FailCode = GlobalFailCode.CharacterNotFound
      });
    }

    // 타겟 유저 확인 (타겟이 필요한 카드의 경우)
    User? targetUser = null;
    if (request.TargetUserId != 0)
    {
      targetUser = room.GetUser(request.TargetUserId);
      if (targetUser == null)
      {
        _logger.LogError("타겟 유저를 찾을 수 없음: TargetUserId = {TargetUserId}", request.TargetUserId);
        return HandlerResponse.CreateResponse(PacketId.UseCardResponse, sequence, new S2CUseCardResponse
        {
          Success = false,
          FailCode = GlobalFailCode.CharacterNotFound
        });
      }
    }

    // 카드 사용 처리
    if (!room.UseCard(user, targetUser, request.CardType))
    {
      _logger.LogError("카드 사용 실패: UserId = {UserId}, CardType = {CardType}", client.UserId, request.CardType);
      return HandlerResponse.CreateResponse(PacketId.UseCardResponse, sequence, new S2CUseCardResponse
      {
        Success = false,
        FailCode = GlobalFailCode.InvalidRequest
      });
    }

    // 카드 사용 응답 패킷 생성
    var response = new S2CUseCardResponse
    {
      Success = true,
      FailCode = GlobalFailCode.NoneFailcode
    };

    var responseGamePacket = new GamePacket();
    responseGamePacket.UseCardResponse = response;

    var initResponse = HandlerResponse.CreateResponse(PacketId.UseCardResponse, sequence, responseGamePacket);

    // 카드 사용 알림 패킷 생성
    var notification = new S2CUseCardNotification
    {
      UserId = client.UserId,
      CardType = request.CardType,
      TargetUserId = request.TargetUserId
    };

    var notificationGamePacket = new GamePacket();
    notificationGamePacket.UseCardNotification = notification;

    // 방에 있는 모든 유저ID
    var userIds = room.GetUsers().Select(u => u.Id).ToList();

    // 브로드캐스트 전송
    var broadcastResponse = HandlerResponse.CreateBroadcast(PacketId.UseCardNotification, notificationGamePacket, userIds);
    var useCardResponses = initResponse.SetNextResponse(broadcastResponse);

    // 카드 장착 알림 패킷 생성
    var equipCardNotification = new S2CEquipCardNotification
    {
      CardType = request.CardType,
      UserId = client.UserId
    };

    var equipCardGamePacket = new GamePacket();
    equipCardGamePacket.EquipCardNotification = equipCardNotification;

    var equipBroadcastResponse = HandlerResponse.CreateBroadcast(PacketId.EquipCardNotification, equipCardGamePacket, userIds);
    var equipResponse = useCardResponses.SetNextResponse(equipBroadcastResponse);

    // 유저 상태 업데이트
    user.Character.State = CharacterStateType.BbangShooter;
    user.Character.StateTargetUserId = targetUser?.Id ?? 0;
    if (targetUser != null)
    {
      targetUser.Character.State = CharacterStateType.BbangTarget;
      targetUser.Character.StateTargetUserId = user.Id;
    }

    // 전체 유저 상태 업데이트
    var userUpdateNotification = new S2CUserUpdateNotification();
    userUpdateNotification.User.AddRange(room.GetUsers().Select(u => new UserData
    {
      Id = u.Id,
      Nickname = u.Nickname,
      Character = u.Character.ToCharacterData()
    }));

    var userUpdateGamePacket = new GamePacket();
    userUpdateGamePacket.UserUpdateNotification = userUpdateNotification;

    var userUpdateBroadcast = HandlerResponse.CreateBroadcast(PacketId.UserUpdateNotification, userUpdateGamePacket, userIds);
    return equipResponse.SetNextResponse(userUpdateBroadcast);
  }

  public async Task<HandlerResponse> HandleReactionRequest(ClientSession client, uint sequence, C2SReactionRequest request)
  {
    var room = _roomManager.GetRoom(client.RoomId);
    if (room == null)
    {
      _logger.LogError("방을 찾을 수 없음: RoomId = {RoomId}", client.RoomId);
      return HandlerResponse.CreateResponse(PacketId.ReactionResponse, sequence, new S2CReactionResponse
      {
        Success = false,
        FailCode = GlobalFailCode.RoomNotFound
      });
    }

    var targetUser = room.GetUser(client.UserId);
    if (targetUser == null)
    {
      _logger.LogError("유저를 찾을 수 없음: UserId = {UserId}", client.UserId);
      return HandlerResponse.CreateResponse(PacketId.ReactionResponse, sequence, new S2CReactionResponse
      {
        Success = false,
        FailCode = GlobalFailCode.CharacterNotFound
      });
    }

    // 응답 패킷 생성
    var reactionResponse = new S2CReactionResponse
    {
      Success = true,
      FailCode = GlobalFailCode.NoneFailcode
    };

    var reactionGamePacket = new GamePacket();
    reactionGamePacket.ReactionResponse = reactionResponse;
    var initResponse = HandlerResponse.CreateResponse(PacketId.ReactionResponse, sequence, reactionGamePacket);

    // 빵야 시전자
    var shooterUserId = targetUser.Character.StateTargetUserId;

    // 대상자 HP 감소 및 상태 초기화
    targetUser.Character.Hp -= 1;
    targetUser.Character.State = CharacterStateType.NoneCharacterState;
    targetUser.Character.StateTargetUserId = 0;

    // 시전자 상태 업데이트
    var shooter = room.GetUser(shooterUserId);
    if (shooter != null)
    {
      shooter.Character.State = CharacterStateType.NoneCharacterState;
      shooter.Character.StateTargetUserId = 0;
    }

    // 전체 유저 상태 업데이트 알림
    var userUpdateNotification = new S2CUserUpdateNotification();
    userUpdateNotification.User.AddRange(room.GetUsers().Select(u => new UserData
    {
      Id = u.Id,
      Nickname = u.Nickname,
      Character = u.Character.ToCharacterData()
    }));

    var userUpdateGamePacket = new GamePacket();
    userUpdateGamePacket.UserUpdateNotification = userUpdateNotification;

    var userIds = room.GetUsers().Select(u => u.Id).ToList();
    var userUpdateBroadcast = HandlerResponse.CreateBroadcast(PacketId.UserUpdateNotification, userUpdateGamePacket, userIds);

    // 게임 종료 조건 체크
    var (isGameEnd, winType, winners) = await room.CheckGameEnd();
    if (isGameEnd && winType.HasValue)
    {
      var gameEndNotification = new S2CGameEndNotification
      {
        WinType = winType.Value,
      };
      gameEndNotification.Winners.AddRange(winners);

      var gameEndPacket = new GamePacket();
      gameEndPacket.GameEndNotification = gameEndNotification;

      var gameEndBroadcast = HandlerResponse.CreateBroadcast(PacketId.GameEndNotification, gameEndPacket, userIds);
      return userUpdateBroadcast.SetNextResponse(userUpdateBroadcast).SetNextResponse(gameEndBroadcast);
    }

    return initResponse.SetNextResponse(userUpdateBroadcast);
  }

  public async Task<HandlerResponse> HandleDestroyCardRequest(ClientSession client, uint sequence, C2SDestroyCardRequest request)
  {
    var room = _roomManager.GetRoom(client.RoomId);
    if (room == null)
    {
      _logger.LogError("방을 찾을 수 없음: RoomId = {RoomId}", client.RoomId);
      return HandlerResponse.CreateResponse(PacketId.DestroyCardResponse, sequence, new S2CDestroyCardResponse());
    }

    var user = room.GetUser(client.UserId);
    if (user == null)
    {
      _logger.LogError("유저를 찾을 수 없음: UserId = {UserId}", client.UserId);
      return HandlerResponse.CreateResponse(PacketId.DestroyCardResponse, sequence, new S2CDestroyCardResponse());
    }

    // 버릴 카드들이 존재하는지 확인 후 제거
    foreach (var cardToDestroy in request.DestroyCards)
    {
      var cardIndex = user.Character.HandCards.FindIndex(c => c.Type == cardToDestroy.Type);
      if (cardIndex == -1)
      {
        _logger.LogError("버릴 카드를 찾을 수 없음: UserId = {UserId}, CardType = {CardType}", client.UserId, cardToDestroy.Type);
        return HandlerResponse.CreateResponse(PacketId.DestroyCardResponse, sequence, new S2CDestroyCardResponse());
      }

      // 카드 제거
      user.Character.HandCards.RemoveAt(cardIndex);
      user.Character.HandCardsCount--;
    }

    // 응답 생성
    var response = new S2CDestroyCardResponse();
    response.HandCards.AddRange(user.Character.HandCards);

    var responseGamePacket = new GamePacket();
    responseGamePacket.DestroyCardResponse = response;

    // 유저 상태 업데이트 알림 생성
    var userUpdateNotification = new S2CUserUpdateNotification();
    userUpdateNotification.User.AddRange(room.GetUsers().Select(u => new UserData
    {
      Id = u.Id,
      Nickname = u.Nickname,
      Character = u.Character.ToCharacterData()
    }));

    var userUpdateGamePacket = new GamePacket();
    userUpdateGamePacket.UserUpdateNotification = userUpdateNotification;

    var userIds = room.GetUsers().Select(u => u.Id).ToList();
    var userUpdateBroadcast = HandlerResponse.CreateBroadcast(
      PacketId.UserUpdateNotification,
      userUpdateGamePacket,
      userIds
    );

    return HandlerResponse.CreateResponse(PacketId.DestroyCardResponse, sequence, responseGamePacket).SetNextResponse(userUpdateBroadcast);
  }

  public async Task<HandlerResponse> HandleComeBackLobbyRequest(ClientSession client, uint sequence, C2SComeBackLobbyRequest request)
  {
    var room = _roomManager.GetRoom(client.RoomId);
    if (room != null)
    {
      // 방에서 유저 제거
      await room.RemoveUser(client.UserId);

      // 더 이상 유저 없으면 방 제거
      if (room.Users.Count == 0)
        _roomManager.RemoveRoom(client.RoomId);
    }

    // 유저 제거
    _userManager.RemoveUser(client.UserId);

    var response = new S2CComeBackLobbyResponse
    {
      UserId = client.UserId,
      ServerInfo = new ServerInfoData
      {
        Host = "127.0.0.1",
        Port = 4000,
        Token = "" // 토큰 검증 불필요
      }
    };

    var gamePacket = new GamePacket();
    gamePacket.ComeBackLobbyResponse = response;

    return HandlerResponse.CreateResponse(PacketId.ComeBackLobbyResponse, sequence, gamePacket);
  }
}