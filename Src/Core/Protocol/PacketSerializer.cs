using System.Text;
using Google.Protobuf;
using Core.Protocol.Packets;

namespace Core.Protocol;

public class PacketSerializer
{
  public const int HEADER_SIZE = 11; // 2(type) + 1(versionLength) + 4(sequence) + 4(payloadLength)
  private static readonly string VERSION = "1.0.0"; // 버전 정보

  private readonly IPacketManager _packetManager;

  public PacketSerializer(IPacketManager packetManager)
  {
    _packetManager = packetManager;
  }

  /// <summary>
  /// 서버에서 클라이언트로 보내는 메시지 직렬화 (Big Endian)
  /// </summary>
  public byte[]? Serialize<T>(PacketId packetId, T message, uint sequence) where T : IMessage
  {
    try
    {
      var messageBytes = message.ToByteArray();
      var versionBytes = Encoding.UTF8.GetBytes(VERSION);
      var totalLength = HEADER_SIZE + versionBytes.Length + messageBytes.Length;
      var result = new byte[totalLength];
      var offset = 0;

      // 패킷 타입 (2 bytes)
      WriteBytes(result, ref offset, BitConverter.GetBytes((ushort)packetId));

      // 버전 길이 (1 byte) + 버전 문자열
      result[offset++] = (byte)versionBytes.Length;
      versionBytes.CopyTo(result, offset);
      offset += versionBytes.Length;

      // 시퀀스 (4 bytes)
      WriteBytes(result, ref offset, BitConverter.GetBytes(sequence));

      // 페이로드 길이 (4 bytes)
      WriteBytes(result, ref offset, BitConverter.GetBytes(messageBytes.Length));

      // 페이로드
      messageBytes.CopyTo(result, offset);

      return result;
    }
    catch (Exception ex)
    {
      Console.WriteLine($"[PacketSerializer] Serialization 실패: {ex.Message}");
      return null;
    }
  }

  /// <summary>
  /// 클라이언트에서 서버로 받은 메시지 역직렬화 (Little Endian)
  /// </summary>
  public (PacketId id, uint sequence, IMessage? message)? Deserialize(byte[] buffer)
  {
    try
    {
      if (buffer.Length < HEADER_SIZE) return null;

      var offset = 0;

      // 패킷 타입 (2 bytes)
      var packetId = (PacketId)ReadUInt16(buffer, ref offset);

      // 버전 길이 (1 byte) + 버전 문자열
      var versionLength = buffer[offset++];
      if (offset + versionLength > buffer.Length) return null;
      offset += versionLength;

      // 시퀀스 (4 bytes)
      var sequence = ReadUInt32(buffer, ref offset);

      // 페이로드 길이 (4 bytes)
      var payloadLength = ReadInt32(buffer, ref offset);
      if (payloadLength <= 0 || payloadLength > 1024 * 1024) return null;

      // 페이로드
      if (buffer.Length < offset + payloadLength) return null;
      var payload = new byte[payloadLength];
      Array.Copy(buffer, offset, payload, 0, payloadLength);

      var message = _packetManager.ParseMessage(buffer.AsSpan(offset, payloadLength));
      return message == null ? null : (packetId, sequence, message);
    }
    catch (Exception ex)
    {
      Console.WriteLine($"[PacketSerializer] Deserialization 실패: {ex.Message}");
      return null;
    }
  }

  /// <summary>
  /// 패킷 크기와 유효성 검증
  /// </summary>
  public (bool isValid, int totalSize) GetExpectedPacketSize(byte[] buffer)
  {
    try
    {
      if (buffer.Length < HEADER_SIZE)
        return (true, 0);

      var offset = 0;

      // 패킷 타입 검증
      var packetType = ReadUInt16(buffer, ref offset);
      if (!Enum.IsDefined(typeof(PacketId), packetType))
        return (false, 0);

      // 버전 길이 검증
      var versionLength = buffer[offset++];
      if (versionLength != VERSION.Length)
        return (false, 0);

      // 시퀀스 건너뛰기
      offset += versionLength + 4;

      // 페이로드 길이
      var payloadLength = ReadInt32(buffer, ref offset);
      if (payloadLength <= 0 || payloadLength > 1024 * 1024)
        return (false, 0);

      return (true, HEADER_SIZE + versionLength + payloadLength);
    }
    catch (Exception ex)
    {
      Console.WriteLine($"[PacketSerializer] GetExpectedPacketSize 실패: {ex.Message}");
      return (false, 0);
    }
  }

  /// <summary>
  /// 바이트 배열 쓰기 (Big Endian)
  /// </summary>
  private void WriteBytes(byte[] buffer, ref int offset, byte[] data)
  {
    if (BitConverter.IsLittleEndian)
      Array.Reverse(data);
    data.CopyTo(buffer, offset);
    offset += data.Length;
  }

  /// <summary>
  /// 2바이트 읽기 (Big Endian)
  /// </summary>
  private ushort ReadUInt16(byte[] buffer, ref int offset)
  {
    var bytes = new byte[2];
    Array.Copy(buffer, offset, bytes, 0, 2);
    if (BitConverter.IsLittleEndian)
      Array.Reverse(bytes);
    offset += 2;
    return BitConverter.ToUInt16(bytes, 0);
  }

  /// <summary>
  /// 4바이트 읽기 (Big Endian)
  /// </summary>
  private uint ReadUInt32(byte[] buffer, ref int offset)
  {
    var bytes = new byte[4];
    Array.Copy(buffer, offset, bytes, 0, 4);
    if (BitConverter.IsLittleEndian)
      Array.Reverse(bytes);
    offset += 4;
    return BitConverter.ToUInt32(bytes, 0); // 부호 없는 정수
  }

  /// <summary>
  /// 4바이트 읽기 (Big Endian)
  /// </summary>
  private int ReadInt32(byte[] buffer, ref int offset)
  {
    var bytes = new byte[4];
    Array.Copy(buffer, offset, bytes, 0, 4);
    if (BitConverter.IsLittleEndian)
      Array.Reverse(bytes);
    offset += 4;
    return BitConverter.ToInt32(bytes, 0); // 부호 있는 정수
  }
}