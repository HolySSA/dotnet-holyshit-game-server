using System.Net.Sockets;
using Core.Client.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Core.Client;

public class ClientSession : IDisposable
{
  private readonly TcpClient _client; // 클라이언트
  private readonly NetworkStream _stream; // 스트림
  private readonly IServiceProvider _serviceProvider; // DI 컨테이너
  private readonly ILogger<ClientSession> _logger;
  private readonly IClientManager _clientManager;
  private readonly CancellationTokenSource _sessionCts;
  private bool _disposed; // 객체 해제 여부

  public string SessionId { get; }
  public int UserId { get; private set; } // 유저 ID
  public IServiceProvider ServiceProvider => _serviceProvider;

  public ClientSession(TcpClient client, IServiceProvider serviceProvider)
  {
    _client = client;
    _stream = client.GetStream();
    _serviceProvider = serviceProvider;
    _logger = serviceProvider.GetRequiredService<ILogger<ClientSession>>();
    _clientManager = serviceProvider.GetRequiredService<IClientManager>();
    _sessionCts = new CancellationTokenSource();

    SessionId = Guid.NewGuid().ToString();
    _clientManager.AddSession(this); // 클라이언트 세션 추가
  }

  /// <summary>
  /// 클라이언트 세션 시작
  /// </summary>
  public async Task StartAsync(CancellationToken cancellationToken = default)
  {
    try
    {
      _logger.LogInformation("새로운 클라이언트 연결: {SessionId}", SessionId);
      using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _sessionCts.Token);
      await ProcessMessagesAsync(linkedCts.Token);
    }
    catch (OperationCanceledException)
    {
      _logger.LogInformation("세션이 정상적으로 종료되었습니다: {SessionId}", SessionId);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "세션 처리 중 오류 발생: {SessionId}", SessionId);
    }
    finally
    {
      Dispose();
    }
  }

  /// <summary>
  /// 메시지 처리
  /// </summary>
  private async Task ProcessMessagesAsync(CancellationToken cancellationToken)
  {
    var buffer = new byte[4096];
    var messageBuffer = new List<byte>();

    try
    {
      while (!cancellationToken.IsCancellationRequested && !_disposed)
      {
        // 연결이 끊어졌는지 확인
        if (!IsConnected())
        {
          _logger.LogInformation("클라이언트 연결이 끊어졌습니다: {SessionId}", SessionId);
          break;
        }
        
        await Task.Delay(1000, cancellationToken); // CPU 사용률을 줄이기 위한 딜레이
      }
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
      _logger.LogError(ex, "메시지 처리 중 오류 발생: {SessionId}", SessionId);
    }
  }

  private bool IsConnected()
  {
    try
    {
      if (_client.Client.Poll(0, SelectMode.SelectRead))
      {
        byte[] buff = new byte[1];
        if (_client.Client.Receive(buff, SocketFlags.Peek) == 0)
        {
          // 클라이언트가 정상적으로 연결을 종료
          return false;
        }
      }
      return true;
    }
    catch
    {
      return false;
    }
  }

  /// <summary>
  /// 패킷 수신 (역직렬화)
  /// </summary>
  /// <param name="messageBuffer"></param>
  /// <returns></returns>
  private async Task ProcessPacketAsync(List<byte> messageBuffer)
  {
    // 여기에 패킷 처리 로직 구현
    // 예: 패킷 헤더 확인, 패킷 역직렬화, 게임 로직 처리 등
  }

  /// <summary>
  /// 패킷 전송
  /// </summary>
  public async Task SendAsync<T>(T packet) where T : class
  {
    if (_disposed)
      throw new ObjectDisposedException(nameof(ClientSession));

    try
    {
      // 여기에 패킷 직렬화 및 전송 로직 구현
      byte[] data = SerializePacket(packet);
      await _stream.WriteAsync(data);
      await _stream.FlushAsync();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "패킷 전송 중 오류 발생: {SessionId}", SessionId);
      throw;
    }
  }

  /// <summary>
  /// 패킷 송신 (직렬화)
  /// </summary>
  private byte[] SerializePacket<T>(T packet) where T : class
  {
    // 여기에 패킷 직렬화 로직 구현
    throw new NotImplementedException();
  }

  /// <summary>
  /// 유저 ID 설정
  /// </summary>
  public void SetUserId(int userId)
  {
    UserId = userId;
    _clientManager.RegisterUserSession(userId, this);
  }

  /// <summary>
  /// 세션 정리
  /// </summary>
  public void Dispose()
  {
    if (_disposed) return;

    try
    {
      _sessionCts.Cancel();
      _clientManager.RemoveSession(this);

      _stream?.Dispose();
      _client?.Dispose();
      _sessionCts.Dispose();

      _disposed = true;
      _logger.LogInformation("세션 종료: {SessionId}", SessionId);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "세션 정리 중 오류 발생: {SessionId}", SessionId);
    }
  }
}