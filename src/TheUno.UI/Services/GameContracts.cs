using System;
using System.Collections.Generic;
using TheUno.Core.Models;

namespace TheUno.UI.Services;

/// <summary>Métodos del GameHub que invoca el cliente. Deben llamarse igual que en el servidor.</summary>
public static class HubMethods
{
    public const string CreateGame = "CreateGame";
    public const string GetTakenPlayers = "GetTakenPlayers";
    public const string ChoosePlayer = "ChoosePlayer";
    public const string StartGame = "StartGame";
    public const string PlayCard = "PlayCard";
    public const string DrawCard = "DrawCard";
    public const string PassTurn = "PassTurn";
    public const string CallUno = "CallUno";
    public const string LeaveGame = "LeaveGame";
}

/// <summary>Mensajes que manda el servidor a los clientes.</summary>
public static class HubMessages
{
    public const string PlayersChanged = "PlayersChanged";
    public const string GameStarted = "GameStarted";
    public const string StateChanged = "StateChanged";
    public const string UnoWindowOpened = "UnoWindowOpened";
    public const string UnoPenalty = "UnoPenalty";
    public const string GameOver = "GameOver";
    public const string PlayerLeft = "PlayerLeft";
}

/// <summary>Una carta tal como viaja por la red.</summary>
public record CardDto(Guid Id, CardColor Color, CardType Type, int? Number)
{
    public Card ToCard() => new(Id, Color, Type, Number);
}

/// <summary>Lo que cualquiera puede saber de un jugador: nunca sus cartas, solo cuántas tiene.</summary>
public record PlayerStateDto(string Name, int CardCount, bool HasCalledUno);

/// <summary>
/// La mesa vista por un jugador. El servidor manda uno distinto a cada cliente:
/// la mano completa solo viaja a su dueño.
/// </summary>
public record GameStateDto(
    string MyName,
    List<CardDto> MyHand,
    List<Guid> PlayableCardIds,
    List<PlayerStateDto> Players,
    string CurrentPlayerName,
    CardDto TopCard,
    CardColor ActiveColor,
    int Direction,
    bool CanDraw,
    bool CanPass);
