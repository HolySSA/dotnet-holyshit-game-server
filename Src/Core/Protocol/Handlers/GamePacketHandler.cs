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
    if (request == null)
    {
        _logger.LogError("요청이 null입니다");
        return HandlerResponse.CreateResponse(PacketId.GameServerInitResponse, sequence, 
            new S2CGameServerInitResponse { Success = false, FailCode = GlobalFailCode.InvalidRequest });
    }

    // 요청 내용 로깅
    _logger.LogInformation(
        "GameServerInit 요청 수신: SessionId={SessionId}, UserId={UserId}, Token={Token}, RoomData={{ Id={RoomId}, Name={RoomName}, OwnerId={OwnerId}, MaxUserNum={MaxUserNum}, UserCount={UserCount} }}",
        client.SessionId,
        request.UserId,
        request.Token,
        request.RoomData.Id,
        request.RoomData.Name,
        request.RoomData.OwnerId,
        request.RoomData.MaxUserNum,
        request.RoomData.Users.Count);

    // 토큰 검증
    if (!_tokenValidator.ValidateToken(request.Token, out int userId, out int roomId))
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
    client.SetUserId(userId);
    client.SetRoomId(roomId);

    // 방 생성 or 조회
    var room = _roomManager.GetRoom(roomId);
    if (room == null)
    {
      room = _roomManager.CreateRoom(request.RoomData);
      if (room == null)
      {
        _logger.LogError("방 생성 실패: RoomId = {RoomId}", roomId);
        return HandlerResponse.CreateResponse(PacketId.GameServerInitResponse, sequence, new S2CGameServerInitResponse
        {
          Success = false,
          FailCode = GlobalFailCode.RoomNotFound
        });
      }
    }

    // 유저 생성
    var userData = request.RoomData.Users.FirstOrDefault(u => u.Id == userId);
    if (userData == null)
    {
      _logger.LogError("유저 데이터 없음: UserId = {UserId}", userId);
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
      _logger.LogError("유저 생성 실패: UserId = {UserId}", userId);
      return HandlerResponse.CreateResponse(PacketId.GameServerInitResponse, sequence, new S2CGameServerInitResponse
      {
        Success = false,
        FailCode = GlobalFailCode.CharacterNotFound
      });
    }

    // 방에 유저 추가
    room.AddUser(user);
    _logger.LogInformation("유저 {UserId}가 방 {RoomId}에 입장", userId, roomId);

    /*
    // 게임 상태 정보 생성
    var gameState = new GameStateData
    {
      PhaseType = PhaseType.Day, // 낮
      NextPhaseAt = DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeMilliseconds() // 5분 후 다음 페이즈
    };
    */

    // 성공 응답
    return HandlerResponse.CreateResponse(PacketId.GameServerInitResponse, sequence, new S2CGameServerInitResponse
    {
      Success = true,
      FailCode = GlobalFailCode.NoneFailcode
    });
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