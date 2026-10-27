using System.Collections.ObjectModel;
using GstFitGenerator.Core;

namespace GstFitGenerator.App.ViewModels;

// Tecnologías del proyecto (0 a 10, con su nombre) y la tecnología de cada tipo de feature del catálogo.
public sealed class TechnologiesViewModel : EditableViewModel
{
    public ObservableCollection<TechnologyRowViewModel> Technologies { get; } = [];
    public ObservableCollection<TypeTechnologyRowViewModel> Types { get; } = [];

    public TechnologiesViewModel(Session session) : base(session)
    {
        foreach (var tech in session.Project.Technologies.OrderBy(t => t.Number))
        {
            Technologies.Add(new TechnologyRowViewModel(session, tech));
        }
        var choices = TechnologyChoice.All(session.Project);
        foreach (var type in session.Catalog.Types.OrderBy(t => t.Number))
        {
            Types.Add(new TypeTechnologyRowViewModel(session, type, choices));
        }
    }

    public bool HasNoTypes => Types.Count == 0;
}

public sealed class TechnologyRowViewModel(Session session, Technology technology) : EditableViewModel(session)
{
    public int Number => technology.Number;

    public string Name
    {
        get => technology.Name;
        set => Edit(technology.Name, value.Trim(), v => technology.Name = v);
    }
}

// Opción de los desplegables de tecnología: número (o ninguna) y nombre.
public sealed record TechnologyChoice(int? Number, string Text)
{
    public static List<TechnologyChoice> All(Project project) =>
        [new(null, L.T("Sin asignar")), .. project.Technologies.OrderBy(t => t.Number).Select(t => new TechnologyChoice(t.Number, $"{t.Number} · {t.Name}"))];
}

public sealed class TypeTechnologyRowViewModel(Session session, FeatureType type, IReadOnlyList<TechnologyChoice> choices) : EditableViewModel(session)
{
    public int Number => type.Number;
    public string Display => type.Display;
    public IReadOnlyList<TechnologyChoice> Choices => choices;

    public TechnologyChoice? Technology
    {
        get => choices.FirstOrDefault(c => c.Number == (Session.Project.TypeTechnologies.TryGetValue(type.Number, out var t) ? t : null));
        set
        {
            var map = Session.Project.TypeTechnologies;
            int? current = map.TryGetValue(type.Number, out var t) ? t : null;
            Edit(current, value?.Number, v =>
            {
                if (v is int n)
                {
                    map[type.Number] = n;
                }
                else
                {
                    map.Remove(type.Number);
                }
            });
        }
    }
}
