using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using GstFitGenerator.App.ViewModels;

namespace GstFitGenerator.App.Views;

public partial class ReferenceView : UserControl
{
    // Margen de arriba y abajo de la lista en el que, al arrastrar, la lista se desplaza sola.
    private const double ScrollEdge = 32;

    // Fila que se está arrastrando por su asa, su contenedor (atenuado) y la última posición del puntero en la lista.
    private FeatureRowViewModel? _dragged;
    private Control? _draggedContainer;
    private Point _dragPoint;
    private readonly DispatcherTimer _autoScroll;

    public ReferenceView()
    {
        InitializeComponent();
        _autoScroll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
        _autoScroll.Tick += (_, _) => AutoScroll();
    }

    private ReferenceViewModel? ViewModel => DataContext as ReferenceViewModel;

    private void OnGripPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control { DataContext: FeatureRowViewModel row } grip && e.GetCurrentPoint(grip).Properties.IsLeftButtonPressed)
        {
            _dragged = row;
            if (ViewModel is { } vm)
            {
                vm.Selected = row;
            }
            _draggedContainer = Rows.ContainerFromItem(row);
            _draggedContainer?.Classes.Add("dragging");
            _dragPoint = e.GetPosition(Rows);
            e.Pointer.Capture(grip);
            _autoScroll.Start();
            e.Handled = true;
        }
    }

    private void OnGripMoved(object? sender, PointerEventArgs e)
    {
        if (_dragged is null)
        {
            return;
        }
        _dragPoint = e.GetPosition(Rows);
        ShowDropTarget();
    }

    // Al soltar, la fila arrastrada ocupa el sitio de la que queda debajo del puntero (si es de su misma tecnología).
    private void OnGripReleased(object? sender, PointerReleasedEventArgs e)
    {
        var dragged = _dragged;
        EndDrag();
        e.Pointer.Capture(null);
        if (dragged is null || ViewModel is not { } vm)
        {
            return;
        }
        var target = RowAt(e.GetPosition(Rows));
        if (target is not null)
        {
            vm.Drop(dragged, target);
        }
        e.Handled = true;
    }

    private void OnGripCaptureLost(object? sender, PointerCaptureLostEventArgs e) => EndDrag();

    private void EndDrag()
    {
        _autoScroll.Stop();
        _draggedContainer?.Classes.Remove("dragging");
        _draggedContainer = null;
        _dragged = null;
        DropLine.IsVisible = false;
        DropLabel.IsVisible = false;
    }

    // Línea azul en el borde de la fila de destino: debajo si la fila baja, encima si sube. Junto al puntero, el offset nuevo.
    private void ShowDropTarget()
    {
        var target = _dragged is null ? null : RowAt(_dragPoint);
        if (_dragged is null || target is null || !ReferenceViewModel.CanDrop(_dragged, target)
            || Rows.ContainerFromItem(target) is not { } container)
        {
            DropLine.IsVisible = false;
            DropLabel.IsVisible = false;
            return;
        }
        var below = target.Row.Pointer > _dragged.Row.Pointer;
        if (container.TranslatePoint(new Point(0, below ? container.Bounds.Height : 0), DropLayer) is not { } edge)
        {
            return;
        }
        Canvas.SetLeft(DropLine, edge.X);
        Canvas.SetTop(DropLine, edge.Y - DropLine.Height / 2);
        DropLine.Width = container.Bounds.Width;
        DropLine.IsVisible = true;

        DropText.Text = $"{_dragged.Offset} → {target.Offset}";
        DropLabel.IsVisible = true;
        DropLabel.Measure(Size.Infinity);
        var pointer = Rows.TranslatePoint(_dragPoint, DropLayer) ?? edge;
        Canvas.SetLeft(DropLabel, Math.Max(0, pointer.X + 18));
        Canvas.SetTop(DropLabel, edge.Y - DropLabel.DesiredSize.Height / 2);
    }

    // Mientras se arrastra cerca del borde de arriba o de abajo, la lista se desplaza sola.
    private void AutoScroll()
    {
        if (_dragged is null || Rows.FindDescendantOfType<ScrollViewer>() is not { } scroll)
        {
            return;
        }
        var step = _dragPoint.Y < ScrollEdge ? -(ScrollEdge - _dragPoint.Y) / 2
            : _dragPoint.Y > Rows.Bounds.Height - ScrollEdge ? (_dragPoint.Y - Rows.Bounds.Height + ScrollEdge) / 2
            : 0;
        if (step != 0)
        {
            scroll.Offset = scroll.Offset.WithY(Math.Clamp(scroll.Offset.Y + step, 0, Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height)));
            ShowDropTarget();
        }
    }

    private FeatureRowViewModel? RowAt(Point point)
    {
        var hit = Rows.InputHitTest(point) as Visual;
        return hit?.GetSelfAndVisualAncestors().OfType<Control>().Select(c => c.DataContext).OfType<FeatureRowViewModel>().FirstOrDefault();
    }

    private void OnRowDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (ViewModel is { } vm)
        {
            vm.ShowForm = true;
        }
    }
}
