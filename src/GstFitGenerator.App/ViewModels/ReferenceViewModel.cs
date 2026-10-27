using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GstFitGenerator.Core;

namespace GstFitGenerator.App.ViewModels;

// Una referencia: tabla de features agrupada por tecnología (un bloque por tecnología, con su pointer y su offset)
// y, a la derecha, la ficha de la feature seleccionada con todos sus campos y sus utillajes.
public sealed partial class ReferenceViewModel : EditableViewModel
{
    private readonly MainViewModel _host;

    public Reference Reference { get; }
    // Cabeceras de bloque (BlockHeaderViewModel) y filas (FeatureRowViewModel), en orden.
    public ObservableCollection<object> Items { get; } = [];
    public IReadOnlyList<TypeOption> TypeOptions { get; }
    public IReadOnlyList<LocationOption> LocationOptions { get; }

    public ReferenceViewModel(MainViewModel host, Reference reference) : base(host.Session)
    {
        _host = host;
        Reference = reference;
        TypeOptions = TypeOption.All(Session.Project, Session.Catalog);
        LocationOptions = LocationOption.All(Session.Project);
        Refresh();
    }

    public string Title => L.T("Referencia {0}", Reference.Number);
    public bool IsEmpty => Reference.Features.Count == 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    public partial FeatureRowViewModel? Selected { get; set; }

    public bool HasSelection => Selected is not null;

    // Ficha de la derecha: visible al hacer doble clic en una fila o con el botón.
    [ObservableProperty]
    public partial bool ShowForm { get; set; } = true;

    // Lo que selecciona la lista; las cabeceras de bloque no se pueden seleccionar.
    public object? SelectedItem
    {
        get => Selected;
        set
        {
            if (value is FeatureRowViewModel row)
            {
                Selected = row;
            }
            OnPropertyChanged();
        }
    }

    partial void OnSelectedChanged(FeatureRowViewModel? value) => OnPropertyChanged(nameof(SelectedItem));

    // Vuelve a montar la tabla (después de añadir, mover, borrar o cambiar de tecnología) sin perder la selección.
    public void Refresh(Feature? select = null)
    {
        select ??= Selected?.Feature;
        Items.Clear();
        FeatureRowViewModel? selected = null;
        var rows = Layout.Rows(Session.Project, Reference);
        foreach (var block in rows.GroupBy(r => r.Technology))
        {
            Items.Add(new BlockHeaderViewModel(block.Key, Session.Project.TechnologyName(block.Key), block.Count(), block.Count(r => r.Feature.IsReserve)));
            foreach (var row in block)
            {
                var vm = new FeatureRowViewModel(this, row);
                Items.Add(vm);
                if (row.Feature == select)
                {
                    selected = vm;
                }
            }
        }
        Selected = selected;
        OnPropertyChanged(nameof(IsEmpty));
    }

    public void Select(Feature feature)
    {
        Selected = Items.OfType<FeatureRowViewModel>().FirstOrDefault(r => r.Feature == feature);
        ShowForm = true;
    }

    internal Session EditSession => Session;

    // ---------------------------------------------------------------- añadir, borrar y mover

    [RelayCommand]
    private void Add()
    {
        Feature? feature = null;
        Session.Change("add-feature", () => feature = Editing.AddAfter(Reference, Selected?.Feature));
        Refresh(feature);
        _host.Rebuild();
    }

    [RelayCommand]
    private async Task Delete()
    {
        if (Selected is not { } row)
        {
            return;
        }
        var feature = row.Feature;
        if (feature.IsReserve)
        {
            var following = Editing.FollowingInBlock(Session.Project, Reference, feature);
            if (!await _host.Dialogs.Confirm(L.T("Quitar reserva"),
                    L.T("Al quitar la reserva se desplazan {0} features de su tecnología.", following), L.T("Quitar")))
            {
                return;
            }
            Session.Change("delete-feature", () => Editing.Delete(Reference, feature));
            Refresh();
            return;
        }
        var count = Editing.FollowingInBlock(Session.Project, Reference, feature);
        var name = feature.ClientReference.Length > 0 ? feature.ClientReference : row.Offset;
        switch (await _host.Dialogs.AskDelete(L.T("Eliminar {0}", name),
                    L.T("Convertirla en reserva no mueve nada. Borrarla del todo desplaza {0} features de su tecnología (cambian sus posiciones en el PLC y el HMI).", count)))
        {
            case DeleteChoice.Reserve:
                Session.Change("reserve-feature", () => Editing.ConvertToReserve(Session.Project, feature));
                Refresh(feature);
                break;
            case DeleteChoice.Delete:
                Session.Change("delete-feature", () => Editing.Delete(Reference, feature));
                Refresh();
                break;
        }
    }

    [RelayCommand]
    private void MoveUp() => MoveBy(-1);

    [RelayCommand]
    private void MoveDown() => MoveBy(1);

    private void MoveBy(int delta)
    {
        if (Selected is not { } row)
        {
            return;
        }
        Move(row.Feature, row.Row.Pointer - 1 + delta);
    }

    // Arrastrar una fila sobre otra de la misma tecnología: ocupa su sitio.
    public void Drop(FeatureRowViewModel dragged, FeatureRowViewModel target)
    {
        if (dragged == target || dragged.Row.Technology != target.Row.Technology)
        {
            return;
        }
        Move(dragged.Feature, target.Row.Pointer - 1);
    }

    private void Move(Feature feature, int position)
    {
        var block = Reference.Features.Count(f => Layout.TechnologyOf(Session.Project, f) == Layout.TechnologyOf(Session.Project, feature));
        if (position < 0 || position >= block || Layout.Rows(Session.Project, Reference).First(r => r.Feature == feature).Pointer - 1 == position)
        {
            return;
        }
        Session.Change("move-feature", () => Editing.MoveInBlock(Session.Project, Reference, feature, position));
        Refresh(feature);
    }

    // Cambio de tipo pedido desde la fila. Si pasa a otra tecnología, se avisa antes de moverla.
    internal async Task ChangeType(FeatureRowViewModel row, TypeOption option)
    {
        var project = Session.Project;
        var feature = row.Feature;
        var before = Layout.TechnologyOf(project, feature);
        var after = option.ReserveTechnology ?? (option.Type is int t && project.TypeTechnologies.TryGetValue(t, out var n) ? n : 0);
        if (before != after)
        {
            var following = Editing.FollowingInBlock(project, Reference, feature);
            if (!await _host.Dialogs.Confirm(L.T("Cambiar de tecnología"),
                    L.T("La feature pasa de la tecnología {0} a la {1}: va al final de ese bloque y se desplazan {2} features de la tecnología {0}.", before, after, following),
                    L.T("Cambiar")))
            {
                row.RefreshType();
                return;
            }
        }
        Session.Change("type-feature", () => Editing.ChangeType(project, Reference, feature, option.Type, option.ReserveTechnology));
        Refresh(feature);
    }

    // ---------------------------------------------------------------- pegar y referencias

    [RelayCommand]
    private async Task Paste()
    {
        var text = await _host.Dialogs.PasteText();
        var features = text is null ? [] : Core.Paste.Parse(text);
        if (features.Count == 0)
        {
            await _host.Dialogs.Message(L.T("Pegar"),
                L.T("El portapapeles no tiene filas que se entiendan. Copia filas de una plantilla (desde la columna A) o con este orden: FeatureReferenceClient, Tipo, Location, FeatureId, Critical y los grupos Fixture, LayoutName, Working."));
            return;
        }
        Session.Change("paste-features", () =>
        {
            var index = Selected is null ? Reference.Features.Count : Reference.Features.IndexOf(Selected.Feature) + 1;
            Reference.Features.InsertRange(index, features);
        });
        Refresh(features[^1]);
        _host.Rebuild();
    }

    [RelayCommand]
    private async Task Duplicate()
    {
        Reference? copy = null;
        if (Session.Project.References.Count >= Core.Reference.Max)
        {
            await _host.Dialogs.Message(L.T("Duplicar referencia"), L.T("Ya hay {0} referencias, el máximo del FB.", Core.Reference.Max));
            return;
        }
        Session.Change("duplicate-reference", () => copy = Editing.DuplicateReference(Session.Project, Reference));
        _host.ShowReference(copy!.Number);
    }

    [RelayCommand]
    private Task RemoveReference() => _host.RemoveReference(Reference);

    // ---------------------------------------------------------------- utillajes de la feature seleccionada

    [RelayCommand]
    private void AddFixture()
    {
        if (Selected is not { } row || row.IsReserve)
        {
            return;
        }
        var last = row.Feature.Fixtures.LastOrDefault();
        var fixture = new FixtureUse { Fixture = last?.Fixture ?? "", Working = "1" };
        Session.Change("add-fixture", () => row.Feature.Fixtures.Add(fixture));
        row.ReloadFixtures();
    }

    [RelayCommand]
    private void RemoveFixture(FixtureRowViewModel? fixture)
    {
        if (Selected is not { } row || fixture is null)
        {
            return;
        }
        Session.Change("remove-fixture", () => row.Feature.Fixtures.Remove(fixture.Fixture));
        row.ReloadFixtures();
    }

    [RelayCommand]
    private void ToggleForm() => ShowForm = !ShowForm;
}

// Cabecera de un bloque: la tecnología y cuántas features y reservas tiene.
public sealed record BlockHeaderViewModel(int Technology, string Name, int Count, int Reserves)
{
    public string Title => $"{Technology:00} · {Name}";
    public string Detail => Reserves > 0 ? L.T("{0} posiciones · {1} reservas", Count, Reserves) : L.T("{0} posiciones", Count);
}

// Opción del desplegable de tipo: un tipo del catálogo o una reserva de una tecnología.
public sealed record TypeOption(int? Type, int? ReserveTechnology, string Text)
{
    public static List<TypeOption> All(Project project, Catalog catalog)
    {
        var options = catalog.Types.OrderBy(t => t.Number)
            .Select(t => new TypeOption(t.Number, null,
                project.TypeTechnologies.TryGetValue(t.Number, out var tech) ? $"{t.Number} · {t.Display}  (Tech {tech})" : $"{t.Number} · {t.Display}"))
            .ToList();
        options.AddRange(project.Technologies.OrderBy(t => t.Number)
            .Select(t => new TypeOption(null, t.Number, L.T("Reserva de {0:00} · {1}", t.Number, t.Name))));
        return options;
    }
}

public sealed record LocationOption(int? Number, string Text)
{
    public static List<LocationOption> All(Project project) =>
        [new(null, "—"), .. project.Locations.OrderBy(l => l.Number).Select(l => new LocationOption(l.Number, $"{l.Name} ({l.Number})"))];
}

public sealed class FeatureRowViewModel : EditableViewModel
{
    private readonly ReferenceViewModel _owner;
    private ObservableCollection<FixtureRowViewModel>? _fixtures;

    public FeatureRowViewModel(ReferenceViewModel owner, LayoutRow row) : base(owner.EditSession)
    {
        _owner = owner;
        Row = row;
    }

    public LayoutRow Row { get; }
    public Feature Feature => Row.Feature;
    public IReadOnlyList<TypeOption> TypeOptions => _owner.TypeOptions;
    public IReadOnlyList<LocationOption> LocationOptions => _owner.LocationOptions;
    public bool IsFeature => !IsReserve;
    public bool IsReserve => Feature.IsReserve;
    public string Offset => Row.Offset;
    public string Pointer => Row.Pointer.ToString(System.Globalization.CultureInfo.InvariantCulture);
    public string MainPointer => Row.MainPointer.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public string ClientReference
    {
        get => IsReserve ? ExcelWriters.ReserveText : Feature.ClientReference;
        set
        {
            if (IsReserve)
            {
                OnPropertyChanged();
                return;
            }
            var text = value.Trim();
            // Primera vez que se escribe: se propone el FeatureId si aún no tiene.
            var suggested = Feature.FeatureId is null ? FeatureIds.Suggest(text) : null;
            if (Edit(Feature.ClientReference, text, v =>
                {
                    Feature.ClientReference = v;
                    if (suggested is not null)
                    {
                        Feature.FeatureId = suggested;
                    }
                }) && suggested is not null)
            {
                OnPropertyChanged(nameof(FeatureId));
            }
        }
    }

    public TypeOption? Type
    {
        get => _owner.TypeOptions.FirstOrDefault(o => o.Type == Feature.Type && o.ReserveTechnology == Feature.ReserveTechnology);
        set
        {
            if (value is not null && value != Type)
            {
                _ = _owner.ChangeType(this, value);
            }
        }
    }

    public void RefreshType() => OnPropertyChanged(nameof(Type));

    public LocationOption? Location
    {
        get => _owner.LocationOptions.FirstOrDefault(o => o.Number == Feature.Location);
        set
        {
            if (value is not null && !IsReserve)
            {
                Edit(Feature.Location, value.Number, v => Feature.Location = v);
            }
        }
    }

    public string FeatureId
    {
        get => IsReserve ? "" : Text(Feature.FeatureId);
        set
        {
            if (!IsReserve && TryNumber(value, out var n))
            {
                Edit(Feature.FeatureId, n, v => Feature.FeatureId = v);
            }
            else
            {
                OnPropertyChanged();
            }
        }
    }

    public bool Critical
    {
        get => Feature.Critical;
        set
        {
            if (!IsReserve)
            {
                Edit(Feature.Critical, value, v => Feature.Critical = v);
            }
        }
    }

    // Resumen de los utillajes para la tabla: el primero y cuántos más hay.
    public string FixturesSummary
    {
        get
        {
            var first = Feature.Fixtures.FirstOrDefault();
            if (first is null)
            {
                return "";
            }
            var text = $"{first.Fixture} · {first.LayoutName}";
            return Feature.Fixtures.Count > 1 ? $"{text}  +{Feature.Fixtures.Count - 1}" : text;
        }
    }

    public ObservableCollection<FixtureRowViewModel> Fixtures =>
        _fixtures ??= new(Feature.Fixtures.Select(f => new FixtureRowViewModel(this, f)));

    public void ReloadFixtures()
    {
        _fixtures = null;
        OnPropertyChanged(nameof(Fixtures));
        OnPropertyChanged(nameof(FixturesSummary));
    }

    internal Session EditSession => Session;

    internal void FixtureChanged() => OnPropertyChanged(nameof(FixturesSummary));
}

public sealed class FixtureRowViewModel(FeatureRowViewModel owner, FixtureUse fixture) : EditableViewModel(owner.EditSession)
{
    public FixtureUse Fixture { get; } = fixture;

    public string Name
    {
        get => Fixture.Fixture;
        set
        {
            if (Edit(Fixture.Fixture, value.Trim(), v => Fixture.Fixture = v))
            {
                owner.FixtureChanged();
            }
        }
    }

    public string LayoutName
    {
        get => Fixture.LayoutName;
        set
        {
            if (Edit(Fixture.LayoutName, value.Trim(), v => Fixture.LayoutName = v))
            {
                owner.FixtureChanged();
            }
        }
    }

    public string Working
    {
        get => Fixture.Working;
        set => Edit(Fixture.Working, value.Trim(), v => Fixture.Working = v);
    }
}
