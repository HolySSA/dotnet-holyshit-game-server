using Core.Protocol.Packets;

namespace Game.Models;

public class Character
{
  public CharacterType CharacterType { get; set; }
  public RoleType RoleType { get; set; }
  public int Hp { get; set; }
  public int Weapon { get; set; }

  // CharacterStateInfoData
  public CharacterStateType State { get; set; }
  public CharacterStateType NextState { get; set; }
  public long NextStateAt { get; set; }
  public int StateTargetUserId { get; set; }

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
    NextState = characterData.StateInfo.NextState;
    NextStateAt = characterData.StateInfo.NextStateAt;
    StateTargetUserId = characterData.StateInfo.StateTargetUserId;
    Equips.AddRange(characterData.Equips);
    Debuffs.AddRange(characterData.Debuffs);
    HandCards.AddRange(characterData.HandCards);
    BbangCount = characterData.BbangCount;
    HandCardsCount = characterData.HandCardsCount;
  }

  public CharacterData ToCharacterData()
  {
    return new CharacterData
    {
      CharacterType = CharacterType,
      RoleType = RoleType,
      Hp = Hp,
      Weapon = Weapon,
      StateInfo = new CharacterStateInfoData
      {
        State = State,
        NextState = NextState,
        NextStateAt = NextStateAt,
        StateTargetUserId = StateTargetUserId
      },
      Equips = { Equips },  // repeated 필드는 이렇게 초기화
      Debuffs = { Debuffs },
      HandCards = { HandCards },
      BbangCount = BbangCount,
      HandCardsCount = HandCardsCount
    };
  }
}