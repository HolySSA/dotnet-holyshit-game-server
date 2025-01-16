namespace Game.Models;

public class Monster
{
  public string Id { get; set; } = string.Empty;
  public string Name { get; set; } = string.Empty;
  public string Hp { get; set; } = string.Empty;
}

public class MonsterData
{
  public List<Monster> Data { get; set; } = new();
}

