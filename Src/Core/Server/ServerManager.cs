using System.Net;
using System.Net.Sockets;
using Core.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Core.Server;

public class ServerManager
{
  private readonly TcpListener _tcpListener;
  private readonly CancellationTokenSource _serverCts;
  private readonly IServiceProvider _serviceProvider;
  private readonly ILogger _logger;
  private readonly DataManager _dataManager;

  private readonly string _host;
  private readonly int _port;
  private bool _isRunning;

  public ServerManager(
    IServiceProvider serviceProvider,
    ILogger<ServerManager> logger,
    IConfiguration configuration,
    DataManager dataManager)
  {
    _serviceProvider = serviceProvider;
    _logger = logger;
    _dataManager = dataManager;
    _serverCts = new CancellationTokenSource();

    _host = configuration["Server:HOST"] ?? "127.0.0.1";
    _port = int.Parse(configuration["Server:PORT"] ?? "5000");
    _tcpListener = new TcpListener(IPAddress.Parse(_host), _port);
  }

  /// <summary>
  /// 서버 시작
  /// </summary>
  public async Task StartAsync()
  {
    try
    {
      await _dataManager.InitializeDataAsync();
      _tcpListener.Start();
      _isRunning = true;
      _logger.LogInformation("게임 서버 시작 - {Host}:{Port}", _host, _port);

      while (_isRunning)
      {
        var client = await _tcpListener.AcceptTcpClientAsync(_serverCts.Token);
        _ = HandleClientAsync(client);
      }
    }
    catch (OperationCanceledException)
    {
      _logger.LogInformation("서버가 정상적으로 종료되었습니다.");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "서버 실행 중 오류 발생");
      throw;
    }
  }

  /// <summary>
  /// 클라이언트 세션 처리
  /// </summary>
  private async Task HandleClientAsync(TcpClient client)
  {
    try
    {
      using var session = new ClientSession(client, _serviceProvider);
      await session.StartAsync(_serverCts.Token);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "클라이언트 세션 처리 중 오류");
    }
    finally
    {
      client.Dispose();
    }
  }

  /// <summary>
  /// 서버 종료
  /// </summary>
  public async Task StopAsync()
  {
    _isRunning = false;
    _serverCts.Cancel();
    _tcpListener.Stop();

    await Task.CompletedTask;
    _logger.LogInformation("서버가 종료되었습니다.");
  }
}