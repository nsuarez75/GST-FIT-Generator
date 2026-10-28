using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using GstFitGenerator.Core;

namespace GstFitGenerator.App.Controls;

// Vista previa de una pantalla de techs a tamaño real (1920x966): se mete en un Viewbox para que quepa. Dibuja lo
// mismo que se escribe en el XML: recuadros, títulos, textos y un cuadrado por faceplate, con el estilo de la
// pantalla, y el lienzo con una línea discontinua. Los colores son los del HMI, no los del tema de la app.
// Un clic elige un recuadro (BoxClicked) y arrastrarlo lo mueve (BoxDragged, con la posición nueva).
public sealed class TechScreenPreview : Control
{
    public const int ScreenWidth = TechScreenStyle.ScreenWidth;
    public const int ScreenHeight = TechScreenStyle.ScreenHeight;
    private const int HeaderHeight = 86;
    // Lo que hay que mover el ratón para que cuente como arrastre y no como clic.
    private const double DragThreshold = 4;

    public static readonly StyledProperty<TechScreenPage?> PageProperty =
        AvaloniaProperty.Register<TechScreenPreview, TechScreenPage?>(nameof(Page));

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<TechScreenPreview, string>(nameof(Title), "");

    public static readonly StyledProperty<TechScreenBox?> SelectedBoxProperty =
        AvaloniaProperty.Register<TechScreenPreview, TechScreenBox?>(nameof(SelectedBox));

    static TechScreenPreview() => AffectsRender<TechScreenPreview>(PageProperty, TitleProperty, SelectedBoxProperty);

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

    public TechScreenBox? SelectedBox
    {
        get => GetValue(SelectedBoxProperty);
        set => SetValue(SelectedBoxProperty, value);
    }

    public event Action<TechScreenBox?>? BoxClicked;
    public event Action<TechScreenBox, int, int>? BoxDragged;

    private TechScreenBox? _dragging;
    private Point _dragStart;
    private Vector _dragOffset;

    private static readonly IBrush Background = Brushes.White;
    private static readonly IBrush Header = new SolidColorBrush(Color.FromRgb(245, 246, 248));
    private static readonly IBrush HeaderText = new SolidColorBrush(Color.FromRgb(35, 45, 60));
    private static readonly IPen Line = new Pen(new SolidColorBrush(Color.FromRgb(120, 125, 135)), 2);
    private static readonly IPen Canvas = new Pen(new SolidColorBrush(Color.FromRgb(160, 170, 185)), 1.5, new DashStyle([6, 4], 0));
    private static readonly IPen Selection = new Pen(new SolidColorBrush(Color.FromRgb(37, 99, 235)), 4);
    private static readonly IPen Ghost = new Pen(new SolidColorBrush(Color.FromRgb(37, 99, 235)), 3, new DashStyle([8, 5], 0));
    private static readonly IBrush Faceplate = new SolidColorBrush(Color.FromRgb(150, 200, 160));
    private static readonly IPen FaceplateBorder = new Pen(new SolidColorBrush(Color.FromRgb(70, 110, 80)), 1);
    private static readonly Typeface Font = new("Tahoma", FontStyle.Normal, FontWeight.Bold);

    public TechScreenPreview() => Cursor = new Cursor(StandardCursorType.Arrow);

    protected override Size MeasureOverride(Size availableSize) => new(ScreenWidth, ScreenHeight);

    // El de arriba (el último dibujado) gana si se pisan.
    private TechScreenBox? HitTest(Point p) =>
        Page?.Boxes.LastOrDefault(b => p.X >= b.Left && p.X <= b.Right && p.Y >= b.Top && p.Y <= b.Bottom);

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }
        var p = e.GetPosition(this);
        var box = HitTest(p);
        BoxClicked?.Invoke(box);
        if (box is not null)
        {
            _dragging = box;
            _dragStart = p;
            _dragOffset = default;
            e.Pointer.Capture(this);
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var p = e.GetPosition(this);
        if (_dragging is not null)
        {
            _dragOffset = p - _dragStart;
            InvalidateVisual();
        }
        else
        {
            Cursor = new Cursor(HitTest(p) is null ? StandardCursorType.Arrow : StandardCursorType.SizeAll);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_dragging is not { } box)
        {
            return;
        }
        var offset = _dragOffset;
        _dragging = null;
        _dragOffset = default;
        e.Pointer.Capture(null);
        InvalidateVisual();
        if (Math.Abs(offset.X) >= DragThreshold || Math.Abs(offset.Y) >= DragThreshold)
        {
            BoxDragged?.Invoke(box, box.Left + (int)Math.Round(offset.X), box.Top + (int)Math.Round(offset.Y));
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _dragging = null;
        _dragOffset = default;
        InvalidateVisual();
    }

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
        context.DrawRectangle(null, Canvas, new Rect(s.AreaLeft, s.AreaTop, s.AreaRight - s.AreaLeft, s.AreaBottom - s.AreaTop));
        var boxBack = Brush(s.BoxBack);
        var boxBorder = new Pen(Brush(s.BoxBorder), 1);
        var titleBack = Brush(s.TitleBack);
        var titleFore = Brush(s.TitleFore);
        var labelFore = Brush(s.LabelFore);
        foreach (var box in page.Boxes)
        {
            var rect = new Rect(box.Left, box.Top, box.Width, box.Height);
            context.DrawRectangle(boxBack, boxBorder, rect);
            var title = new Rect(box.Left, box.Top, box.Width, s.TitleHeight);
            context.FillRectangle(titleBack, title);
            DrawText(context, box.Title, s.TitleFontSize, titleFore, title, center: true);
            foreach (var row in box.Rows)
            {
                var label = new Rect(row.Left, row.Top + (s.FaceplateHeight - s.LabelHeight) / 2.0, row.LabelWidth, s.LabelHeight);
                DrawText(context, row.Text, s.LabelFontSize, labelFore, label, center: false);
                var faceplate = new Rect(row.FaceplateLeft, row.Top, s.FaceplateWidth, s.FaceplateHeight);
                context.DrawRectangle(Faceplate, FaceplateBorder, faceplate, 4, 4);
            }
            if (SelectedBox is { } selected && Same(selected, box))
            {
                context.DrawRectangle(null, Selection, rect.Inflate(2));
            }
            if (_dragging is { } dragging && Same(dragging, box))
            {
                var snapped = new Point(TechScreenEdits.Snap(box.Left + (int)_dragOffset.X), TechScreenEdits.Snap(box.Top + (int)_dragOffset.Y));
                context.DrawRectangle(null, Ghost, new Rect(snapped, rect.Size));
            }
        }
    }

    private static bool Same(TechScreenBox a, TechScreenBox b) => a.Key == b.Key && a.Part == b.Part;

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
