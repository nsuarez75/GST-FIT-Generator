using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using GstFitGenerator.App.ViewModels;

namespace GstFitGenerator.App.Views;

public partial class ReferenceView : UserControl
{
    // Fila que se está arrastrando por su asa.
    private FeatureRowViewModel? _dragged;

    public ReferenceView() => InitializeComponent();

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
            e.Pointer.Capture(grip);
            e.Handled = true;
        }
    }

    // Al soltar, la fila arrastrada ocupa el sitio de la que queda debajo del puntero (si es de su misma tecnología).
    private void OnGripReleased(object? sender, PointerReleasedEventArgs e)
    {
        e.Pointer.Capture(null);
        var dragged = _dragged;
        _dragged = null;
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
