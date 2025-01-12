using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Core.Server.Configuration;

public static class ServerConfiguration
{
  public static IServiceProvider ConfigureServices()
  {
    var configuration = new ConfigurationBuilder()
      .SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("appsettings.json").Build();

    var services = new ServiceCollection();

    // 로깅 설정
    services.AddLogging(builder =>
    {
      builder.AddConsole();
      builder.AddDebug();
    });

    // 네트워크 서비스
    services.AddNetworkServices();
    services.AddGameServices();

    return services.BuildServiceProvider();
  }

  public static async Task InitializeServicesAsync(IServiceProvider serviceProvider)
  {
    // 초기화 로직
  }
}