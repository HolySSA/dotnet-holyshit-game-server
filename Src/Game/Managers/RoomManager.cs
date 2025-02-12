using System.Collections.Concurrent;
using Core.Client.Interfaces;
using Core.Protocol.Packets;
using Game.Models;
using Game.Services;
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
  private readonly IClientManager _clientManager;
  private readonly IUserManager _userManager;
  private readonly IGameStatsService _gameStatsService;
  private readonly object _lock = new object(); // 동시성 문제 방지

  public RoomManager(ILogger<RoomManager> logger, IClientManager clientManager, IUserManager userManager, IGameStatsService gameStatsService)
  {
    _logger = logger;
    _clientManager = clientManager;
    _userManager = userManager;
    _gameStatsService = gameStatsService;

    // 이벤트 구독
    _clientManager.OnClientDisconnected += OnClientDisconnected;
  }

  private async Task OnClientDisconnected(string sessionId, int userId, int roomId)
  {
    if (roomId == 0) return;

    var room = GetRoom(roomId);
    if (room != null)
    {
      await room.RemoveUser(userId);
      _logger.LogInformation("방에서 유저 제거: RoomId={RoomId}, UserId={UserId}", roomId, userId);

      // 방에 유저가 없으면 방 제거
      if (room.Users.Count == 0)
        RemoveRoom(roomId);
    }

    // 유저 제거
    _userManager.RemoveUser(userId);
  }

  public void Dispose()
  {
    // 이벤트 구독 해제
    _clientManager.OnClientDisconnected -= OnClientDisconnected;
  }

  public Room? CreateRoom(RoomData roomData)
  {
    lock (_lock)
    {
      // 이미 존재하는 방인지 확인
      if (_rooms.TryGetValue(roomData.Id, out var existingRoom))
        return existingRoom;

      // 방 생성
      var room = new Room(roomData, _clientManager, _gameStatsService);
      if (_rooms.TryAdd(room.Id, room))
      {
        _logger.LogInformation("방 생성: {RoomId}", room.Id);
        return room;
      }

      _logger.LogWarning("방 생성 실패: {RoomId}", room.Id);
      return null;
    }
  }

  public Room? GetRoom(int roomId)
  {
    return _rooms.GetValueOrDefault(roomId);
  }

  public bool RemoveRoom(int roomId)
  {
    lock (_lock)
    {
      if (_rooms.TryRemove(roomId, out _))
      {
        _logger.LogInformation("방 제거: {RoomId}", roomId);
        return true;
      }
      return false;
    }
  }
}