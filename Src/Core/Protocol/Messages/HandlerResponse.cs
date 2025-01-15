using Core.Protocol;
using Core.Protocol.Packets;
using Google.Protobuf;

namespace Core.Protocol.Messages;

public class HandlerResponse
{
  public PacketId PacketId { get; }
  public uint Sequence { get; }
  public IMessage Message { get; }
  public List<int> TargetUserIds { get; }

  private HandlerResponse(PacketId packetId, uint sequence, IMessage message, List<int>? targetUserIds = null)
  {
    PacketId = packetId;
    Sequence = sequence;
    Message = message;
    TargetUserIds = targetUserIds ?? new List<int>();
  }

  /// <summary>
  /// 단일 응답 생성
  /// </summary>
  public static HandlerResponse CreateResponse(PacketId packetId, uint sequence, IMessage message)
  {
    Console.WriteLine($"Creating response: PacketId={packetId}, Message={message}");
    return new HandlerResponse(packetId, sequence, message);
  }

  /// <summary>
  /// 브로드캐스트 알림 생성
  /// </summary>
  public static HandlerResponse CreateBroadcast(PacketId packetId, IMessage message, List<int> targetUserIds)
  {
    return new HandlerResponse(packetId, SequenceGenerator.GetNextSequence(), message, targetUserIds);
  }
}