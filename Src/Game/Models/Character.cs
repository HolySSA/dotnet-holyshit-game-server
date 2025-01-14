using Core.Protocol.Packets;

public class Character
{
  public CharacterType CharacterType { get; set; }
  public RoleType RoleType { get; set; }
  public int Hp { get; set; }
  public int Weapon { get; set; }
  public CharacterStateType State { get; set; }
  public List<int> Equips { get; } = new();
  public List<int> Debuffs { get; } = new();
  public List<CardData> HandCards { get; } = new();
  public int BbangCount { get; set; }
  public int HandCardsCount { get; set; }

  public Character(CharacterData characterData)
  {
    CharacterType = characterData.CharacterType;
    RoleType = characterData.RoleType;
    Hp = characterData.Hp;
    Weapon = characterData.Weapon;
    State = characterData.StateInfo.State;
    Equips.AddRange(characterData.Equips);
    Debuffs.AddRange(characterData.Debuffs);
    HandCards.AddRange(characterData.HandCards);
    BbangCount = characterData.BbangCount;
    HandCardsCount = characterData.HandCardsCount;
  }
}