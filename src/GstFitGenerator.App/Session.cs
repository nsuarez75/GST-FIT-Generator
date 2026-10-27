// Estado de trabajo: el proyecto abierto, el catálogo de tipos de feature y el historial para deshacer y rehacer.
// Cada edición guarda antes una foto (JSON) del proyecto y del catálogo.
using GstFitGenerator.Core;

namespace GstFitGenerator.App;

public sealed class Session
{
    private const int MaxHistory = 200;
    private static readonly TimeSpan Coalesce = TimeSpan.FromSeconds(1.5);

    private readonly LinkedList<Snapshot> _undo = new();
    private readonly Stack<Snapshot> _redo = new();
    private string? _lastKey;
    private DateTime _lastTime;

    private sealed record Snapshot(string Project, string Catalog);

    public Project Project { get; private set; } = new();
    public Catalog Catalog { get; private set; } = new();
    public string? ProjectPath { get; private set; }
    public string CatalogPath { get; }
    public bool Dirty { get; private set; }

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    // Cualquier cambio de datos (para recalcular avisos y el título).
    public event Action? Changed;
    // El proyecto o el catálogo se han sustituido entero (abrir, deshacer...): hay que rehacer las vistas.
    public event Action? Reset;

    public Session(string catalogPath)
    {
        CatalogPath = catalogPath;
        Catalog = Storage.LoadCatalog(catalogPath);
    }

    // Antes de cambiar algo. Varias ediciones seguidas del mismo campo cuentan como una sola para deshacer.
    public void BeforeChange(string key)
    {
        var now = DateTime.UtcNow;
        if (key != _lastKey || now - _lastTime > Coalesce)
        {
            _undo.AddLast(Take());
            if (_undo.Count > MaxHistory)
            {
                _undo.RemoveFirst();
            }
            _redo.Clear();
        }
        _lastKey = key;
        _lastTime = now;
    }

    public void AfterChange()
    {
        Dirty = true;
        Changed?.Invoke();
    }

    // Cambio hecho de una vez (importar, borrar...): foto, cambio y aviso.
    public void Change(string key, Action action)
    {
        BeforeChange(key);
        _lastKey = null;
        action();
        AfterChange();
    }

    public void Undo()
    {
        if (_undo.Last is not { } last)
        {
            return;
        }
        _undo.RemoveLast();
        _redo.Push(Take());
        Restore(last.Value);
    }

    public void Redo()
    {
        if (_redo.Count == 0)
        {
            return;
        }
        _undo.AddLast(Take());
        Restore(_redo.Pop());
    }

    // Proyecto nuevo: una referencia vacía y una location para empezar.
    public void New()
    {
        Project = new Project
        {
            Name = L.T("Proyecto nuevo"),
            References = [new Reference { Number = 1 }],
            Locations = [new Location { Name = "Location 1", Number = 1 }],
        };
        ProjectPath = null;
        Dirty = false;
        ClearHistory();
        Reset?.Invoke();
    }

    public void Open(string path)
    {
        Project = Storage.LoadProject(path);
        ProjectPath = path;
        Dirty = false;
        ClearHistory();
        Reset?.Invoke();
    }

    // Proyecto recién importado de una plantilla (con el catálogo ampliado): sin archivo todavía y con cambios por guardar.
    public void Load(Project project, Catalog catalog)
    {
        Project = project;
        Catalog = catalog;
        ProjectPath = null;
        Dirty = true;
        ClearHistory();
        Reset?.Invoke();
        Changed?.Invoke();
    }

    public void Save(string path)
    {
        Storage.SaveProject(Project, path);
        SaveCatalog();
        ProjectPath = path;
        Dirty = false;
        Changed?.Invoke();
    }

    public void SaveCatalog() => Storage.SaveCatalog(Catalog, CatalogPath);

    // Sustituye el catálogo entero (importar un .fitcat) pudiendo deshacerlo.
    public void ReplaceCatalog(Catalog catalog)
    {
        BeforeChange("catalog-import");
        _lastKey = null;
        Catalog = catalog;
        Dirty = true;
        Reset?.Invoke();
        Changed?.Invoke();
    }

    private Snapshot Take() => new(Storage.ToJson(Project), Storage.ToJson(Catalog));

    private void Restore(Snapshot snapshot)
    {
        Project = Storage.FromJson<Project>(snapshot.Project);
        Catalog = Storage.FromJson<Catalog>(snapshot.Catalog);
        _lastKey = null;
        Dirty = true;
        Reset?.Invoke();
        Changed?.Invoke();
    }

    private void ClearHistory()
    {
        _undo.Clear();
        _redo.Clear();
        _lastKey = null;
    }
}
