using System.Net.Sockets;
using Core.Client.Interfaces;
using Core.Protocol;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Core.Client;

public class ClientSession : IDisposable
{
  private readonly IClientConnection _clientConnection; // 클라이언트 연결
  private readonly IClientManager _clientManager; // 클라이언트 매니저
  private readonly MessageQueue _messageQueue; // 메시지 큐
  private readonly PacketSerializer _packetSerializer; // 패킷 직렬화
  private readonly ILogger<ClientSession> _logger; // 로거
  private bool _disposed; // 객체 해제 여부

  public string SessionId { get; } // 세션 ID
  public int UserId { get; private set; } // 유저 ID
  public int RoomId { get; private set; } // 방 ID
  public IServiceScope? ServiceScope { get; set; } // 현재 요청 서비스 스코프
  public IServiceProvider ServiceProvider { get; } // DI 컨테이너
  public MessageQueue MessageQueue => _messageQueue; // 메시지 큐 접근자

  private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
  private readonly CancellationTokenSource _sessionCts = new CancellationTokenSource();

  public ClientSession(TcpClient client, IServiceProvider serviceProvider)
  {
    SessionId = Guid.NewGuid().ToString();
    ServiceProvider = serviceProvider;

    // 의존성 주입
    _clientManager = serviceProvider.GetRequiredService<IClientManager>();
    _packetSerializer = serviceProvider.GetRequiredService<PacketSerializer>();
    _messageQueue = serviceProvider.GetRequiredService<MessageQueue>();
    _logger = serviceProvider.GetRequiredService<ILogger<ClientSession>>();

    _messageQueue.SetSession(this);

    // 네트워크 연결 초기화
    _clientConnection = new TcpClientConnection(
      client.GetStream(),
      serviceProvider.GetRequiredService<ILogger<TcpClientConnection>>());

    _clientConnection.OnDataReceived += HandleDataReceived;
    _clientConnection.OnDisconnected += HandleDisconnected;

    // 클라이언트 세션 추가
    _clientManager.AddSession(this);
  }

  /// <summary>
  /// 클라이언트 세션 시작
  /// </summary>
  public async Task StartAsync(CancellationToken cancellationToken = default)
  {
    try
    {
      _logger.LogInformation("새로운 클라이언트 연결: {SessionId}", SessionId);
      await _clientConnection.StartAsync(cancellationToken);
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
  /// 데이터 수신 처리
  /// </summary>
  private async void HandleDataReceived(byte[] data)
  {
    try
    {
      var result = _packetSerializer.Deserialize(data);
      if (result.HasValue)
      {
        var (packetId, sequence, message) = result.Value;

        if (message != null)
          await _messageQueue.EnqueueReceive(packetId, sequence, message);
      }
      else
      {
        _logger.LogWarning("패킷 역직렬화 실패: SessionId={SessionId}", SessionId);  // 역직렬화 실패 로그 추가
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "패킷 처리 중 오류 발생: {SessionId}", SessionId);
    }
  }

  /// <summary>
  /// 연결 종료 처리
  /// </summary>
  private void HandleDisconnected()
  {
    _logger.LogInformation("클라이언트 연결 종료: {SessionId}", SessionId);
    Dispose();
  }

  /// <summary>
  /// 패킷 전송
  /// </summary>
  public async Task SendAsync(byte[] data)
  {
    if (_disposed)
      return;

    if (data == null)
    {
      _logger.LogError("전송할 데이터가 null입니다");
      return;
    }

    try
    {
      await _sendLock.WaitAsync();
      
      try
      {
        await _clientConnection.SendAsync(data);
      }
      finally
      {
        _sendLock.Release();
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "데이터 전송 중 오류 발생: SessionId={SessionId}", SessionId);
      Dispose();
    }
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
  /// 방 ID 설정
  /// </summary>
  public void SetRoomId(int roomId)
  {
    RoomId = roomId;
  }

  /// <summary>
  /// 세션 정리
  /// </summary>
  public void Dispose()
  {
    if (_disposed) return;
    _disposed = true;

    lock (this)
    {
      try
      {
        _sessionCts.Cancel();
        _clientManager.RemoveSession(this);
        _clientConnection.Dispose();
        ServiceScope?.Dispose();
        _sendLock.Dispose();
        _sessionCts.Dispose();

        _logger.LogInformation("세션 종료: {SessionId}", SessionId);
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "세션 정리 중 오류 발생: {SessionId}", SessionId);
      }
    }
  }
}