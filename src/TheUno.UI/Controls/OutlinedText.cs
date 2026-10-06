using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace TheUno.UI.Controls;

/// <summary>
/// Texto con relleno, contorno y sombra dura. TextBlock no permite contorno,
/// así que el texto se convierte en geometría y se dibuja a mano.
/// </summary>
public class OutlinedText : Control
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<OutlinedText, string>(nameof(Text), string.Empty);

    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<OutlinedText, double>(nameof(FontSize), 48);

    public static readonly StyledProperty<IBrush> FillProperty =
        AvaloniaProperty.Register<OutlinedText, IBrush>(nameof(Fill), Brushes.White);

    public static readonly StyledProperty<IBrush> StrokeProperty =
        AvaloniaProperty.Register<OutlinedText, IBrush>(nameof(Stroke), Brushes.Black);

    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<OutlinedText, double>(nameof(StrokeThickness), 4);

    public static readonly StyledProperty<double> ShadowOffsetProperty =
        AvaloniaProperty.Register<OutlinedText, double>(nameof(ShadowOffset), 0);

    static OutlinedText()
    {
        AffectsMeasure<OutlinedText>(TextProperty, FontSizeProperty, StrokeThicknessProperty, ShadowOffsetProperty);
        AffectsRender<OutlinedText>(FillProperty, StrokeProperty);
    }

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public IBrush Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public IBrush Stroke
    {
        get => GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public double StrokeThickness
    {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    /// <summary>Desplazamiento de la sombra negra (0 = sin sombra).</summary>
    public double ShadowOffset
    {
        get => GetValue(ShadowOffsetProperty);
        set => SetValue(ShadowOffsetProperty, value);
    }

    private FormattedText CreateFormattedText() =>
        new(Text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(TextElement.GetFontFamily(this)),
            FontSize,
            Fill);

    protected override Size MeasureOverride(Size availableSize)
    {
        var text = CreateFormattedText();
        var extra = StrokeThickness * 2 + ShadowOffset;
        return new Size(text.Width + extra, text.Height + extra);
    }

    public override void Render(DrawingContext context)
    {
        var origin = new Point(StrokeThickness, StrokeThickness);
        var geometry = CreateFormattedText().BuildGeometry(origin);
        if (geometry is null)
        {
            return;
        }

        // El contorno se dibuja al doble de grosor y el relleno encima, así el borde queda por fuera.
        var outline = new Pen(Stroke, StrokeThickness * 2, lineJoin: PenLineJoin.Miter);

        if (ShadowOffset > 0)
        {
            using (context.PushTransform(Matrix.CreateTranslation(ShadowOffset, ShadowOffset)))
            {
                context.DrawGeometry(Brushes.Black, new Pen(Brushes.Black, StrokeThickness * 2), geometry);
            }
        }

        context.DrawGeometry(null, outline, geometry);
        context.DrawGeometry(Fill, null, geometry);
    }
}
