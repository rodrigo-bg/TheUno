using System;
using System.Collections.Generic;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using TheUno.Core.Models;

namespace TheUno.UI.ViewModels;

/// <summary>
/// Representación visual de una carta del Core. Traduce su color y tipo al nombre
/// de su imagen en Assets/Cards_8bit (ej. "r_7", "b_skip", "wild_draw4").
/// </summary>
public partial class CardViewModel : ViewModelBase
{
    private const string CardsFolder = "avares://TheUno.UI/Assets/Cards_8bit";

    private static readonly Dictionary<string, Bitmap> BitmapCache = new();

    /// <summary>La carta es del jugador en turno y el motor permite tirarla: se resalta en la mano.</summary>
    [ObservableProperty]
    private bool _isPlayable;

    /// <summary>
    /// Carta boca abajo de un rival que se asoma hacia el centro mientras él "elige" en su turno.
    /// Es solo animación: no tiene que ver con la carta que va a tirar.
    /// </summary>
    [ObservableProperty]
    private bool _isPicking;

    public CardViewModel(Card card)
    {
        Card = card;
        AssetName = GetAssetName(card);
    }

    private CardViewModel(Card card, string assetName)
    {
        Card = card;
        AssetName = assetName;
    }

    /// <summary>
    /// Carta boca abajo de un rival. No representa ninguna carta real: el cliente nunca recibe
    /// las manos de los demás, solo cuántas cartas tienen.
    /// </summary>
    public static CardViewModel CreateBack() =>
        new(new Card(Guid.Empty, CardColor.None, CardType.Wild), "back");

    public Card Card { get; }

    public string AssetName { get; }

    public Bitmap Image => LoadBitmap(AssetName + ".png");

    public static Bitmap BackImage => LoadBitmap("back.png");

    // Las imágenes empiezan con la letra de su color: r_7.png, g_skip.png, b_draw2.png...
    private static char? GetColorPrefix(CardColor color) => color switch
    {
        CardColor.Red => 'r',
        CardColor.Green => 'g',
        CardColor.Blue => 'b',
        CardColor.Yellow => 'y',
        _ => null
    };

    private static string GetAssetName(Card card)
    {
        if (card.Type == CardType.Wild) return "wild";
        if (card.Type == CardType.WildDrawFour) return "wild_draw4";

        var color = GetColorPrefix(card.Color)
                    ?? throw new ArgumentException($"La carta {card.DisplayText} necesita un color.", nameof(card));

        var value = card.Type switch
        {
            CardType.Number when card.Number is >= 0 and <= 9 => card.Number.Value.ToString(),
            CardType.Skip => "skip",
            CardType.Reverse => "reverse",
            CardType.DrawTwo => "draw2",
            _ => throw new ArgumentException($"La carta {card.DisplayText} no tiene imagen.", nameof(card))
        };

        return $"{color}_{value}";
    }

    private static Bitmap LoadBitmap(string fileName)
    {
        if (!BitmapCache.TryGetValue(fileName, out var bitmap))
        {
            var uri = new Uri($"{CardsFolder}/{fileName}");
            bitmap = new Bitmap(AssetLoader.Open(uri));
            BitmapCache[fileName] = bitmap;
        }

        return bitmap;
    }
}
