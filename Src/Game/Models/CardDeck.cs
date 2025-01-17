using Core.Protocol.Packets;
using System.Security.Cryptography;

namespace Game.Models;

public class CardDeck
{
  private readonly List<CardData> _cards = new();
    
  public CardDeck()
  {
    InitializeDeck();
    Shuffle();
  }

  /// <summary>
  /// 카드 덱 초기화
  /// </summary>
  private void InitializeDeck()
  {
    AddCards(CardType.Bbang, 20);
    AddCards(CardType.BigBbang, 1);
    AddCards(CardType.Shield, 10);
    AddCards(CardType.Vaccine, 6);
    AddCards(CardType.Call119, 2);
    AddCards(CardType.DeathMatch, 4);
    AddCards(CardType.Guerrilla, 1);
    AddCards(CardType.Absorb, 4);
    AddCards(CardType.Hallucination, 4);
    AddCards(CardType.FleaMarket, 3);
    AddCards(CardType.MaturedSavings, 2);
    AddCards(CardType.WinLottery, 1);
    AddCards(CardType.SniperGun, 1);
    AddCards(CardType.HandGun, 2);
    AddCards(CardType.DesertEagle, 3);
    AddCards(CardType.AutoRifle, 2);
    AddCards(CardType.LaserPointer, 1);
    AddCards(CardType.Radar, 1);
    AddCards(CardType.AutoShield, 2);
    AddCards(CardType.StealthSuit, 2);
    AddCards(CardType.ContainmentUnit, 3);
    AddCards(CardType.SatelliteTarget, 1);
    AddCards(CardType.Bomb, 1);
  }

  /// <summary>
  /// 카드 추가
  /// </summary>
  private void AddCards(CardType type, int count)
  {
    for (int i = 0; i < count; i++)
    {
      _cards.Add(new CardData { Type = type, Count = 1 });
    }
  }

  /// <summary>
  /// 카드 섞기
  /// </summary>
  private void Shuffle()
  {
    int n = _cards.Count;
    while (n > 1)
    {
      n--;
      int k = RandomNumberGenerator.GetInt32(n + 1);
      (_cards[k], _cards[n]) = (_cards[n], _cards[k]); // 보안을 위해 암호학적 난수 생성기 사용
    }
  }

  /// <summary>
  /// 카드 뽑기
  /// </summary>
  public List<CardData> DrawCards(int count)
  {
    // 남은 카드만큼만 뽑기
    if (count > _cards.Count)
      count = _cards.Count;

    var drawnCards = _cards.Take(count).ToList();
    _cards.RemoveRange(0, count);
    return drawnCards;
  }

  /// <summary>
  /// 남은 카드 수
  /// </summary>
  public int RemainingCards => _cards.Count;

  /// <summary>
  /// 사용한 카드 덱 맨 아래에 다시 추가
  /// </summary>
  public void AddUsedCard(CardData card)
  {
    _cards.Add(card); // 맨 뒤에 추가
  }
}