using System.Collections.Concurrent;
using System.Numerics;

namespace Game.Models;

public class SpawnPointPool
{
  private readonly List<Vector2> _allSpawnPoints;
  private readonly ConcurrentDictionary<Vector2, bool> _usedSpawnPoints;
  private readonly Random _random;
  private readonly object _lock = new object();

  public SpawnPointPool()
  {
    _allSpawnPoints = new List<Vector2>
    {
      new(-3.972f, 3.703f),
      new(10.897f, 4.033f),
      new(11.737f, -5.216f),
      new(5.647f, -5.126f),
      new(-6.202f, -5.126f),
      new(-13.262f, 4.213f),
      new(-22.742f, 3.653f),
      new(-21.622f, -6.936f),
      new(-24.732f, -6.886f),
      new(-15.702f, 6.863f),
      new(-1.562f, 6.173f),
      new(-13.857f, 6.073f),
      new(5.507f, 11.963f),
      new(-18.252f, 12.453f),
      new(-1.752f, -7.376f),
      new(21.517f, -4.826f),
      new(21.717f, 3.223f),
      new(23.877f, 10.683f),
      new(15.337f, -12.296f),
      new(-15.202f, -4.736f)
    };
    _usedSpawnPoints = new ConcurrentDictionary<Vector2, bool>();
    _random = new Random();
  }

  /// <summary>
  /// 랜덤 스폰 포인트 반환
  /// </summary>
  public Vector2? GetRandomSpawnPoint()
  {
    var availablePoints = _allSpawnPoints.Where(p => !_usedSpawnPoints.ContainsKey(p)).ToList();
    if (!availablePoints.Any())
      return null;

    var spawnPoint = availablePoints[_random.Next(availablePoints.Count)];
    _usedSpawnPoints.TryAdd(spawnPoint, true);
    return spawnPoint;
  }

  /// <summary>
  /// 스폰 포인트 해제
  /// </summary>
  public void ReleaseSpawnPoint(Vector2 point)
  {
    _usedSpawnPoints.TryRemove(point, out _);
  }

  /// <summary>
  /// 스폰 포인트 초기화
  /// </summary>
  public void Reset()
  {
    _usedSpawnPoints.Clear();
  }
}