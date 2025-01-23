using StackExchange.Redis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Core.Server.Configuration;

namespace Core.Server.Services;

public interface IRedisService
{
  IDatabase GetDatabase();
}

public class RedisService : IRedisService, IDisposable
{
  private readonly ConnectionMultiplexer _redis;
  private readonly ILogger<RedisService> _logger;
  private const int DEFAULT_DB = 0;

  public RedisService(IConfiguration configuration, ILogger<RedisService> logger)
  {
    _logger = logger;
    try
    {
      var redisConfig = configuration.GetSection("Redis").Get<RedisConfig>();
      var options = new ConfigurationOptions
      {
        EndPoints = { $"{redisConfig?.Host}:{redisConfig?.Port}" },
        Password = redisConfig?.Password,
        AbortOnConnectFail = false,
        ConnectTimeout = 5000,
        SyncTimeout = 5000
      };

      _redis = ConnectionMultiplexer.Connect(options);
      _logger.LogInformation("Redis 연결 성공");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Redis 연결 실패");
      throw;
    }
  }

  public IDatabase GetDatabase()
  {
    return _redis.GetDatabase(DEFAULT_DB);
  }

  public void Dispose()
  {
    _redis?.Dispose();
  }
}