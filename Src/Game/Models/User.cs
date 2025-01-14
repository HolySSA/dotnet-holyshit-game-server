using Core.Protocol.Packets;

public class User
{
  public int Id { get; set; }
  public string Nickname { get; set; }
  public Character Character { get; set; }
  public double X { get; set; }  // 위치 정보
  public double Y { get; set; }  // 위치 정보

  public User(UserData userData)
  {
    Id = userData.Id;
    Nickname = userData.Nickname;
    Character = new Character(userData.Character);
  }

  public CharacterPositionData ToPositionData()
  {
    return new CharacterPositionData
    {
      Id = Id,
      X = X,
      Y = Y
    };
  }
}