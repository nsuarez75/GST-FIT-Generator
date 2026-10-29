using GstFitGenerator.Core;

namespace GstFitGenerator.App.ViewModels;

// Datos del proyecto: nombre, planta, línea, revisión y prefijo de las pantallas, con un resumen.
public sealed class ProjectEditorViewModel(MainViewModel host) : EditableViewModel(host.Session)
{
    private Project Project => Session.Project;

    public MainViewModel Host { get; } = host;

    public string Name
    {
        get => Project.Name;
        set => Edit(Project.Name, value.Trim(), v => Project.Name = v);
    }

    public string Plant
    {
        get => Project.Plant;
        set => Edit(Project.Plant, value.Trim(), v => Project.Plant = v);
    }

    public string Line
    {
        get => Project.Line;
        set => Edit(Project.Line, value.Trim(), v => Project.Line = v);
    }

    public string Revision
    {
        get => Project.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture);
        set
        {
            if (int.TryParse(value, out var n) && n >= 0)
            {
                Edit(Project.Revision, n, v => Project.Revision = v);
            }
            else
            {
                OnPropertyChanged();
            }
        }
    }

    // Cifras del FeatureId (0 a 18); 0 lo escribe sin ceros delante.
    public string FeatureIdDigits
    {
        get => Project.FeatureIdDigits.ToString(System.Globalization.CultureInfo.InvariantCulture);
        set
        {
            if (int.TryParse(value, out var n) && n is >= 0 and <= 18)
            {
                Edit(Project.FeatureIdDigits, n, v => Project.FeatureIdDigits = v);
            }
            else
            {
                OnPropertyChanged();
            }
        }
    }

    public string ScreenPrefix
    {
        get => Project.ScreenPrefix;
        set => Edit(Project.ScreenPrefix, value.Trim(), v => Project.ScreenPrefix = v);
    }

    public string TemplateFile => ExcelWriters.TemplateFileName(Project, DateOnly.FromDateTime(DateTime.Today));

    public int ReferenceCount => Project.References.Count;
    public int FeatureCount => Project.References.Sum(r => r.Features.Count(f => !f.IsReserve));
    public int ReserveCount => Project.References.Sum(r => r.Features.Count(f => f.IsReserve));
    public int AuxiliaryCount => Project.Auxiliaries.Count;
}
