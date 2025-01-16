using Constants;
using Game.Models;
using Microsoft.Extensions.Logging;
using Utils.Loader;

namespace Core.Server;

public class DataManager
{
  private readonly JsonFileLoader _jsonLoader;
  private MonsterData? _monsterData;
  private readonly ILogger<DataManager> _logger;

  public DataManager(ILogger<DataManager> logger)
  {
    _jsonLoader = new JsonFileLoader();
    _logger = logger;
  }

  public async Task InitializeDataAsync()
  {
    await Task.WhenAll(
      LoadMonsterDataAsync()
    );
  }

  private async Task LoadMonsterDataAsync()
  {
    try
    {
      _monsterData = await Task.Run(() =>
        _jsonLoader.LoadFromAssets<MonsterData>(PathConstants.Assets.DataFiles.MONSTER_INFO)
      );

      _logger.LogInformation($"몬스터 데이터 로드 성공: {_monsterData.Data.Count}개");
    }
    catch (Exception ex)
    {
      _logger.LogError($"몬스터 데이터 로드 실패: {ex.Message}");
      throw;
    }
  }

  public Monster? GetMonsterById(string id)
  {
    return _monsterData?.Data.FirstOrDefault(m => m.Id == id);
  }
}