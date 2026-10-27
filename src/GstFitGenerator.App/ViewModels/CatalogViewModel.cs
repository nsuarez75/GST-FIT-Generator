using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GstFitGenerator.Core;

namespace GstFitGenerator.App.ViewModels;

// Catálogo de tipos de feature, común a todos los proyectos: número, proceso y detalle.
// La tecnología de cada tipo no está aquí: es de cada proyecto (ver Tecnologías).
public sealed partial class CatalogViewModel : EditableViewModel
{
    private readonly IDialogs _dialogs;

    public ObservableCollection<FeatureTypeRowViewModel> Rows { get; } = [];

    [ObservableProperty]
    public partial FeatureTypeRowViewModel? Selected { get; set; }

    public CatalogViewModel(Session session, IDialogs dialogs) : base(session)
    {
        _dialogs = dialogs;
        Load();
    }

    private void Load()
    {
        Rows.Clear();
        foreach (var type in Session.Catalog.Types.OrderBy(t => t.Number))
        {
            Rows.Add(new FeatureTypeRowViewModel(Session, type));
        }
    }

    [RelayCommand]
    private void Add()
    {
        var number = Session.Catalog.Types.Select(t => t.Number).DefaultIfEmpty(0).Max() + 1;
        var type = new FeatureType { Number = number, Process = L.T("Proceso nuevo") };
        Session.Change("add-type", () => Session.Catalog.Types.Add(type));
        var row = new FeatureTypeRowViewModel(Session, type);
        Rows.Add(row);
        Selected = row;
    }

    [RelayCommand]
    private async Task Remove(FeatureTypeRowViewModel? row)
    {
        row ??= Selected;
        if (row is null)
        {
            return;
        }
        var uses = Session.Project.References.Sum(r => r.Features.Count(f => f.Type == row.Type.Number));
        if (uses > 0 && !await _dialogs.Confirm(L.T("Quitar tipo"),
                L.T("El tipo {0} lo usan {1} features de este proyecto (y quizá de otros). ¿Quitarlo del catálogo?", row.Type.Number, uses), L.T("Quitar")))
        {
            return;
        }
        Session.Change("remove-type", () => Session.Catalog.Types.Remove(row.Type));
        Rows.Remove(row);
    }

    [RelayCommand]
    private async Task Export()
    {
        var path = await _dialogs.SaveFile(L.T("Exportar catálogo"), MainViewModel.CatalogFilter, "catalogo" + Storage.CatalogExtension);
        if (path is null)
        {
            return;
        }
        try
        {
            Storage.SaveCatalog(Session.Catalog, path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            await _dialogs.Message(L.T("Exportar catálogo"), L.T("No se pudo guardar: {0}", e.Message));
        }
    }

    [RelayCommand]
    private async Task Import()
    {
        var path = await _dialogs.OpenFile(L.T("Importar catálogo"), MainViewModel.CatalogFilter);
        if (path is null)
        {
            return;
        }
        try
        {
            Session.ReplaceCatalog(Storage.LoadCatalog(path));
            Load();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            await _dialogs.Message(L.T("Importar catálogo"), L.T("No se pudo abrir {0}: {1}", Path.GetFileName(path), e.Message));
        }
    }
}

public sealed class FeatureTypeRowViewModel(Session session, FeatureType type) : EditableViewModel(session)
{
    public FeatureType Type { get; } = type;

    // Cambiar el número cambia también el de las features y la tecnología asignada de este proyecto.
    public string Number
    {
        get => Type.Number.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (!int.TryParse(value, out var n) || n < 0 || Session.Catalog.Types.Any(t => t != Type && t.Number == n))
            {
                OnPropertyChanged();
                return;
            }
            var old = Type.Number;
            Edit(old, n, v =>
            {
                Type.Number = v;
                foreach (var f in Session.Project.References.SelectMany(r => r.Features).Where(f => f.Type == old))
                {
                    f.Type = v;
                }
                if (Session.Project.TypeTechnologies.Remove(old, out var tech))
                {
                    Session.Project.TypeTechnologies[v] = tech;
                }
            });
        }
    }

    public string Process
    {
        get => Type.Process;
        set => Edit(Type.Process, value.Trim(), v => Type.Process = v);
    }

    public string Detail
    {
        get => Type.Detail;
        set => Edit(Type.Detail, value.Trim(), v => Type.Detail = v);
    }
}
