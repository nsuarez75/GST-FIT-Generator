using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GstFitGenerator.Core;

namespace GstFitGenerator.App.ViewModels;

// Ventana «Generar»: qué se escribe, carpeta de salida, qué ha cambiado de posición desde la última generación
// y, si hay cambios, una entrada nueva para el control de versiones.
public sealed partial class GenerateViewModel : ObservableObject
{
    private readonly MainViewModel _host;
    private readonly GenerationOptions _options = new();

    public ObservableCollection<string> Changes { get; } = [];
    public ObservableCollection<string> Notes { get; } = [];

    public GenerateViewModel(MainViewModel host)
    {
        _host = host;
        var project = host.Session.Project;
        foreach (var change in Layout.ChangesSinceLastGeneration(project))
        {
            var name = change.Feature is null ? L.T("(borrada)") : change.Feature.IsReserve ? ExcelWriters.ReserveText : change.Feature.ClientReference;
            Changes.Add(change.After is null ? L.T("{0}: {1} ya no está", change.Before, name) : $"{change.Before} → {change.After}  {name}");
        }
        foreach (var issue in Validation.Check(project, host.Session.Catalog))
        {
            Notes.Add(issue.Text);
        }
        AddVersion = HasChanges;
    }

    // La ventana la cierra cuando termina bien.
    public event Action? Done;

    public bool HasChanges => Changes.Count > 0;
    public bool IsFirstGeneration => _host.Session.Project.LastOffsets is null;
    public bool HasNoChanges => !HasChanges && !IsFirstGeneration;
    public bool HasNotes => Notes.Count > 0;
    public string TemplateFile => ExcelWriters.TemplateFileName(_host.Session.Project, Today);
    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);

    public bool Template { get => _options.Template; set => SetOption(_options.Template, value, v => _options.Template = v); }
    public bool FitExcel { get => _options.FitExcel; set => SetOption(_options.FitExcel, value, v => _options.FitExcel = v); }
    public bool TableInfoDb { get => _options.TableInfoDb; set => SetOption(_options.TableInfoDb, value, v => _options.TableInfoDb = v); }
    public bool ConfigReferenceFb { get => _options.ConfigReferenceFb; set => SetOption(_options.ConfigReferenceFb, value, v => _options.ConfigReferenceFb = v); }
    public bool CheckTechDb { get => _options.CheckTechDb; set => SetOption(_options.CheckTechDb, value, v => _options.CheckTechDb = v); }

    private void SetOption(bool current, bool value, Action<bool> set, [System.Runtime.CompilerServices.CallerMemberName] string? property = null)
    {
        if (current != value)
        {
            set(value);
            OnPropertyChanged(property);
        }
    }

    // Añadir al control de versiones una entrada con la fecha de hoy al generar.
    [ObservableProperty]
    public partial bool AddVersion { get; set; }

    [ObservableProperty]
    public partial string VersionText { get; set; } = "";

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    public string OutputFolder
    {
        get => _host.Session.Project.OutputFolder ?? "";
        set
        {
            if (value != OutputFolder)
            {
                _host.Session.Change("output-folder", () => _host.Session.Project.OutputFolder = value.Length == 0 ? null : value);
                OnPropertyChanged();
            }
        }
    }

    [RelayCommand]
    private async Task PickFolder()
    {
        if (await _host.Dialogs.PickFolder(L.T("Carpeta de salida")) is { } folder)
        {
            OutputFolder = folder;
        }
    }

    [RelayCommand]
    private async Task Generate()
    {
        var dialogs = _host.Dialogs;
        var session = _host.Session;
        if (!_options.Any)
        {
            await dialogs.Message(L.T("Generar"), L.T("Marca al menos una cosa que generar."));
            return;
        }
        var folder = OutputFolder;
        if (folder.Length == 0)
        {
            folder = await dialogs.PickFolder(L.T("Carpeta de salida")) ?? "";
            if (folder.Length == 0)
            {
                return;
            }
            OutputFolder = folder;
        }
        var existing = Generation.Planned(session.Project, _options, folder, Today).Where(File.Exists)
            .Select(f => Path.GetRelativePath(folder, f)).ToList();
        if (existing.Count > 0 && !await dialogs.Confirm(L.T("Sobrescribir"),
                L.T("Ya existen en la carpeta de salida:\n{0}\n\n¿Sobrescribirlos?", string.Join("\n", existing)), L.T("Sobrescribir")))
        {
            return;
        }

        IsBusy = true;
        List<string> written;
        try
        {
            // La entrada de versiones va antes, para que salga en la plantilla que se genera ahora.
            session.Change("generate", () =>
            {
                if (AddVersion && VersionText.Trim().Length > 0)
                {
                    session.Project.Versions.Add(new VersionEntry { Date = Today, Description = VersionText.Trim() });
                }
            });
            var project = session.Project;
            var catalog = session.Catalog;
            written = await Task.Run(() => Generation.Run(project, catalog, _options, folder, Today));
            session.AfterChange();
        }
        // ClosedXML y el disco pueden fallar de varias maneras (archivo abierto en Excel, sin permisos...).
        catch (Exception e)
        {
            await dialogs.Message(L.T("Generar"), L.T("No se pudo escribir en {0}: {1}", folder, e.Message));
            return;
        }
        finally
        {
            IsBusy = false;
        }
        await dialogs.Message(L.T("Generado"), L.T("Archivos escritos en {0}:\n\n{1}", folder, string.Join("\n", written.Select(f => Path.GetRelativePath(folder, f)))));
        Done?.Invoke();
    }
}
