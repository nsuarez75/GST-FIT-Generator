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
        Colors =
        [
            new(session, L.T("Fondo del recuadro"), s => s.BoxBack, (s, v) => s.BoxBack = v, Refresh),
            new(session, L.T("Borde del recuadro"), s => s.BoxBorder, (s, v) => s.BoxBorder = v, Refresh),
            new(session, L.T("Franja del título"), s => s.TitleBack, (s, v) => s.TitleBack = v, Refresh),
            new(session, L.T("Texto del título"), s => s.TitleFore, (s, v) => s.TitleFore = v, Refresh),
            new(session, L.T("Texto de las filas"), s => s.LabelFore, (s, v) => s.LabelFore = v, Refresh),
        ];
        Refresh();
    }

    // ---------------------------------------------------------------- estilo

    private TechScreenStyle Style => Session.Project.TechScreens.Style;

    public IReadOnlyList<StyleColorViewModel> Colors { get; }

    public int MinFontSize => TechScreenStyle.MinFontSize;
    public int MaxFontSize => TechScreenStyle.MaxFontSize;
    public int MinRowHeight => TechScreens.Clamp(Style).FaceplateHeight + 1;
    public int MaxRowHeight => TechScreenStyle.MaxRowHeight;
    public int MaxBoxGap => TechScreenStyle.MaxBoxGap;

    public decimal TitleFontSize
    {
        get => Style.TitleFontSize;
        set => EditStyle(Style.TitleFontSize, value, (s, v) => s.TitleFontSize = v);
    }

    public decimal LabelFontSize
    {
        get => Style.LabelFontSize;
        set => EditStyle(Style.LabelFontSize, value, (s, v) => s.LabelFontSize = v);
    }

    public decimal RowHeight
    {
        get => Style.RowHeight;
        set => EditStyle(Style.RowHeight, value, (s, v) => s.RowHeight = v);
    }

    public decimal BoxGap
    {
        get => Style.BoxGap;
        set => EditStyle(Style.BoxGap, value, (s, v) => s.BoxGap = v);
    }

    public int MaxMargin => TechScreenStyle.MaxMargin;
    public int MinFaceplateWidth => TechScreenStyle.MinFaceplateWidth;
    public int MaxFaceplateWidth => TechScreenStyle.MaxFaceplateWidth;

    public decimal MarginLeft
    {
        get => Style.MarginLeft;
        set => EditStyle(Style.MarginLeft, value, (s, v) => s.MarginLeft = v);
    }

    public decimal MarginTop
    {
        get => Style.MarginTop;
        set => EditStyle(Style.MarginTop, value, (s, v) => s.MarginTop = v);
    }

    public decimal MarginRight
    {
        get => Style.MarginRight;
        set => EditStyle(Style.MarginRight, value, (s, v) => s.MarginRight = v);
    }

    public decimal MarginBottom
    {
        get => Style.MarginBottom;
        set => EditStyle(Style.MarginBottom, value, (s, v) => s.MarginBottom = v);
    }

    // Ancho del faceplate; el alto sale de la proporción del TechLayout.
    public decimal FaceplateWidth
    {
        get => Style.FaceplateWidth;
        set
        {
            EditStyle(Style.FaceplateWidth, value, (s, v) => s.FaceplateWidth = v);
            OnPropertyChanged(nameof(FaceplateSize));
            OnPropertyChanged(nameof(MinRowHeight));
        }
    }

    public string FaceplateSize => $"{TechScreens.Clamp(Style).FaceplateWidth} × {TechScreens.Clamp(Style).FaceplateHeight}";

    private void EditStyle(int current, decimal value, Action<TechScreenStyle, int> set, [System.Runtime.CompilerServices.CallerMemberName] string? property = null)
    {
        if (Edit(current, (int)value, v => set(Style, v), property))
        {
            Refresh();
        }
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void ResetStyle()
    {
        Session.Change("tech-style-reset", () => Session.Project.TechScreens.Style = new());
        OnPropertyChanged(nameof(TitleFontSize));
        OnPropertyChanged(nameof(LabelFontSize));
        OnPropertyChanged(nameof(RowHeight));
        OnPropertyChanged(nameof(BoxGap));
        OnPropertyChanged(nameof(MarginLeft));
        OnPropertyChanged(nameof(MarginTop));
        OnPropertyChanged(nameof(MarginRight));
        OnPropertyChanged(nameof(MarginBottom));
        OnPropertyChanged(nameof(FaceplateWidth));
        OnPropertyChanged(nameof(FaceplateSize));
        OnPropertyChanged(nameof(MinRowHeight));
        foreach (var color in Colors)
        {
            color.Refresh();
        }
        Refresh();
    }

    // ---------------------------------------------------------------- recuadro seleccionado

    // El recuadro elegido en la vista previa: se busca por su referencia de pantallas, Fixture y parte, para no
    // perderlo cuando el reparto se rehace (o cambia de pantalla).
    private (int Reference, string Key, int Part)? _selectedBox;

    [ObservableProperty]
    public partial TechScreenBox? SelectedBox { get; set; }

    public bool HasSelectedBox => SelectedBox is not null;

    private TechScreenPage? BoxPage => SelectedPage?.Page;

    public string SelectedBoxTitle => SelectedBox?.Title ?? "";

    // Pantallas de la referencia del recuadro y, al final, «Pantalla nueva».
    public IReadOnlyList<string> BoxScreenOptions { get; private set; } = [];

    public int BoxScreenIndex
    {
        get => BoxPage?.Index ?? -1;
        set
        {
            if (BoxPage is { } page && SelectedBox is { } box && value >= 0 && value != page.Index)
            {
                EditBox("tech-box-screen", () => TechScreenEdits.ToScreen(Session.Project, page, box, value));
            }
        }
    }

    public decimal? BoxLeft
    {
        get => SelectedBox?.Left;
        set
        {
            if (value is decimal left && SelectedBox is { } box && (int)left != box.Left)
            {
                OnBoxDragged(box, (int)left, box.Top);
            }
        }
    }

    public decimal? BoxTop
    {
        get => SelectedBox?.Top;
        set
        {
            if (value is decimal top && SelectedBox is { } box && (int)top != box.Top)
            {
                OnBoxDragged(box, box.Left, (int)top);
            }
        }
    }

    // Filas por columna del recuadro; vacío, las que quepan.
    public decimal? BoxRows
    {
        get => SelectedBox is { } box && ManualRows(box) is int rows ? rows : null;
        set
        {
            if (BoxPage is { } page && SelectedBox is { } box && (int?)value != ManualRows(box))
            {
                EditBox("tech-box-rows", () => TechScreenEdits.SetRows(Session.Project, page, box, (int?)value));
            }
        }
    }

    public int BoxRowsHint => SelectedBox?.RowsPerColumn ?? 0;

    private int? ManualRows(TechScreenBox box) =>
        Session.Project.TechScreens.BoxEdits.FirstOrDefault(e => e.Reference == BoxPage?.Reference && e.Key == box.Key)?
            .Parts.FirstOrDefault(p => p.Part == box.Part)?.RowsPerColumn;

    // Assets por los que se puede partir: todos menos el primero.
    public IReadOnlyList<string> SplitOptions => SelectedBox?.Assets.Skip(1).Where(a => a.Length > 0).ToList() ?? [];

    public bool CanSplit => SplitOptions.Count > 0;

    [ObservableProperty]
    public partial string? SplitAsset { get; set; }

    // Clic en un recuadro de la vista previa.
    public void OnBoxClicked(TechScreenBox? box)
    {
        _selectedBox = box is null || BoxPage is null ? null : (BoxPage.Reference, box.Key, box.Part);
        SelectBox(box);
    }

    // Recuadro arrastrado (o X/Y escritos) en la pantalla que se está viendo.
    public void OnBoxDragged(TechScreenBox box, int left, int top)
    {
        if (BoxPage is { } page)
        {
            _selectedBox = (page.Reference, box.Key, box.Part);
            EditBox("tech-box-move", () => TechScreenEdits.Move(Session.Project, page, box, left, top));
        }
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void Split()
    {
        if (BoxPage is { } page && SelectedBox is { } box && SplitAsset is { } asset)
        {
            EditBox("tech-box-split", () => TechScreenEdits.Split(Session.Project, page, box, asset));
        }
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void ResetBox()
    {
        if (BoxPage is { } page && SelectedBox is { } box)
        {
            EditBox("tech-box-reset", () => TechScreenEdits.Reset(Session.Project, page, box));
        }
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void ResetAllBoxes()
    {
        Session.Change("tech-box-reset-all", () => TechScreenEdits.ResetAll(Session.Project));
        Refresh();
    }

    // Cada retoque es un paso de deshacer; después se vuelve a repartir y el recuadro sigue elegido.
    private void EditBox(string key, Action action)
    {
        Session.Change(key, action);
        Refresh();
    }

    private void SelectBox(TechScreenBox? box)
    {
        SelectedBox = box;
        var page = BoxPage;
        BoxScreenOptions = page is null
            ? []
            : [.. Pages.Where(p => p.Page.Reference == page.Reference).Select(p => p.Page.Number.ToString("00", CultureInfo.InvariantCulture)), L.T("Pantalla nueva")];
        SplitAsset = SplitOptions.FirstOrDefault();
        OnPropertyChanged(nameof(HasSelectedBox));
        OnPropertyChanged(nameof(SelectedBoxTitle));
        OnPropertyChanged(nameof(BoxScreenOptions));
        OnPropertyChanged(nameof(BoxScreenIndex));
        OnPropertyChanged(nameof(BoxLeft));
        OnPropertyChanged(nameof(BoxTop));
        OnPropertyChanged(nameof(BoxRows));
        OnPropertyChanged(nameof(BoxRowsHint));
        OnPropertyChanged(nameof(SplitOptions));
        OnPropertyChanged(nameof(CanSplit));
    }

    partial void OnSelectedPageChanged(TechPageOption? value)
    {
        // Al cambiar de pantalla a mano, el recuadro elegido deja de estar a la vista.
        if (_selectedBox is { } key && value?.Page.Boxes.FirstOrDefault(b => value.Page.Reference == key.Reference && b.Key == key.Key && b.Part == key.Part) is { } box)
        {
            SelectBox(box);
        }
        else
        {
            _selectedBox = null;
            SelectBox(null);
        }
    }

    // Vuelve a repartir y a dibujar, quedándose en la misma pantalla si sigue existiendo, o en la del recuadro
    // elegido si se ha movido a otra.
    public void Refresh()
    {
        var project = Session.Project;
        var plan = TechScreens.Plan(project);
        var selected = SelectedPage?.Page.Number;
        var boxKey = _selectedBox;
        Pages.Clear();
        foreach (var page in plan.Pages)
        {
            Pages.Add(new TechPageOption(page, Core.Screen.Name(project.ScreenPrefix, page.Number), project.Screens[page.Number - 1].Description));
        }
        _selectedBox = boxKey;
        var withBox = boxKey is { } k
            ? Pages.FirstOrDefault(p => p.Page.Reference == k.Reference && p.Page.Boxes.Any(b => b.Key == k.Key && b.Part == k.Part))
            : null;
        SelectedPage = withBox ?? Pages.FirstOrDefault(p => p.Page.Number == selected) ?? Pages.FirstOrDefault();
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

// Un color del estilo de las pantallas, para el selector de color.
public sealed class StyleColorViewModel(Session session, string label, Func<TechScreenStyle, string> get, Action<TechScreenStyle, string> set, Action changed)
    : EditableViewModel(session)
{
    public string Label { get; } = label;

    public Avalonia.Media.Color Color
    {
        get
        {
            var (r, g, b) = TechScreenColors.Parse(get(Session.Project.TechScreens.Style));
            return Avalonia.Media.Color.FromRgb(r, g, b);
        }
        set
        {
            if (Edit(get(Session.Project.TechScreens.Style), TechScreenColors.Format(value.R, value.G, value.B), v => set(Session.Project.TechScreens.Style, v)))
            {
                changed();
            }
        }
    }

    public void Refresh() => OnPropertyChanged(nameof(Color));
}

public sealed record TechPageOption(TechScreenPage Page, string Name, string Description)
{
    public string Text => Description.Length > 0 ? $"{Name} · {Description}" : $"{Name} · {Page.DefaultDescription}";
}
