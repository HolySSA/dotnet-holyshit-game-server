using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Core.Server.Configuration;

public static class ServerConfiguration
{
  public static IServiceProvider ConfigureServices(string[] args)
  {
    // 명령줄 인자에서 설정 파일 경로 가져오기
    var settingsFile = GetSettingsFile(args);
    var configuration = new ConfigurationBuilder()
      .SetBasePath(Directory.GetCurrentDirectory())
      .AddJsonFile("appsettings.json", optional: true) // 기본 설정
      .AddJsonFile(settingsFile, optional: false) // 지정 설정
      .Build();

    var services = new ServiceCollection();

    // 확장 메서드를 통한 서비스 등록
    services
      .AddServerConfiguration(configuration)
      .AddNetworkServices()
      .AddGameServices();

    return services.BuildServiceProvider();
  }

  private static string GetSettingsFile(string[] args)
  {
    // --settings 인자 찾기
    for (int i = 0; i < args.Length - 1; i++)
    {
      if (args[i] == "--settings")
        return args[i + 1];
    }

    // 기본값 반환
    return "appsettings.json";
  }

  public static async Task InitializeServicesAsync(IServiceProvider serviceProvider)
  {
    // 초기화 로직
  }
}