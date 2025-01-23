using System.Net.Sockets;
using Microsoft.Extensions.Logging;

public interface IClientConnection : IDisposable
{
  bool IsConnected { get; }
  event Action<byte[]> OnDataReceived;
  event Action OnDisconnected;
  
  Task SendAsync(byte[] data);
  Task StartAsync(CancellationToken cancellationToken = default);
}

public class TcpClientConnection : IClientConnection
{
  private readonly NetworkStream _stream; // 스트림
  private readonly ILogger<TcpClientConnection> _logger; // 로거
  private readonly CancellationTokenSource _cts; // 취소 토큰
  private bool _disposed; // 객체 해제 여부

  public bool IsConnected => !_disposed; // 클라이언트 연결 상태
  public event Action<byte[]>? OnDataReceived; // 데이터 수신 시 발생하는 이벤트
  public event Action? OnDisconnected; // 연결 종료 시 발생하는 이벤트

  public TcpClientConnection(NetworkStream stream, ILogger<TcpClientConnection> logger)
  {
    _stream = stream;
    _logger = logger;
    _cts = new CancellationTokenSource();
  }

  /// <summary>
  /// 네트워크 연결 시작
  /// </summary>
  public async Task StartAsync(CancellationToken cancellationToken = default)
  {
    try
    {
      using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _cts.Token);
      await ReceiveLoopAsync(linkedCts.Token);
    }
    catch (OperationCanceledException)
    {
      _logger.LogInformation("네트워크 연결이 정상적으로 종료되었습니다.");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "네트워크 수신 중 오류 발생");
    }
    finally
    {
      OnDisconnected?.Invoke();
    }
  }

  /// <summary>
  /// 데이터 수신 루프
  /// </summary>
  private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
  {
    var buffer = new byte[4096];
    var messageBuffer = new List<byte>();

    try
    {
      while (!cancellationToken.IsCancellationRequested && !_disposed)
      {
        int bytesRead;
        try 
        {
          bytesRead = await _stream.ReadAsync(buffer, cancellationToken);
        }
        catch (IOException ex) when (ex.InnerException is SocketException socketEx
          && (socketEx.SocketErrorCode == SocketError.ConnectionReset 
          || socketEx.SocketErrorCode == SocketError.ConnectionAborted))
        {
          _logger.LogInformation("클라이언트가 연결을 종료했습니다.");
          break;
        }

        if (bytesRead == 0) 
        {
          _logger.LogInformation("정상적인 연결 종료");
          break;
        }

        messageBuffer.AddRange(new ArraySegment<byte>(buffer, 0, bytesRead));
        OnDataReceived?.Invoke(messageBuffer.ToArray());
        messageBuffer.Clear();
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "데이터 수신 중 오류 발생");
      throw;
    }
  }

  /// <summary>
  /// 데이터 전송
  /// </summary>
  public async Task SendAsync(byte[] data)
  {
    if (_disposed)
      throw new ObjectDisposedException(nameof(TcpClientConnection));

    try
    {
      await _stream.WriteAsync(data);
      await _stream.FlushAsync();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "데이터 전송 중 오류 발생");
      throw;
    }
  }

  /// <summary>
  /// 연결 자원 정리
  /// </summary>
  public void Dispose()
  {
    if (_disposed) return;
    _disposed = true;
    
    _cts.Cancel();
    _cts.Dispose();
    _stream?.Dispose();
  }
}