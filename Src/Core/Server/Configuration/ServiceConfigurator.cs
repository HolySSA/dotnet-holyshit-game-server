using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Core.Client.Interfaces;
using Core.Client;
using Microsoft.Extensions.Logging;
using Core.Protocol;
using Core.Protocol.Handlers;
using Utils.Security;
using Game.Managers;
using Core.Server.Services;
using Game.Services;

namespace Core.Server.Configuration;

public static class ServiceConfigurator
{
  /// <summary>
  /// 설정 서비스 등록
  /// </summary>
  public static IServiceCollection AddServerConfiguration(this IServiceCollection services, IConfiguration configuration)
  {
    services.AddSingleton<IConfiguration>(configuration);
    services.AddSingleton<DataManager>();

    // Redis 설정 및 서비스 등록
    services.Configure<RedisConfig>(configuration.GetSection("Redis"));
    services.AddSingleton<IRedisService, RedisService>();

    // 로깅 설정
    services.AddLogging(builder =>
    {
      builder.AddConsole();
      builder.AddDebug();
    });

    return services;
  }

  /// <summary>
  /// 네트워크 서비스 등록
  /// </summary>
  public static IServiceCollection AddNetworkServices(this IServiceCollection services)
  {
    // 순서 중요!
    services.AddSingleton<IClientManager, ClientManager>();
    services.AddSingleton<IHandlerManager, HandlerManager>();
    services.AddSingleton<IPacketManager, PacketManager>(); // 패킷 매니저 (핸들러 매니저에 의존)
    services.AddSingleton<PacketSerializer>();
    services.AddTransient<MessageQueue>(); // ClientSession마다 새로운 인스턴스가 필요하므로 Transient로 등록
    services.AddSingleton<ServerManager>(); // 서버 매니저 (모든 매니저에 의존)
    return services;
  }

  /// <summary>
  /// 게임 서비스 등록
  /// </summary>
  public static IServiceCollection AddGameServices(this IServiceCollection services)
  {
    services.AddSingleton<IRoomManager, RoomManager>();
    services.AddSingleton<IUserManager, UserManager>();
    services.AddSingleton<IGameStatsService, GameStatsService>();
    services.AddScoped<GamePacketHandler>();
    services.AddScoped<JwtTokenValidator>(); 
    return services;
  }
}