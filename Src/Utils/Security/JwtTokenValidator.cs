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

  public bool ValidateToken(string token)
  {
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

      tokenHandler.ValidateToken(token, validationParameters, out _);
      return true;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "토큰 검증 실패");
      return false;
    }
  }
}