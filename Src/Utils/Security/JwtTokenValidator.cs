using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Utils.Security;

public class JwtTokenValidator
{
  private readonly IConfiguration _configuration;
  private readonly ILogger<JwtTokenValidator> _logger;

  public JwtTokenValidator(IConfiguration configuration, ILogger<JwtTokenValidator> logger)
  {
    _configuration = configuration;
    _logger = logger;
  }

  public bool ValidateToken(string token, out int userId, out int roomId)
  {
    userId = 0;
    roomId = 0;

    try
    {
      var secretKey = _configuration["JwtSettings:SecretKey"];
      if (string.IsNullOrEmpty(secretKey))
      {
        _logger.LogError("JWT SecretKey가 설정되지 않았습니다.");
        return false;
      }

      var tokenHandler = new JwtSecurityTokenHandler();
      var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));

      var validationParameters = new TokenValidationParameters
      {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = key,
        ValidateIssuer = true,
        ValidIssuer = _configuration["JwtSettings:Issuer"] ?? "LobbyServer",
        ValidateAudience = true,
        ValidAudience = _configuration["JwtSettings:Audience"] ?? "GameServer",
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
      };

      var principal = tokenHandler.ValidateToken(token, validationParameters, out _);

      userId = int.Parse(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");
      roomId = int.Parse(principal.FindFirst("RoomId")?.Value ?? "0");

      return true;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "토큰 검증 실패");
        return false;
    }
  }
}