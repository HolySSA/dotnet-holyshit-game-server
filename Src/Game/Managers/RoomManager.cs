using System.Collections.Concurrent;
using Core.Protocol.Packets;
using Game.Models;
using Microsoft.Extensions.Logging;

namespace Game.Managers;

public interface IRoomManager
{
  Room? CreateRoom(RoomData roomData);
  Room? GetRoom(int roomId);
  bool RemoveRoom(int roomId);
}

public class RoomManager : IRoomManager
{
  private readonly ConcurrentDictionary<int, Room> _rooms = new();
  private readonly ILogger<RoomManager> _logger;

  public RoomManager(ILogger<RoomManager> logger)
  {
    _logger = logger;
  }

  public Room? CreateRoom(RoomData roomData)
  {
    var room = new Room(roomData);
    if (_rooms.TryAdd(room.Id, room))
    {
      _logger.LogInformation("방 생성: {RoomId}", room.Id);
      return room;
    }
    
    _logger.LogWarning("방 생성 실패 (이미 존재): {RoomId}", room.Id);
    return null;
  }

  public Room? GetRoom(int roomId)
  {
    return _rooms.GetValueOrDefault(roomId);
  }

  public bool RemoveRoom(int roomId)
  {
    if (_rooms.TryRemove(roomId, out _))
    {
      _logger.LogInformation("방 제거: {RoomId}", roomId);
      return true;
    }
    return false;
  }
}