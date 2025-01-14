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
      {
        _propertyCache[payloadCase] = property;
        _logger.LogDebug("프로퍼티 캐시 추가: {PayloadCase} -> {PropertyName}", payloadCase, property.Name);
      }
    }

    _logger.LogInformation("PacketManager 초기화 완료: 캐시된 프로퍼티 수={Count}", _propertyCache.Count);
  }

  /// <summary>
  /// 메시지 파싱
  /// </summary>
  public IMessage? ParseMessage(ReadOnlySpan<byte> payload)
  {
    try
    {
      var gamePacket = GamePacket.Parser.ParseFrom(payload.ToArray());
      _logger.LogInformation("게임 패킷 파싱됨: PayloadCase={PayloadCase}", gamePacket.PayloadCase);

      if (_propertyCache.TryGetValue(gamePacket.PayloadCase, out var property))
      {
        var message = property.GetValue(gamePacket) as IMessage;
        if (message == null)
        {
          _logger.LogWarning("프로퍼티 값을 IMessage로 변환 실패: PayloadCase={PayloadCase}", gamePacket.PayloadCase);
        }
        return message;
      }

      _logger.LogWarning("알 수 없는 페이로드 케이스: {PayloadCase}", gamePacket.PayloadCase);
      return null;
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
