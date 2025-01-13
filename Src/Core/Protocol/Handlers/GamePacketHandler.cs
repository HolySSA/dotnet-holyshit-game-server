using Core.Protocol.Packets;
using Core.Client;
using Microsoft.Extensions.Logging;
using Core.Protocol.Messages;
using Utils.Security;

namespace Core.Protocol.Handlers;

public class GamePacketHandler
{
  private readonly JwtTokenValidator _tokenValidator;
  private readonly ILogger<GamePacketHandler> _logger;

  public GamePacketHandler(JwtTokenValidator tokenValidator, ILogger<GamePacketHandler> logger)
  {
    _tokenValidator = tokenValidator;
    _logger = logger;
  }

  public async Task<HandlerResponse> HandleGameServerInitRequest(ClientSession client, uint sequence, C2SGameServerInitRequest request)
  {
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

    var response = new S2CGameServerInitResponse
    {
      Success = true,
      FailCode = GlobalFailCode.NoneFailcode
    };

    return HandlerResponse.CreateResponse(PacketId.GameServerInitResponse, sequence, response);
  }

  public static async Task<HandlerResponse> HandlePositionUpdate(ClientSession client, uint sequence, C2SPositionUpdateRequest request)
  {
    var response = new S2CPositionUpdateResponse
    {
      Success = true,
      FailCode = GlobalFailCode.NoneFailcode
    };

    return HandlerResponse.CreateResponse(PacketId.PositionUpdateResponse, sequence, response);
  }
}