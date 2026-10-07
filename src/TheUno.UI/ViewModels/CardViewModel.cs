using System;
using System.Collections.Generic;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using TheUno.Core.Models;

namespace TheUno.UI.ViewModels;

/// <summary>
/// Representación visual de una carta del Core. Traduce su color y tipo al nombre
/// de su imagen en Assets/Cards_8bit (ej. "r_7", "b_skip", "wild_draw4").
/// </summary>
public class CardViewModel : ViewModelBase
{
    private const string CardsFolder = "avares://TheUno.UI/Assets/Cards_8bit";

    private static readonly Dictionary<string, Bitmap> BitmapCache = new();

    public CardViewModel(Card card)
    {
        Card = card;
        AssetName = GetAssetName(card);
    }

    public Card Card { get; }

    public string AssetName { get; }

    /// <summary>Letra del color de la carta (r, g, b, y) o null si es comodín.</summary>
    public char? ColorCode => GetColorCode(Card.Color);

    public Bitmap Image => LoadBitmap(AssetName + ".png");

    public static Bitmap BackImage => LoadBitmap("back.png");

    public static char? GetColorCode(CardColor color) => color switch
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

        var color = GetColorCode(card.Color)
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
