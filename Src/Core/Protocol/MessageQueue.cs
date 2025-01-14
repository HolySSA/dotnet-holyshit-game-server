using System.Collections.Concurrent;
using Core.Client;
using Core.Protocol.Packets;
using Google.Protobuf;
using Microsoft.Extensions.Logging;

namespace Core.Protocol;

public record GamePacketMessage(PacketId PacketId, uint Sequence, IMessage Message);

public class MessageQueue
{
  private readonly ConcurrentQueue<GamePacketMessage> _receiveQueue = new();
  private readonly ConcurrentQueue<(PacketId, uint, IMessage)> _sendQueue = new();
  private volatile bool _processingReceive;
  private volatile bool _processingSend;

  private ClientSession? _session;
  private readonly IPacketManager _packetManager;
  private readonly PacketSerializer _packetSerializer;
  private readonly ILogger<MessageQueue> _logger;

  public MessageQueue(IPacketManager packetManager, PacketSerializer packetSerializer, ILogger<MessageQueue> logger)
  {
    _packetManager = packetManager;
    _packetSerializer = packetSerializer;
    _logger = logger;
  }

  /// <summary>
  /// 세션 설정
  /// </summary>
  public void SetSession(ClientSession session)
  {
    _session = session;
  }

  /// <summary>
  /// 수신 메시지 큐에 추가
  /// </summary>
  public async Task EnqueueReceive(PacketId packetId, uint sequence, IMessage message)
  {
    _logger.LogInformation("메시지 큐에 추가: PacketId={PacketId}, Sequence={Sequence}", packetId, sequence);

    _receiveQueue.Enqueue(new GamePacketMessage(packetId, sequence, message));
    await ProcessReceiveQueue();
  }

  /// <summary>
  /// 송신 메시지 큐에 추가
  /// </summary>
  public async Task EnqueueSend(PacketId packetId, uint sequence, IMessage message)
  {
    _sendQueue.Enqueue((packetId, sequence, message));
    await ProcessSendQueue();
  }

  /// <summary>
  /// 수신 메시지 큐 처리
  /// </summary>
  private async Task ProcessReceiveQueue()
  {
    if (_processingReceive) return;
    _processingReceive = true;

    try
    {
      while (_receiveQueue.TryDequeue(out var message))
      {
        try
        {
          await _packetManager.ProcessMessageAsync(_session, message.PacketId, message.Sequence, message.Message);
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "수신 처리 오류: SessionId={SessionId}", _session.SessionId);
        }
      }
    }
    finally
    {
      _processingReceive = false;
    }
  }

  /// <summary>
  /// 송신 메시지 큐 처리
  /// </summary>
  private async Task ProcessSendQueue()
  {
    if (_processingSend) return;
    _processingSend = true;

    try
    {
      while (_sendQueue.TryDequeue(out var message))
      {
        try
        {
          var (packetId, sequence, data) = message;
          var serializedData = _packetSerializer.Serialize(packetId, data, sequence);
          if (serializedData != null)
            await _session.SendAsync(serializedData);
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "송신 처리 오류: SessionId={SessionId}", _session.SessionId);
        }
      }
    }
    finally
    {
      _processingSend = false;
    }
  }
}