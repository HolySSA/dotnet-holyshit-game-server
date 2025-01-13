using Core.Protocol.Packets;
using Core.Client;
using Google.Protobuf;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Core.Protocol.Handlers;
using Core.Client.Interfaces;
using Core.Protocol.Messages;

namespace Core.Protocol;

public interface IHandlerManager
{
  void OnHandlers<T>(PacketId packetId, Func<ClientSession, uint, T, Task<HandlerResponse>> handler) where T : IMessage;
  Task HandleMessageAsync(ClientSession client, PacketId packetId, uint sequence, IMessage message);
}

public class HandlerManager : IHandlerManager
{
  private readonly ConcurrentDictionary<PacketId, Func<ClientSession, uint, IMessage, Task<HandlerResponse>>> _handlers = new();
  private readonly IClientManager _clientManager;
  private readonly ILogger<HandlerManager> _logger;

  public HandlerManager(IClientManager clientManager, ILogger<HandlerManager> logger)
  {
    _clientManager = clientManager;
    _logger = logger;
    RegisterHandlers();
  }

  /// <summary>
  /// 모든 핸들러 등록
  /// </summary>
  private void RegisterHandlers()
  {
    OnHandlers<C2SPositionUpdateRequest>(PacketId.PositionUpdateRequest, GamePacketHandler.HandlePositionUpdate);

    _logger.LogInformation("게임 패킷 핸들러 등록 완료");
  }

  /// <summary>
  /// 핸들러 등록
  /// </summary>
  public void OnHandlers<T>(PacketId packetId, Func<ClientSession, uint, T, Task<HandlerResponse>> handler) where T : IMessage
  {
    _handlers[packetId] = async (client, seq, message) => await handler(client, seq, (T)message);
    _logger.LogDebug("핸들러 등록: {PacketId}", packetId);
  }

  /// <summary>
  /// 메시지 처리
  /// </summary>
   public async Task HandleMessageAsync(ClientSession client, PacketId packetId, uint sequence, IMessage message)
  {
    if (!_handlers.TryGetValue(packetId, out var handler))
    {
      _logger.LogWarning("핸들러 없음: {PacketId}", packetId);
      return;
    }

    try
    {
      var response = await handler(client, sequence, message);
      if (response != null)
        await ProcessResponse(client, response);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "메시지 처리 실패: {PacketId}, Sequence: {Sequence}", packetId, sequence);
      throw;
    }
  }

  /// <summary>
  /// (S2C) 응답 메시지 큐에 담기
  /// </summary>
  private async Task ProcessResponse(ClientSession client, HandlerResponse response)
  {
    if (response.TargetUserIds.Any())
    {
      // 브로드캐스트
      foreach (var userId in response.TargetUserIds)
      {
        var targetSession = _clientManager.GetSessionByUserId(userId);
        if (targetSession != null)
        {
          await targetSession.MessageQueue.EnqueueSend(
            response.PacketId,
            response.Sequence,
            response.Message);
        }
      }
    }
    else
    {
      // 단일 클라이언트 응답
      await client.MessageQueue.EnqueueSend(
        response.PacketId,
        response.Sequence,
        response.Message);
    }
  }
}