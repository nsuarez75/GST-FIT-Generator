using Avalonia.Controls;
using GstFitGenerator.App.ViewModels;

namespace GstFitGenerator.App.Views;

public partial class TechScreensView : UserControl
{
    public TechScreensView()
    {
        InitializeComponent();
        // La vista previa avisa de clics y arrastres; el modelo de vista hace el retoque.
        Preview.BoxClicked += box => (DataContext as TechScreensViewModel)?.OnBoxClicked(box);
        Preview.BoxDragged += (box, left, top) => (DataContext as TechScreensViewModel)?.OnBoxDragged(box, left, top);
    }
}
