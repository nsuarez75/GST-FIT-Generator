using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using GstFitGenerator.Core;

namespace GstFitGenerator.App.Controls;

// Vista previa de una pantalla de techs a tamaño real (1920x966): se mete en un Viewbox para que quepa. Dibuja lo
// mismo que se escribe en el XML: recuadros, títulos, textos y un cuadrado por faceplate, con el estilo de la
// pantalla. Los colores son los del HMI, no los del tema de la app.
public sealed class TechScreenPreview : Control
{
    public const int ScreenWidth = 1920;
    public const int ScreenHeight = 966;
    private const int HeaderHeight = 86;

    public static readonly StyledProperty<TechScreenPage?> PageProperty =
        AvaloniaProperty.Register<TechScreenPreview, TechScreenPage?>(nameof(Page));

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<TechScreenPreview, string>(nameof(Title), "");

    static TechScreenPreview() => AffectsRender<TechScreenPreview>(PageProperty, TitleProperty);

    public TechScreenPage? Page
    {
        get => GetValue(PageProperty);
        set => SetValue(PageProperty, value);
    }

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    private static readonly IBrush Background = Brushes.White;
    private static readonly IBrush Header = new SolidColorBrush(Color.FromRgb(245, 246, 248));
    private static readonly IBrush HeaderText = new SolidColorBrush(Color.FromRgb(35, 45, 60));
    private static readonly IPen Line = new Pen(new SolidColorBrush(Color.FromRgb(120, 125, 135)), 2);
    private static readonly IBrush Faceplate = new SolidColorBrush(Color.FromRgb(150, 200, 160));
    private static readonly IPen FaceplateBorder = new Pen(new SolidColorBrush(Color.FromRgb(70, 110, 80)), 1);
    private static readonly Typeface Font = new("Tahoma", FontStyle.Normal, FontWeight.Bold);

    protected override Size MeasureOverride(Size availableSize) => new(ScreenWidth, ScreenHeight);

    public override void Render(DrawingContext context)
    {
        context.FillRectangle(Background, new Rect(0, 0, ScreenWidth, ScreenHeight));
        context.FillRectangle(Header, new Rect(0, 0, ScreenWidth, HeaderHeight));
        context.DrawLine(Line, new Point(0, HeaderHeight), new Point(ScreenWidth, HeaderHeight));
        DrawText(context, Title, 26, HeaderText, new Rect(585, 3, 750, 80), center: true);
        if (Page is not { } page)
        {
            return;
        }
        var s = page.Style;
        var boxBack = Brush(s.BoxBack);
        var boxBorder = new Pen(Brush(s.BoxBorder), 1);
        var titleBack = Brush(s.TitleBack);
        var titleFore = Brush(s.TitleFore);
        var labelFore = Brush(s.LabelFore);
        foreach (var box in page.Boxes)
        {
            context.DrawRectangle(boxBack, boxBorder, new Rect(box.Left, box.Top, box.Width, box.Height));
            var title = new Rect(box.Left, box.Top, box.Width, s.TitleHeight);
            context.FillRectangle(titleBack, title);
            DrawText(context, box.Title, s.TitleFontSize, titleFore, title, center: true);
            foreach (var row in box.Rows)
            {
                var label = new Rect(row.Left, row.Top + (TechScreens.FaceplateHeight - s.LabelHeight) / 2.0, row.LabelWidth, s.LabelHeight);
                DrawText(context, row.Text, s.LabelFontSize, labelFore, label, center: false);
                var faceplate = new Rect(row.FaceplateLeft, row.Top, TechScreens.FaceplateWidth, TechScreens.FaceplateHeight);
                context.DrawRectangle(Faceplate, FaceplateBorder, faceplate, 4, 4);
            }
        }
    }

    private static SolidColorBrush Brush(string hex)
    {
        var (r, g, b) = TechScreenColors.Parse(hex);
        return new SolidColorBrush(Color.FromRgb(r, g, b));
    }

    private static void DrawText(DrawingContext context, string text, double size, IBrush brush, Rect area, bool center)
    {
        var formatted = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Font, size, brush)
        {
            MaxTextWidth = Math.Max(1, area.Width - 4),
            MaxLineCount = 1,
            Trimming = TextTrimming.CharacterEllipsis,
        };
        var x = center ? area.X + (area.Width - formatted.Width) / 2 : area.X + 3;
        var y = area.Y + (area.Height - formatted.Height) / 2;
        context.DrawText(formatted, new Point(x, y));
    }
}
