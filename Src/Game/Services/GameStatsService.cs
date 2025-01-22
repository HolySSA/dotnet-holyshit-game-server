using Core.Protocol.Packets;
using Core.Server.Services;
using Microsoft.Extensions.Logging;

namespace Game.Services;

public interface IGameStatsService
{
  Task IncrementPlayCountAsync(int userId, CharacterType characterType);
  Task IncrementWinCountAsync(int userId, CharacterType characterType);
}

public class GameStatsService : IGameStatsService
{
  private readonly IRedisService _redisService;
  private readonly ILogger<GameStatsService> _logger;
  private const string USER_CHARACTERS_KEY_FORMAT = "user:{0}:characters";

  public GameStatsService(IRedisService redisService, ILogger<GameStatsService> logger)
  {
    _redisService = redisService;
    _logger = logger;
  }

  public async Task IncrementPlayCountAsync(int userId, CharacterType characterType)
  {
    try
    {
      var db = _redisService.GetDatabase();
      var key = string.Format(USER_CHARACTERS_KEY_FORMAT, userId);
      var field = (int)characterType;

      // 현재 통계 가져오기
      var currentStats = await db.HashGetAsync(key, field);
      if (!currentStats.HasValue)
      {
        _logger.LogWarning("캐릭터 통계 없음: UserId={UserId}, CharacterType={CharacterType}", userId, characterType);
        return;
      }

      // "playCount:winCount" 형식의 값 파싱
      var stats = currentStats.ToString().Split(':');
      if (stats.Length != 2 || !int.TryParse(stats[0], out int playCount) || !int.TryParse(stats[1], out int winCount))
      {
        _logger.LogError("잘못된 통계 형식: {Stats}", currentStats);
        return;
      }

      // 통계 업데이트
      playCount++;

      // 새로운 값 저장
      var newValue = $"{playCount}:{winCount}";
      await db.HashSetAsync(key, field, newValue);

      _logger.LogInformation(
        "플레이 카운트 증가: UserId={UserId}, CharacterType={CharacterType}, PlayCount={PlayCount}",
        userId, characterType, playCount);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "플레이 카운트 증가 실패: UserId={UserId}, CharacterType={CharacterType}", userId, characterType);
    }
  }

  public async Task IncrementWinCountAsync(int userId, CharacterType characterType)
  {
    try
    {
      var db = _redisService.GetDatabase();
      var key = string.Format(USER_CHARACTERS_KEY_FORMAT, userId);
      var field = (int)characterType;

      var currentStats = await db.HashGetAsync(key, field);
      if (!currentStats.HasValue)
      {
        _logger.LogWarning("캐릭터 통계 없음: UserId={UserId}, CharacterType={CharacterType}", userId, characterType);
        return;
      }

      var stats = currentStats.ToString().Split(':');
      if (stats.Length != 2 || !int.TryParse(stats[0], out int playCount) || !int.TryParse(stats[1], out int winCount))
      {
        _logger.LogError("잘못된 통계 형식: {Stats}", currentStats);
        return;
      }

      winCount++;
      var newValue = $"{playCount}:{winCount}";
      await db.HashSetAsync(key, field, newValue);

      _logger.LogInformation(
        "승리 카운트 증가: UserId={UserId}, CharacterType={CharacterType}, WinCount={WinCount}", 
        userId, characterType, winCount);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "승리 카운트 증가 실패: UserId={UserId}, CharacterType={CharacterType}", userId, characterType);
    }
  }
}