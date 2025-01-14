using Core.Protocol.Packets;

namespace Game.Models;

public class Room
{
  public int Id { get; set; }
  public int OwnerId { get; set; }
  public string Name { get; set; }
  public int MaxUserNum { get; set; }
  public RoomStateType State { get; set; }
  public Dictionary<int, User> Users { get; } = new();
  private readonly SpawnPointPool _spawnPointPool = new();

  public Room(RoomData roomData)
  {
    Id = roomData.Id;
    OwnerId = roomData.OwnerId;
    Name = roomData.Name;
    MaxUserNum = roomData.MaxUserNum;
    State = roomData.State;
  }

  /// <summary>
  /// 유저 추가
  /// </summary>
  public void AddUser(User user)
  {
    // 초기 위치 할당
    var spawnPoint = _spawnPointPool.GetRandomSpawnPoint();
    if (spawnPoint.HasValue)
    {
      user.X = spawnPoint.Value.X;
      user.Y = spawnPoint.Value.Y;
    }
    Users[user.Id] = user;
  }

  /// <summary>
  /// 유저 조회
  /// </summary>
  public User? GetUser(int userId)
  {
    return Users.GetValueOrDefault(userId);
  }

  /// <summary>
  /// 방에 있는 모든 유저 위치 정보 반환
  /// </summary>
  public List<CharacterPositionData> GetAllUserPositions()
  {
    return Users.Values.Select(u => new CharacterPositionData
    {
      Id = u.Id,
      X = u.X,
      Y = u.Y
    }).ToList();
  }
}