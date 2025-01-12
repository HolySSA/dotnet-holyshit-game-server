using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Core.Client.Interfaces;
using Core.Client;
using Microsoft.Extensions.Logging;

namespace Core.Server.Configuration;

public static class ServiceCollectionExtensions
{
  /// <summary>
  /// 설정 서비스 등록
  /// </summary>
  public static IServiceCollection AddServerConfiguration(
    this IServiceCollection services,
    IConfiguration configuration)
  {
    services.AddSingleton<IConfiguration>(configuration);

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
    services.AddSingleton<IClientManager, ClientManager>();
    services.AddSingleton<ServerManager>();
    return services;
  }

  /// <summary>
  /// 게임 서비스 등록
  /// </summary>
  public static IServiceCollection AddGameServices(this IServiceCollection services)
  {
    return services;
  }
}