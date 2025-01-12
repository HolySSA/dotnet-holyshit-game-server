using Core.Client.Interfaces;
using Microsoft.Extensions.Logging;

namespace Core.Client;

public class ClientManager : IClientManager
{
  private readonly ILogger<ClientManager> _logger;
  private readonly Dictionary<string, ClientSession> _sessions = new();
  private readonly Dictionary<int, ClientSession> _userSessions = new();

  private readonly object _lock = new(); // 클라이언트 목록 동시 접근 제한 객체

  public ClientManager(ILogger<ClientManager> logger)
  {
    _logger = logger;
  }

  /// <summary>
  /// 세션 추가
  /// </summary>
  public void AddSession(ClientSession session)
  {
    lock (_lock)
    {
      if (_sessions.TryAdd(session.SessionId, session))
        _logger.LogInformation("세션 추가됨: {SessionId}", session.SessionId);
      else
        _logger.LogWarning("세션 추가 실패 (중복): {SessionId}", session.SessionId);
    }
  }

  /// <summary>
  /// 세션 제거
  /// </summary>
  public void RemoveSession(ClientSession session)
  {
    lock (_lock)
    {
      if (_sessions.Remove(session.SessionId))
      {
        if (session.UserId > 0)
          _userSessions.Remove(session.UserId);
        _logger.LogInformation("세션 제거: UserId={UserId}, SessionId={SessionId}", session.UserId, session.SessionId);
      }
    }
  }

  /// <summary>
  /// 유저 ID로 세션 등록
  /// </summary>
  public void RegisterUserSession(int userId, ClientSession session)
  {
    lock (_lock)
    {
      if (_userSessions.TryAdd(userId, session))
        _logger.LogInformation("유저 세션 등록: UserId={UserId}, SessionId={SessionId}", userId, session.SessionId);
      else
        _logger.LogWarning("유저 세션 등록 실패 (중복): UserId={UserId}", userId);
    }
  }

  /// <summary>
  /// 세션 ID로 세션 조회
  /// </summary>
  public ClientSession? GetSession(string sessionId)
  {
    lock (_lock)
    {
      return _sessions.GetValueOrDefault(sessionId);
    }
  }

  /// <summary>
  /// 유저 ID로 세션 조회
  /// </summary>
  public ClientSession? GetSessionByUserId(int userId)
  {
    lock (_lock)
    {
      return _userSessions.GetValueOrDefault(userId);
    }
  }

  /// <summary>
  /// 모든 세션 조회
  /// </summary>
  public IEnumerable<ClientSession> GetAllSessions()
  {
    lock (_lock)
    {
      return _sessions.Values;
    }
  }

  /// <summary>
  /// 세션 ID로 세션 조회
  /// </summary>
  public bool TryGetSession(string sessionId, out ClientSession? session)
  {
    lock (_lock)
    {
      return _sessions.TryGetValue(sessionId, out session);
    }
  }

  /// <summary>
  /// 유저 ID로 세션 조회
  /// </summary>
  public bool TryGetSessionByUserId(int userId, out ClientSession? session)
  {
    lock (_lock)
    {
      return _userSessions.TryGetValue(userId, out session);
    }
  }
}

