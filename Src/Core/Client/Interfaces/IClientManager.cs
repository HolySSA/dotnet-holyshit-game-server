namespace Core.Client.Interfaces;

public interface IClientManager
{
  void AddSession(ClientSession session);
  void RemoveSession(ClientSession session);
  void RegisterUserSession(int userId, ClientSession session);
  IEnumerable<ClientSession> GetAllSessions();
  bool TryGetSession(string sessionId, out ClientSession? session);
  bool TryGetSessionByUserId(int userId, out ClientSession? session);
  ClientSession? GetSession(string sessionId);
  ClientSession? GetSessionByUserId(int userId);

  // 이벤트
  event Func<string, int, int, Task>? OnClientDisconnected; // sessionId, userId, roomId
}