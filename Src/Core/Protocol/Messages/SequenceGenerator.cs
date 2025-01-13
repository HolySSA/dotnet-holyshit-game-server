namespace Core.Protocol.Messages;

/// <summary>
/// 시퀀스 생성기
/// </summary>
public static class SequenceGenerator
{
  private static uint _currentSequence;
  
  /// <summary>
  /// 다음 시퀀스 번호 반환
  /// </summary>
  public static uint GetNextSequence() => Interlocked.Increment(ref _currentSequence);
}