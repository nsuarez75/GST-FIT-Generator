// Editores de listas sencillas del proyecto: locations, auxiliares, Screen Names y control de versiones.
using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GstFitGenerator.Core;

namespace GstFitGenerator.App.ViewModels;

// ---------------------------------------------------------------- locations

public sealed partial class LocationsViewModel : EditableViewModel
{
    public ObservableCollection<LocationRowViewModel> Rows { get; } = [];

    [ObservableProperty]
    public partial LocationRowViewModel? Selected { get; set; }

    public LocationsViewModel(Session session) : base(session)
    {
        foreach (var location in session.Project.Locations)
        {
            Rows.Add(new LocationRowViewModel(session, location));
        }
    }

    [RelayCommand]
    private void Add()
    {
        var number = Session.Project.Locations.Select(l => l.Number).DefaultIfEmpty(0).Max() + 1;
        var location = new Location { Name = $"Location {number}", Number = number };
        Session.Change("add-location", () => Session.Project.Locations.Add(location));
        var row = new LocationRowViewModel(Session, location);
        Rows.Add(row);
        Selected = row;
    }

    [RelayCommand]
    private void Remove(LocationRowViewModel? row)
    {
        row ??= Selected;
        if (row is null)
        {
            return;
        }
        Session.Change("remove-location", () => Session.Project.Locations.Remove(row.Location));
        Rows.Remove(row);
    }
}

public sealed class LocationRowViewModel(Session session, Location location) : EditableViewModel(session)
{
    public Location Location { get; } = location;

    public string Name
    {
        get => Location.Name;
        set => Edit(Location.Name, value.Trim(), v => Location.Name = v);
    }

    // Las features guardan el número: cambiarlo aquí cambia también el de las features que lo usan.
    public string Number
    {
        get => Location.Number.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (!int.TryParse(value, out var n) || n < 0 || Session.Project.Locations.Any(l => l != Location && l.Number == n))
            {
                OnPropertyChanged();
                return;
            }
            var old = Location.Number;
            Edit(old, n, v =>
            {
                Location.Number = v;
                foreach (var f in Session.Project.References.SelectMany(r => r.Features).Where(f => f.Location == old))
                {
                    f.Location = v;
                }
            });
        }
    }

    public int Uses => Session.Project.References.Sum(r => r.Features.Count(f => f.Location == Location.Number));
}

// ---------------------------------------------------------------- auxiliares

public sealed partial class AuxiliariesViewModel : EditableViewModel
{
    public ObservableCollection<AuxRowViewModel> Rows { get; } = [];

    [ObservableProperty]
    public partial AuxRowViewModel? Selected { get; set; }

    public AuxiliariesViewModel(Session session) : base(session)
    {
        foreach (var aux in session.Project.Auxiliaries)
        {
            Rows.Add(new AuxRowViewModel(session, aux));
        }
    }

    [RelayCommand]
    private void Add()
    {
        var aux = new AuxFeature { Working = "1" };
        var index = Selected is null ? Session.Project.Auxiliaries.Count : Session.Project.Auxiliaries.IndexOf(Selected.Aux) + 1;
        Session.Change("add-aux", () => Session.Project.Auxiliaries.Insert(index, aux));
        var row = new AuxRowViewModel(Session, aux);
        Rows.Insert(index, row);
        Selected = row;
    }

    [RelayCommand]
    private void Remove(AuxRowViewModel? row)
    {
        row ??= Selected;
        if (row is null)
        {
            return;
        }
        Session.Change("remove-aux", () => Session.Project.Auxiliaries.Remove(row.Aux));
        Rows.Remove(row);
    }
}

public sealed class AuxRowViewModel(Session session, AuxFeature aux) : EditableViewModel(session)
{
    public AuxFeature Aux { get; } = aux;

    public string Asset
    {
        get => Aux.Asset;
        set => Edit(Aux.Asset, value.Trim(), v => Aux.Asset = v);
    }

    public string Working
    {
        get => Aux.Working;
        set => Edit(Aux.Working, value.Trim(), v => Aux.Working = v);
    }

    public string FeatureId
    {
        get => Text(Aux.FeatureId);
        set
        {
            if (TryNumber(value, out var n))
            {
                Edit(Aux.FeatureId, n, v => Aux.FeatureId = v);
            }
            else
            {
                OnPropertyChanged();
            }
        }
    }
}

// ---------------------------------------------------------------- Screen Names

public sealed class ScreensViewModel : EditableViewModel
{
    public ObservableCollection<ScreenRowViewModel> Rows { get; } = [];

    public ScreensViewModel(Session session) : base(session)
    {
        var screens = session.Project.Screens;
        while (screens.Count < Screen.Count)
        {
            screens.Add(new Screen());
        }
        for (var i = 0; i < Screen.Count; i++)
        {
            Rows.Add(new ScreenRowViewModel(session, screens[i], i + 1));
        }
    }

    public string Prefix
    {
        get => Session.Project.ScreenPrefix;
        set
        {
            if (Edit(Session.Project.ScreenPrefix, value.Trim(), v => Session.Project.ScreenPrefix = v))
            {
                foreach (var row in Rows)
                {
                    row.RefreshName();
                }
            }
        }
    }
}

public sealed class ScreenRowViewModel(Session session, Screen screen, int number) : EditableViewModel(session)
{
    public string Name => Screen.Name(Session.Project.ScreenPrefix, number);

    public string Description
    {
        get => screen.Description;
        set => Edit(screen.Description, value.Trim(), v => screen.Description = v);
    }

    public void RefreshName() => OnPropertyChanged(nameof(Name));
}

// ---------------------------------------------------------------- control de versiones

public sealed partial class VersionsViewModel : EditableViewModel
{
    public ObservableCollection<VersionRowViewModel> Rows { get; } = [];

    [ObservableProperty]
    public partial VersionRowViewModel? Selected { get; set; }

    public VersionsViewModel(Session session) : base(session)
    {
        foreach (var v in session.Project.Versions)
        {
            Rows.Add(new VersionRowViewModel(session, v));
        }
    }

    [RelayCommand]
    private void Add()
    {
        var entry = new VersionEntry { Date = DateOnly.FromDateTime(DateTime.Today) };
        Session.Change("add-version", () => Session.Project.Versions.Add(entry));
        var row = new VersionRowViewModel(Session, entry);
        Rows.Add(row);
        Selected = row;
    }

    [RelayCommand]
    private void Remove(VersionRowViewModel? row)
    {
        row ??= Selected;
        if (row is null)
        {
            return;
        }
        Session.Change("remove-version", () => Session.Project.Versions.Remove(row.Entry));
        Rows.Remove(row);
    }
}

public sealed class VersionRowViewModel(Session session, VersionEntry entry) : EditableViewModel(session)
{
    private const string DateFormat = "dd/MM/yyyy";

    public VersionEntry Entry { get; } = entry;

    public string Date
    {
        get => Entry.Date.ToString(DateFormat, CultureInfo.InvariantCulture);
        set
        {
            if (DateOnly.TryParseExact(value.Trim(), DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                || DateOnly.TryParse(value.Trim(), CultureInfo.GetCultureInfo("es-ES"), DateTimeStyles.None, out d))
            {
                Edit(Entry.Date, d, v => Entry.Date = v);
            }
            else
            {
                OnPropertyChanged();
            }
        }
    }

    public string Description
    {
        get => Entry.Description;
        set => Edit(Entry.Description, value.Trim(), v => Entry.Description = v);
    }
}
