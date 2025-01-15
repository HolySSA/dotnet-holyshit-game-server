using System.Text;
using Google.Protobuf;
using Core.Protocol.Packets;
using Microsoft.Extensions.Logging;

namespace Core.Protocol;

public class PacketSerializer
{
  public const int HEADER_SIZE = 11; // 2(type) + 1(versionLength) + 4(sequence) + 4(payloadLength)
  private static readonly string VERSION = "1.0.0"; // 버전 정보

  private readonly IPacketManager _packetManager;
  private readonly ILogger<PacketSerializer> _logger;

  public PacketSerializer(IPacketManager packetManager, ILogger<PacketSerializer> logger)
  {
    _packetManager = packetManager;
    _logger = logger;
  }

  /// <summary>
  /// S2C 패킷 직렬화 (Big Endian)
  /// </summary>
  public byte[]? Serialize<T>(PacketId packetId, T message, uint sequence) where T : IMessage
  {
    try
    {
      if (message == null)
      {
        _logger.LogError("메시지가 null입니다: PacketId={PacketId}", packetId);
        return null;
      }

      var messageType = message.GetType();
      var messageBytes = message.ToByteArray();
      _logger.LogInformation("메시지 직렬화: PacketId={PacketId}, MessageType={MessageType}, Content={Content}, ByteLength={Length}", 
            packetId, 
            messageType.Name,  // 실제 구체적인 타입 이름
            message.ToString(),  // 메시지 내용
            messageBytes.Length);
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

      _logger.LogInformation("패킷 직렬화 완료: TotalLength={Length}, MessageType={MessageType}, Data={Data}", 
            result.Length,
            messageType.Name,
            BitConverter.ToString(result));

      return result;
    }
    catch (Exception ex)
    {
      Console.WriteLine($"[PacketSerializer] Serialization 실패: {ex.Message}");
      return null;
    }
  }

  /// <summary>
  /// C2S 패킷 역직렬화 (Little Endian)
  /// </summary>
  public (PacketId id, uint sequence, IMessage? message)? Deserialize(byte[] buffer)
  {
    try
    {
      if (buffer.Length < HEADER_SIZE)
      {
        _logger.LogWarning("버퍼 크기가 헤더보다 작음: {Length} < {HeaderSize}", buffer.Length, HEADER_SIZE);
        return null;
      }

      var offset = 0;

      // 패킷 타입 (2 bytes)
      var packetId = (PacketId)ReadUInt16(buffer, ref offset);

      // 버전 길이 (1 byte) + 버전 문자열
      var versionLength = buffer[offset++];
      if (offset + versionLength > buffer.Length)
      {
        _logger.LogWarning("버전 문자열이 버퍼를 초과.");
        return null;
      }
      var version = Encoding.UTF8.GetString(buffer, offset, versionLength);
      if (version != VERSION)
      {
        _logger.LogWarning("버전 불일치: 기대={ExpectedVersion}, 실제={ActualVersion}", VERSION, version);
        return null;
      }
      offset += versionLength;

      // 시퀀스 (4 bytes)
      var sequence = ReadUInt32(buffer, ref offset);

      // 페이로드 길이 (4 bytes)
      var payloadLength = ReadInt32(buffer, ref offset);
      if (payloadLength <= 0 || payloadLength > 1024 * 1024)
      {
        _logger.LogWarning("잘못된 페이로드 길이: {PayloadLength}", payloadLength);
        return null;
      }

      // 페이로드
      if (buffer.Length < offset + payloadLength)
      {
        _logger.LogWarning("페이로드 길이가 버퍼를 초과");
        return null;
      }

      var payload = new byte[payloadLength];
      Array.Copy(buffer, offset, payload, 0, payloadLength);

      var message = _packetManager.ParseMessage(payload);
      if (message == null)
      {
        _logger.LogWarning("메시지 파싱 실패: PacketId={PacketId}", packetId);
        return null;
      }

      return (packetId, sequence, message);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Deserialization 실패");
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