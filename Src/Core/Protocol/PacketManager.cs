using System.Reflection;
using Core.Client;
using Core.Protocol.Packets;
using Google.Protobuf;
using Microsoft.Extensions.Logging;

namespace Core.Protocol;

public interface IPacketManager
{
  IMessage? ParseMessage(ReadOnlySpan<byte> payload);
  Task ProcessMessageAsync(ClientSession client, PacketId id, uint sequence, IMessage message);
}

public class PacketManager : IPacketManager
{
  private static readonly Dictionary<GamePacket.PayloadOneofCase, PropertyInfo> _propertyCache = new();
  private readonly IHandlerManager _handlerManager;
  private readonly ILogger<PacketManager> _logger;

  public PacketManager(ILogger<PacketManager> logger, IHandlerManager handlerManager)
  {
    _handlerManager = handlerManager;
    _logger = logger;
    Initialize();
  }

  /// <summary>
  /// 프로퍼티 캐시 초기화
  /// </summary>
  private void Initialize()
  {
    foreach (var payloadCase in Enum.GetValues<GamePacket.PayloadOneofCase>())
    {
      if (payloadCase == GamePacket.PayloadOneofCase.None) continue;

      var property = typeof(GamePacket).GetProperty(payloadCase.ToString());
      if (property != null)
        _propertyCache[payloadCase] = property;
    }

    _logger.LogInformation("PacketManager 초기화 완료");
  }

  /// <summary>
  /// 메시지 파싱
  /// </summary>
  public IMessage? ParseMessage(ReadOnlySpan<byte> payload)
  {
    try
    {
      var gamePacket = GamePacket.Parser.ParseFrom(payload.ToArray());
      return _propertyCache.TryGetValue(gamePacket.PayloadCase, out var property)
        ? property.GetValue(gamePacket) as IMessage : null;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "메시지 파싱 중 오류 발생");
      return null;
    }
  }

  /// <summary>
  /// 메시지 처리
  /// </summary>
  public async Task ProcessMessageAsync(ClientSession client, PacketId id, uint sequence, IMessage message)
  {
    try
    {
      await _handlerManager.HandleMessageAsync(client, id, sequence, message);
      _logger.LogDebug("메시지 처리 완료: ID={PacketId}, Sequence={Sequence}", id, sequence);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "메시지 처리 실패: ID={PacketId}, Sequence={Sequence}", id, sequence);
      throw;
    }
  }
}
