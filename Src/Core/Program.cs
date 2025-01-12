using Core.Server;
using Core.Server.Configuration;
using Microsoft.Extensions.DependencyInjection;

class Program
{
  private static ServerManager? _serverManager;

  static async Task Main(string[] args)
  {
    AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

    try
    {
      var serviceProvider = ServerConfiguration.ConfigureServices(args); // 서버 설정
      await ServerConfiguration.InitializeServicesAsync(serviceProvider); // 서버 초기화

      _serverManager = serviceProvider.GetRequiredService<ServerManager>(); // 서버 매니저
      await _serverManager.StartAsync(); // 서버 시작
    }
    catch (Exception ex)
    {
      Console.WriteLine($"서버 실행 중 오류 발생: {ex.Message}");
    }
  }

  /// <summary>
  /// 서버 종료 시 실행되는 이벤트 핸들러
  /// </summary>
  private static void OnProcessExit(object? sender, EventArgs e)
  {
    _serverManager?.StopAsync().Wait();
  }
}
