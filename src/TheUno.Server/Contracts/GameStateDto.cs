using System;
using System.Collections.Generic;
using TheUno.Core.Models;

namespace TheUno.Server.Contracts;

/// <summary>
/// Estado completo de la mesa filtrado específicamente para un jugador.
/// El servidor construye y envía una instancia distinta a cada cliente conectado
/// para que cada uno reciba exclusivamente su propia mano.
/// </summary>
public class GameStateDto
{
    public string MyName { get; }
    public IReadOnlyList<CardDto> MyHand { get; }
    public IReadOnlyList<Guid> PlayableCardIds { get; }
    public IReadOnlyList<PlayerStateDto> Players { get; }
    public string CurrentPlayerName { get; }
    public CardDto TopCard { get; }
    public CardColor ActiveColor { get; }
    public int Direction { get; }
    public bool CanDraw { get; }
    public bool CanPass { get; }

    public GameStateDto(
        string myName,
        IReadOnlyList<CardDto> myHand,
        IReadOnlyList<Guid> playableCardIds,
        IReadOnlyList<PlayerStateDto> players,
        string currentPlayerName,
        CardDto topCard,
        CardColor activeColor,
        int direction,
        bool canDraw,
        bool canPass)
    {
        MyName = myName ?? throw new ArgumentNullException(nameof(myName));
        MyHand = myHand ?? throw new ArgumentNullException(nameof(myHand));
        PlayableCardIds = playableCardIds ?? throw new ArgumentNullException(nameof(playableCardIds));
        Players = players ?? throw new ArgumentNullException(nameof(players));
        CurrentPlayerName = currentPlayerName ?? throw new ArgumentNullException(nameof(currentPlayerName));
        TopCard = topCard ?? throw new ArgumentNullException(nameof(topCard));
        ActiveColor = activeColor;
        Direction = direction;
        CanDraw = canDraw;
        CanPass = canPass;
    }
}
