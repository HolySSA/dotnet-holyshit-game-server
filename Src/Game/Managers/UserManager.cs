using System.Collections.Concurrent;
using Core.Protocol.Packets;
using Game.Models;
using Microsoft.Extensions.Logging;

namespace Game.Managers;

public interface IUserManager
{
  User? CreateUser(UserData userData);
  User? GetUser(int userId);
  bool RemoveUser(int userId);
}

public class UserManager : IUserManager
{
  private readonly ConcurrentDictionary<int, User> _users = new();
  private readonly ILogger<UserManager> _logger;

  public UserManager(ILogger<UserManager> logger)
  {
    _logger = logger;
  }

  public User? CreateUser(UserData userData)
  {
    var user = new User(userData);
    if (_users.TryAdd(user.Id, user))
    {
      _logger.LogInformation("유저 생성: {UserId}", user.Id);
      return user;
    }

    _logger.LogWarning("유저 생성 실패 (이미 존재): {UserId}", user.Id);
    return null;
  }

  public User? GetUser(int userId)
  {
    return _users.GetValueOrDefault(userId);
  }

  public bool RemoveUser(int userId)
  {
    if (_users.TryRemove(userId, out _))
    {
      _logger.LogInformation("유저 제거: {UserId}", userId);
      return true;
    }
    return false;
  }
}