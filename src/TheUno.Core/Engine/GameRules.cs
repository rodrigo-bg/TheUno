using System.Collections.Generic;
using TheUno.Core.Models;

namespace TheUno.Core.Engine;

public class GameRules
{
    public bool IsPlayLegal(Card cardToPlay, Card topCard, CardColor activeColor, List<Card> playerHand)
    {
        if (cardToPlay == null || topCard == null || playerHand == null)
        {
            return false;
        }

        // 1. Comodín clásico (Wild): siempre se puede jugar
        if (cardToPlay.Type == CardType.Wild)
        {
            return true;
        }

        // 2. Comodín +4 (Wild Draw Four): sujeto a la regla oficial de Mattel
        if (cardToPlay.Type == CardType.WildDrawFour)
        {
            return CanPlayWildDrawFour(playerHand, activeColor);
        }

        // 3. Coincidencia por color activo
        if (cardToPlay.Color == activeColor)
        {
            return true;
        }

        // 4. Coincidencia por número (solo si ambas son cartas numéricas)
        if (cardToPlay.Type == CardType.Number && topCard.Type == CardType.Number)
        {
            if (cardToPlay.Number.HasValue && topCard.Number.HasValue)
            {
                if (cardToPlay.Number.Value == topCard.Number.Value)
                {
                    return true;
                }
            }
        }

        // 5. Coincidencia por tipo de acción (Skip sobre Skip, Reverse sobre Reverse, etc.)
        if (cardToPlay.Type != CardType.Number && cardToPlay.Type == topCard.Type)
        {
            return true;
        }

        // Si no cumple ninguna condición anterior, la jugada es ilegal
        return false;
    }
}