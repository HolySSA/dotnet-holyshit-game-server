namespace Core.Server.Configuration;

public class RedisConfig
{
  public string Host { get; set; } = "localhost";
  public int Port { get; set; } = 6379;
  public string Password { get; set; } = string.Empty;
}