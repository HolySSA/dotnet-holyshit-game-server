using Core.Protocol.Packets;
using Core.Client;
using Microsoft.Extensions.Logging;
using Core.Protocol.Messages;
using Utils.Security;
using Game.Managers;

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
    // 토큰 검증
    if (!_tokenValidator.ValidateToken(request.Token))
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

    // 방 생성 or 조회
    var room = _roomManager.GetRoom(request.RoomData.Id);
    if (room == null)
    {
      room = _roomManager.CreateRoom(request.RoomData);
      if (room == null)
      {
        _logger.LogError("방 생성 실패: RoomId = {RoomId}", request.RoomData.Id);
        return HandlerResponse.CreateResponse(PacketId.GameServerInitResponse, sequence, new S2CGameServerInitResponse
        {
          Success = false,
          FailCode = GlobalFailCode.RoomNotFound
        });
      }
    }

    // RoomData에 존재하는 유저인지 확인
    var userData = request.RoomData.Users.FirstOrDefault(u => u.Id == request.UserId);
    if (userData == null)
    {
      _logger.LogError("유저 데이터 없음: UserId = {UserId}", request.UserId);
      return HandlerResponse.CreateResponse(PacketId.GameServerInitResponse, sequence, new S2CGameServerInitResponse
      {
        Success = false,
        FailCode = GlobalFailCode.CharacterNotFound
      });
    }

    // 유저 생성
    var user = _userManager.CreateUser(userData);
    if (user == null)
    {
      _logger.LogError("유저 생성 실패: UserId = {UserId}", request.UserId);
      return HandlerResponse.CreateResponse(PacketId.GameServerInitResponse, sequence, new S2CGameServerInitResponse
      {
        Success = false,
        FailCode = GlobalFailCode.CharacterNotFound
      });
    }

    // 방에 유저 추가
    room.AddUser(user);
    _logger.LogInformation("유저 {UserId}가 방 {RoomId}에 입장", request.UserId, request.RoomData.Id);

    // 로비 데이터의 유저들과 실제 접속한 유저들 비교
    var lobbyUsers = request.RoomData.Users;
    var connectedUsers = room.GetUsers();

    // 모든 로비 유저가 게임 서버에 접속했는지 확인
    if (lobbyUsers.Count == connectedUsers.Count && lobbyUsers.All(lu => connectedUsers.Any(cu => cu.Id == lu.Id)))
    {
      var response = new S2CGameServerInitResponse()
      {
        Success = true,
        FailCode = GlobalFailCode.NoneFailcode
      };

      var responseGamePacket = new GamePacket();
      responseGamePacket.GameServerInitResponse = response;

      var initResponse = HandlerResponse.CreateResponse(PacketId.GameServerInitResponse, sequence, responseGamePacket);

      // 게임 상태 정보 생성
      var gameState = new GameStateData
      {
        PhaseType = PhaseType.Day, // 낮
        NextPhaseAt = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeMilliseconds() // 5분 후 다음 페이즈
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
    else
    {
      var response = new S2CGameServerInitResponse()
      {
        Success = true,
        FailCode = GlobalFailCode.NoneFailcode
      };

      var gamePacket = new GamePacket();
      gamePacket.GameServerInitResponse = response;

      return HandlerResponse.CreateResponse(PacketId.GameServerInitResponse, sequence, gamePacket);
    }
  }

  public async Task<HandlerResponse> HandlePositionUpdateRequest(ClientSession client, uint sequence, C2SPositionUpdateRequest request)
  {
    var response = new S2CPositionUpdateResponse
    {
      Success = true,
      FailCode = GlobalFailCode.NoneFailcode
    };

    return HandlerResponse.CreateResponse(PacketId.PositionUpdateResponse, sequence, response);
  }
}