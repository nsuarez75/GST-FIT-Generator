using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using GstFitGenerator.Core;

namespace GstFitGenerator.App.ViewModels;

// Pantallas de techs: pantalla de inicio de cada referencia y de las reservas, nombres cortos de Fixtures y
// Assets, avisos del reparto y vista previa de cada pantalla.
public sealed partial class TechScreensViewModel : EditableViewModel
{
    public ObservableCollection<TechStartRowViewModel> Starts { get; } = [];
    public ObservableCollection<ShortNameRowViewModel> ShortNames { get; } = [];
    public ObservableCollection<string> Warnings { get; } = [];
    public ObservableCollection<string> Errors { get; } = [];
    public ObservableCollection<TechPageOption> Pages { get; } = [];

    [ObservableProperty]
    public partial TechPageOption? SelectedPage { get; set; }

    [ObservableProperty]
    public partial bool HasPages { get; set; }

    [ObservableProperty]
    public partial bool HasWarnings { get; set; }

    [ObservableProperty]
    public partial bool HasErrors { get; set; }

    public TechScreensViewModel(Session session) : base(session)
    {
        var project = session.Project;
        foreach (var reference in project.References.OrderBy(r => r.Number))
        {
            Starts.Add(new TechStartRowViewModel(session, L.T("Referencia {0}", reference.Number), reference.Number, Refresh));
        }
        Starts.Add(new TechStartRowViewModel(session, L.T("Reservas"), null, Refresh));

        // Solo los Fixtures y Assets que salen en las pantallas: los del primer grupo de cada feature con tecnología.
        var uses = project.References
            .SelectMany(r => Layout.Rows(project, r))
            .Where(r => r.Technology != 0 && !r.Feature.IsReserve && r.Feature.Fixtures.Count > 0)
            .Select(r => r.Feature.Fixtures[0])
            .ToList();
        foreach (var name in uses.Select(u => u.Fixture).Where(n => n.Length > 0).Distinct().Order(NaturalComparer.Instance))
        {
            ShortNames.Add(new ShortNameRowViewModel(session, "Fixture", name, Refresh));
        }
        foreach (var name in uses.Select(u => u.LayoutName).Where(n => n.Length > 0).Distinct().Order(NaturalComparer.Instance))
        {
            ShortNames.Add(new ShortNameRowViewModel(session, "Asset", name, Refresh));
        }
        Refresh();
    }

    // Vuelve a repartir y a dibujar, quedándose en la misma pantalla si sigue existiendo.
    public void Refresh()
    {
        var project = Session.Project;
        var plan = TechScreens.Plan(project);
        var selected = SelectedPage?.Page.Number;
        Pages.Clear();
        foreach (var page in plan.Pages)
        {
            Pages.Add(new TechPageOption(page, Core.Screen.Name(project.ScreenPrefix, page.Number), project.Screens[page.Number - 1].Description));
        }
        SelectedPage = Pages.FirstOrDefault(p => p.Page.Number == selected) ?? Pages.FirstOrDefault();
        HasPages = Pages.Count > 0;
        Warnings.Clear();
        foreach (var warning in plan.Warnings)
        {
            Warnings.Add(warning);
        }
        Errors.Clear();
        foreach (var error in plan.Errors)
        {
            Errors.Add(error);
        }
        HasWarnings = Warnings.Count > 0;
        HasErrors = Errors.Count > 0;
    }
}

// Pantalla de inicio de una referencia (Reference con valor) o de las reservas (Reference null).
public sealed class TechStartRowViewModel(Session session, string label, int? reference, Action changed) : EditableViewModel(session)
{
    public string Label { get; } = label;

    // Opción 0: no sale en pantallas; las demás, la pantalla 1…24.
    public IReadOnlyList<string> Options { get; } =
        [L.T("No"), .. Enumerable.Range(1, Core.Screen.Count).Select(n => n.ToString("00", CultureInfo.InvariantCulture))];

    public int SelectedIndex
    {
        get => (reference is int r
            ? Session.Project.TechScreens.ReferenceStarts.TryGetValue(r, out var start) ? start : (int?)null
            : Session.Project.TechScreens.ReservesStart) ?? 0;
        set
        {
            if (value < 0 || !Edit(SelectedIndex, value, Set))
            {
                return;
            }
            changed();
        }
    }

    private void Set(int index)
    {
        var settings = Session.Project.TechScreens;
        int? start = index == 0 ? null : index;
        if (reference is int r)
        {
            if (start is int s)
            {
                settings.ReferenceStarts[r] = s;
            }
            else
            {
                settings.ReferenceStarts.Remove(r);
            }
        }
        else
        {
            settings.ReservesStart = start;
        }
    }
}

public sealed class ShortNameRowViewModel(Session session, string kind, string name, Action changed) : EditableViewModel(session)
{
    public string Kind { get; } = kind;
    public string Name { get; } = name;

    public string ShortName
    {
        get => Session.Project.TechScreens.ShortNames.TryGetValue(Name, out var s) ? s : "";
        set
        {
            if (Edit(ShortName, value.Trim(), Set))
            {
                changed();
            }
        }
    }

    private void Set(string value)
    {
        if (value.Length > 0)
        {
            Session.Project.TechScreens.ShortNames[Name] = value;
        }
        else
        {
            Session.Project.TechScreens.ShortNames.Remove(Name);
        }
    }
}

public sealed record TechPageOption(TechScreenPage Page, string Name, string Description)
{
    public string Text => Description.Length > 0 ? $"{Name} · {Description}" : $"{Name} · {Page.DefaultDescription}";
}
