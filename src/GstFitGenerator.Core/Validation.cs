// Comprobaciones del proyecto antes de generar. Avisan, no impiden generar.
namespace GstFitGenerator.Core;

// Aviso con la referencia y la feature que lo provocan (null si es del proyecto), para poder ir a ello.
public sealed record Issue(int? Reference, Feature? Feature, string Text);

public static class Validation
{
    public static List<Issue> Check(Project project, Catalog catalog)
    {
        var issues = new List<Issue>();
        if (project.References.Count > Reference.Max)
        {
            issues.Add(new(null, null, L.T("Hay {0} referencias; el FB admite como máximo {1}.", project.References.Count, Reference.Max)));
        }
        foreach (var (type, tech) in project.TypeTechnologies.Where(t => t.Value is < 0 or > Technology.Max))
        {
            issues.Add(new(null, null, L.T("El tipo {0} tiene la tecnología {1}; tiene que estar entre 0 y {2}.", type, tech, Technology.Max)));
        }

        foreach (var reference in project.References.OrderBy(r => r.Number))
        {
            var ids = new Dictionary<long, Feature>();
            foreach (var row in Layout.Rows(project, reference))
            {
                var f = row.Feature;
                if (f.IsReserve)
                {
                    continue;
                }
                var name = f.ClientReference == "" ? row.Offset : f.ClientReference;
                if (f.Type is null)
                {
                    issues.Add(new(reference.Number, f, L.T("Ref{0} · {1}: no tiene tipo.", reference.Number, name)));
                }
                else if (catalog.Find(f.Type.Value) is null)
                {
                    issues.Add(new(reference.Number, f, L.T("Ref{0} · {1}: el tipo {2} no está en el catálogo.", reference.Number, name, f.Type)));
                }
                else if (project.TechnologyOf(f) is null)
                {
                    issues.Add(new(reference.Number, f, L.T("Ref{0} · {1}: su tipo no tiene tecnología asignada; cuenta como tecnología 0.", reference.Number, name)));
                }
                if (f.FeatureId is null)
                {
                    issues.Add(new(reference.Number, f, L.T("Ref{0} · {1}: no tiene FeatureId.", reference.Number, name)));
                }
                else if (!ids.TryAdd(f.FeatureId.Value, f))
                {
                    issues.Add(new(reference.Number, f, L.T("Ref{0} · {1}: el FeatureId {2} está repetido.", reference.Number, name, f.FeatureId)));
                }
                if (!f.Fixtures.Any(x => !string.IsNullOrWhiteSpace(x.LayoutName)))
                {
                    issues.Add(new(reference.Number, f, L.T("Ref{0} · {1}: no tiene ningún LayoutName; no saldrá en el FIT ni en el PLC.", reference.Number, name)));
                }
                if (f.Location is int location && project.FindLocation(location) is null)
                {
                    issues.Add(new(reference.Number, f, L.T("Ref{0} · {1}: la location {2} no existe.", reference.Number, name, location)));
                }
            }
        }
        return issues;
    }
}
