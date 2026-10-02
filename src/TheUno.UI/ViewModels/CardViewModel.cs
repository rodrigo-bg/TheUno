using System;
using System.Collections.Generic;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace TheUno.UI.ViewModels;

/// <summary>
/// Representación visual de una carta. Por ahora se identifica por el nombre de su
/// imagen en Assets/Cards_8bit (ej. "r_7", "b_skip", "wild_draw4"); cuando TheUno.Core
/// exponga el tipo Card, este ViewModel lo envolverá.
/// </summary>
public class CardViewModel : ViewModelBase
{
    private const string CardsFolder = "avares://TheUno.UI/Assets/Cards_8bit";

    private static readonly Dictionary<string, Bitmap> BitmapCache = new();

    public CardViewModel(string assetName)
    {
        AssetName = assetName;
    }

    public string AssetName { get; }

    /// <summary>Letra del color de la carta (r, g, b, y) o null si es comodín.</summary>
    public char? ColorCode => AssetName.StartsWith("wild") ? null : AssetName[0];

    public Bitmap Image => LoadBitmap(AssetName + ".png");

    public static Bitmap BackImage => LoadBitmap("back.png");

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
