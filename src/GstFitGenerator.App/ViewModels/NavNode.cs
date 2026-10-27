using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GstFitGenerator.App.ViewModels;

public enum NavKind
{
    Project,
    Technologies,
    Locations,
    References,
    Reference,
    Auxiliaries,
    Screens,
    Versions,
    Sizes,
}

// Nodo del árbol del proyecto (panel izquierdo). Reference lleva el número de la referencia.
public sealed partial class NavNode(NavKind kind, int reference = 0) : ObservableObject
{
    public NavKind Kind { get; } = kind;
    public int Reference { get; } = reference;
    public ObservableCollection<NavNode> Children { get; } = [];

    [ObservableProperty]
    public partial string Title { get; set; } = "";

    [ObservableProperty]
    public partial string Detail { get; set; } = "";

    [ObservableProperty]
    public partial bool IsExpanded { get; set; } = true;

    // Icono del nodo (ver Controls/Icon.cs).
    public string Icon => Kind switch
    {
        NavKind.Project => "box",
        NavKind.Technologies => "tag",
        NavKind.Locations => "map-pin",
        NavKind.References => "layers",
        NavKind.Reference => "list",
        NavKind.Auxiliaries => "users",
        NavKind.Screens => "monitor",
        NavKind.Versions => "history",
        _ => "ruler",
    };

    public bool Matches(NavKind kind, int reference) => Kind == kind && Reference == reference;
}
