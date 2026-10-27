using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GstFitGenerator.Core;

namespace GstFitGenerator.App.ViewModels;

public sealed record IssueViewModel(Issue Issue)
{
    public string Text => Issue.Text;
}

// Ventana principal: barra superior, árbol del proyecto, editor central y barra de avisos.
public sealed partial class MainViewModel : ObservableObject
{
    public static readonly FileFilter ProjectFilter = new("GST FIT Generator", Storage.ProjectExtension);
    public static readonly FileFilter CatalogFilter = new(L.T("Catálogo de tipos de feature"), Storage.CatalogExtension);
    public static readonly FileFilter ExcelFilter = new("Excel", ".xlsx");

    private readonly Action<AppSettings> _saveSettings;

    public Session Session { get; }
    // La ventana sobre la que se abren los diálogos; cambia si se vuelve a crear la ventana.
    public IDialogs Dialogs { get; set; }
    public AppSettings Settings { get; }
    public ObservableCollection<NavNode> Tree { get; } = [];
    public ObservableCollection<IssueViewModel> Issues { get; } = [];

    // Cambio de tema o idioma; lo aplica la aplicación.
    public event Action? AppearanceChanged;

    [ObservableProperty]
    public partial NavNode? SelectedNode { get; set; }

    [ObservableProperty]
    public partial object? Editor { get; set; }

    [ObservableProperty]
    public partial string Title { get; set; } = "";

    // Nombre del proyecto y si tiene cambios sin guardar, para la barra superior.
    [ObservableProperty]
    public partial string ProjectName { get; set; } = "";

    [ObservableProperty]
    public partial bool IsDirty { get; set; }

    [ObservableProperty]
    public partial string IssuesSummary { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoIssues))]
    public partial bool HasIssues { get; set; }

    public bool HasNoIssues => !HasIssues;

    [ObservableProperty]
    public partial IssueViewModel? SelectedIssue { get; set; }

    public MainViewModel(Session session, IDialogs dialogs, AppSettings settings, Action<AppSettings> saveSettings)
    {
        Session = session;
        Dialogs = dialogs;
        Settings = settings;
        _saveSettings = saveSettings;
        Session.Changed += OnChanged;
        Session.Reset += Rebuild;
        Rebuild();
    }

    public bool IsThemeSystem => Settings.Theme == AppSettings.ThemeSystem;
    public bool IsThemeLight => Settings.Theme == AppSettings.ThemeLight;
    public bool IsThemeDark => Settings.Theme == AppSettings.ThemeDark;
    public bool IsSpanish => L.Language == L.Spanish;
    public bool IsEnglish => L.Language == L.English;
    public string ThemeIcon => IsThemeDark ? "moon" : IsThemeLight ? "sun" : "monitor";
    public string LanguageText => IsEnglish ? "EN" : "ES";

    // ---------------------------------------------------------------- árbol y editor

    public void Rebuild()
    {
        var kind = SelectedNode?.Kind ?? NavKind.Project;
        var reference = SelectedNode?.Reference ?? 0;
        Tree.Clear();
        var root = new NavNode(NavKind.Project);
        root.Children.Add(new NavNode(NavKind.Technologies));
        root.Children.Add(new NavNode(NavKind.Locations));
        var references = new NavNode(NavKind.References);
        foreach (var r in Session.Project.References.OrderBy(r => r.Number))
        {
            references.Children.Add(new NavNode(NavKind.Reference, r.Number));
        }
        root.Children.Add(references);
        root.Children.Add(new NavNode(NavKind.Auxiliaries));
        root.Children.Add(new NavNode(NavKind.Screens));
        root.Children.Add(new NavNode(NavKind.Versions));
        root.Children.Add(new NavNode(NavKind.Sizes));
        Tree.Add(root);
        RefreshTitles();
        // Forzar un editor nuevo aunque el nodo elegido sea el mismo: el proyecto puede haber cambiado entero.
        SelectedNode = null;
        SelectedNode = FindNode(kind, reference) ?? (kind == NavKind.Reference ? FindNode(NavKind.References, 0) : null) ?? root;
        OnChanged();
    }

    private void RefreshTitles()
    {
        var project = Session.Project;
        foreach (var root in Tree)
        {
            root.Title = project.Name.Length > 0 ? project.Name : L.T("Proyecto");
            root.Detail = string.Join(" · ", new[] { project.Plant, project.Line }.Where(s => s.Length > 0));
            foreach (var node in Flatten(root.Children))
            {
                (node.Title, node.Detail) = node.Kind switch
                {
                    NavKind.Technologies => (L.T("Tecnologías"), ""),
                    NavKind.Locations => (L.T("Locations"), Count(project.Locations.Count)),
                    NavKind.References => (L.T("Referencias"), Count(project.References.Count)),
                    NavKind.Reference => (L.T("Referencia {0}", node.Reference), Count(project.FindReference(node.Reference)?.Features.Count ?? 0)),
                    NavKind.Auxiliaries => (L.T("Auxiliares"), Count(project.Auxiliaries.Count)),
                    NavKind.Screens => (L.T("Screen Names"), ""),
                    NavKind.Versions => (L.T("Control de versiones"), Count(project.Versions.Count)),
                    _ => (L.T("Tamaños"), ""),
                };
            }
        }
    }

    private static string Count(int n) => n.ToString(CultureInfo.InvariantCulture);

    private static IEnumerable<NavNode> Flatten(IEnumerable<NavNode> nodes) =>
        nodes.SelectMany(n => new[] { n }.Concat(Flatten(n.Children)));

    public NavNode? FindNode(NavKind kind, int reference = 0) =>
        Flatten(Tree).FirstOrDefault(n => n.Matches(kind, reference));

    partial void OnSelectedNodeChanged(NavNode? value)
    {
        var project = Session.Project;
        Editor = value?.Kind switch
        {
            null => null,
            NavKind.Project => new ProjectEditorViewModel(this),
            NavKind.Technologies => new TechnologiesViewModel(Session),
            NavKind.Locations => new LocationsViewModel(Session),
            NavKind.References => project.References.Count > 0
                ? new ReferenceViewModel(this, project.References.OrderBy(r => r.Number).First())
                : new ProjectEditorViewModel(this),
            NavKind.Reference => project.FindReference(value.Reference) is { } r ? new ReferenceViewModel(this, r) : null,
            NavKind.Auxiliaries => new AuxiliariesViewModel(Session),
            NavKind.Screens => new ScreensViewModel(Session),
            NavKind.Versions => new VersionsViewModel(Session),
            _ => new SizesViewModel(this),
        };
    }

    private void OnChanged()
    {
        var name = Session.Project.Name.Length > 0 ? Session.Project.Name : L.T("Proyecto");
        Title = $"GST FIT Generator — {name}{(Session.Dirty ? " *" : "")}";
        ProjectName = name;
        IsDirty = Session.Dirty;
        RefreshTitles();
        Issues.Clear();
        foreach (var issue in Validation.Check(Session.Project, Session.Catalog))
        {
            Issues.Add(new IssueViewModel(issue));
        }
        IssuesSummary = Issues.Count switch
        {
            0 => L.T("Sin avisos"),
            1 => L.T("1 aviso"),
            _ => L.T("{0} avisos", Issues.Count),
        };
        HasIssues = Issues.Count > 0;
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    // Ir a lo que provoca un aviso: la referencia y la feature.
    partial void OnSelectedIssueChanged(IssueViewModel? value)
    {
        if (value?.Issue is not { Reference: int reference } issue)
        {
            return;
        }
        if (Editor is not ReferenceViewModel current || current.Reference.Number != reference)
        {
            SelectedNode = FindNode(NavKind.Reference, reference) ?? SelectedNode;
        }
        if (Editor is ReferenceViewModel editor && issue.Feature is { } feature)
        {
            editor.Select(feature);
        }
    }

    // ---------------------------------------------------------------- archivo

    [RelayCommand]
    private async Task New()
    {
        if (await ConfirmDiscard())
        {
            Session.New();
        }
    }

    [RelayCommand]
    private async Task Open()
    {
        if (!await ConfirmDiscard())
        {
            return;
        }
        var path = await Dialogs.OpenFile(L.T("Abrir proyecto"), ProjectFilter);
        if (path is not null)
        {
            await OpenPath(path);
        }
    }

    public async Task OpenPath(string path)
    {
        try
        {
            Session.Open(path);
            Settings.LastProject = path;
            _saveSettings(Settings);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            await Dialogs.Message(L.T("Abrir proyecto"), L.T("No se pudo abrir {0}: {1}", Path.GetFileName(path), e.Message));
        }
    }

    [RelayCommand]
    public async Task<bool> Save() => Session.ProjectPath is { } path ? await SaveTo(path) : await SaveAs();

    [RelayCommand]
    private async Task<bool> SaveAs()
    {
        var name = (Session.Project.Name.Length > 0 ? Session.Project.Name : L.T("Proyecto")) + Storage.ProjectExtension;
        var path = await Dialogs.SaveFile(L.T("Guardar proyecto"), ProjectFilter, name);
        return path is not null && await SaveTo(path);
    }

    private async Task<bool> SaveTo(string path)
    {
        try
        {
            Session.Save(path);
            Settings.LastProject = path;
            _saveSettings(Settings);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            await Dialogs.Message(L.T("Guardar proyecto"), L.T("No se pudo guardar: {0}", e.Message));
            return false;
        }
    }

    // true si se puede seguir: no hay cambios, se han guardado o se descartan.
    public async Task<bool> ConfirmDiscard()
    {
        if (!Session.Dirty)
        {
            return true;
        }
        return await Dialogs.AskSave() switch
        {
            SaveChoice.Save => await Save(),
            SaveChoice.Discard => true,
            _ => false,
        };
    }

    // Crea un proyecto nuevo a partir de una plantilla Excel de las de antes.
    [RelayCommand]
    private async Task ImportTemplate()
    {
        if (!await ConfirmDiscard())
        {
            return;
        }
        var path = await Dialogs.OpenFile(L.T("Importar plantilla"), ExcelFilter);
        if (path is null)
        {
            return;
        }
        var catalog = Storage.Clone(Session.Catalog);
        ImportResult result;
        try
        {
            result = await Task.Run(() => TemplateImport.Import(path, catalog));
        }
        // ClosedXML lanza excepciones de varios tipos con un Excel dañado o abierto en otro programa.
        catch (Exception e)
        {
            await Dialogs.Message(L.T("Importar plantilla"), L.T("No se pudo leer la plantilla: {0}", e.Message));
            return;
        }
        var project = result.Project;
        var summary = new List<string>
        {
            L.T("Proyecto {0} (revisión {1}), planta {2}, línea {3}", project.Name, project.Revision, project.Plant, project.Line),
        };
        summary.AddRange(project.References.Select(r => L.T("Referencia {0}: {1} features", r.Number, r.Features.Count)));
        summary.Add(L.T("{0} auxiliares, {1} locations, {2} entradas de control de versiones", project.Auxiliaries.Count, project.Locations.Count, project.Versions.Count));
        summary.AddRange(result.AddedTypes.Select(t => L.T("Tipo nuevo en el catálogo: {0} · {1}", t.Number, t.Display)));
        var warnings = result.DifferentTypes
            .Select(d => L.T("El tipo {0} es «{1}» en el catálogo y «{2}» en la plantilla: se queda el del catálogo.", d.Catalog.Number, d.Catalog.Display, d.Template.Display))
            .Concat(result.Notes)
            .ToList();
        var (accepted, _) = await Dialogs.Changes(L.T("Importar plantilla: {0}", Path.GetFileName(path)), summary, warnings);
        if (!accepted)
        {
            return;
        }
        SelectedNode = null;
        Session.Load(project, catalog);
    }

    // ---------------------------------------------------------------- editar

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo() => Session.Undo();

    private bool CanUndo() => Session.CanUndo;

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo() => Session.Redo();

    private bool CanRedo() => Session.CanRedo;

    // ---------------------------------------------------------------- proyecto

    [RelayCommand]
    public async Task AddReference()
    {
        var project = Session.Project;
        if (project.References.Count >= Reference.Max)
        {
            await Dialogs.Message(L.T("Añadir referencia"), L.T("Ya hay {0} referencias, el máximo del FB.", Reference.Max));
            return;
        }
        var number = Enumerable.Range(1, Reference.Max).First(n => project.FindReference(n) is null);
        Session.Change("add-reference", () => project.References.Add(new Reference { Number = number }));
        ShowReference(number);
    }

    public void ShowReference(int number)
    {
        SelectedNode = null;
        Rebuild();
        SelectedNode = FindNode(NavKind.Reference, number);
    }

    public async Task RemoveReference(Reference reference)
    {
        if (!await Dialogs.Confirm(L.T("Quitar referencia"),
                L.T("¿Quitar la referencia {0} con sus {1} features?", reference.Number, reference.Features.Count), L.T("Quitar")))
        {
            return;
        }
        Session.Change("remove-reference", () => Session.Project.References.Remove(reference));
        SelectedNode = null;
        Rebuild();
    }

    [RelayCommand]
    private async Task OpenCatalog()
    {
        await Dialogs.ShowCatalog(new CatalogViewModel(Session, Dialogs));
        Rebuild();
    }

    [RelayCommand]
    private async Task Generate()
    {
        await Dialogs.ShowGenerate(new GenerateViewModel(this));
        OnChanged();
    }

    // ---------------------------------------------------------------- ver

    [RelayCommand]
    private void SetTheme(string theme)
    {
        Settings.Theme = theme;
        _saveSettings(Settings);
        OnPropertyChanged(nameof(IsThemeSystem));
        OnPropertyChanged(nameof(IsThemeLight));
        OnPropertyChanged(nameof(IsThemeDark));
        OnPropertyChanged(nameof(ThemeIcon));
        AppearanceChanged?.Invoke();
    }

    [RelayCommand]
    private void SetLanguage(string language)
    {
        Settings.Language = language;
        L.Language = L.Normalize(language);
        _saveSettings(Settings);
        AppearanceChanged?.Invoke();
    }

    [RelayCommand]
    private Task About() => Dialogs.About(L.T("Acerca de"), L.T("Creado por Ignacio Suarez."), UpdateCheck.RepositoryUrl);

    // ---------------------------------------------------------------- versión nueva

    public static Version CurrentVersion => typeof(MainViewModel).Assembly.GetName().Version ?? new Version(0, 0, 0);

    // Al abrir: si en GitHub hay una release más nueva que esta versión, se avisa en una barra arriba.
    public async Task CheckForUpdates(Func<CancellationToken, Task<ReleaseInfo?>> latest)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var release = await latest(timeout.Token);
        if (release is not null && UpdateCheck.IsNewer(release, CurrentVersion))
        {
            UpdateUrl = new Uri(release.Url);
            UpdateNotice = L.T("Hay una versión nueva: {0} (tienes la {1}).", release.Tag, CurrentVersion.ToString(3));
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUpdate))]
    public partial string? UpdateNotice { get; set; }

    [ObservableProperty]
    public partial Uri? UpdateUrl { get; set; }

    public bool HasUpdate => UpdateNotice is not null;

    [RelayCommand]
    private void DismissUpdate() => UpdateNotice = null;
}
