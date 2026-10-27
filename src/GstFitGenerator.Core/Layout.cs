// Posición de cada feature: se agrupan por tecnología (de menor a mayor) y dentro de cada tecnología
// mantienen el orden de la lista. Es lo que hacían las columnas V-Z de la plantilla:
//   Pointer      = posición dentro de su tecnología      (=IF(W2=W1, X1+1, 1))
//   Main Pointer = posición en la referencia             (=COUNTA($W$2:W2))
//   Offset       = referencia, tecnología y pointer      (=TEXT(C2,"00")&TEXT(W2,"00")&TEXT(X2,"000"))
// Nada de esto se guarda: sale siempre del orden de la lista, así que no puede descuadrarse.
namespace GstFitGenerator.Core;

public sealed record LayoutRow(Feature Feature, int Reference, int Technology, int Pointer, int MainPointer)
{
    public string Offset => Layout.Offset(Reference, Technology, Pointer);
}

public static class Layout
{
    public static string Offset(int reference, int technology, int pointer) => $"{reference:00}{technology:00}{pointer:000}";

    // Una feature cuyo tipo no tiene tecnología asignada cuenta como tecnología 0 (No technology); Validation lo avisa.
    public static int TechnologyOf(Project project, Feature feature) => project.TechnologyOf(feature) ?? 0;

    public static List<LayoutRow> Rows(Project project, Reference reference)
    {
        var rows = new List<LayoutRow>();
        var main = 0;
        foreach (var block in Blocks(project, reference))
        {
            var pointer = 0;
            foreach (var feature in block.Features)
            {
                rows.Add(new LayoutRow(feature, reference.Number, block.Technology, ++pointer, ++main));
            }
        }
        return rows;
    }

    public static List<LayoutRow> Rows(Project project) =>
        project.References.OrderBy(r => r.Number).SelectMany(r => Rows(project, r)).ToList();

    // Bloques de la referencia: una tecnología con sus features en el orden de la lista.
    public static List<(int Technology, List<Feature> Features)> Blocks(Project project, Reference reference) =>
        reference.Features
            .GroupBy(f => TechnologyOf(project, f))
            .OrderBy(g => g.Key)
            .Select(g => (g.Key, g.ToList()))
            .ToList();

    public static Dictionary<string, string> Offsets(Project project) =>
        Rows(project).ToDictionary(r => r.Feature.Id, r => r.Offset);

    // Features que han cambiado de offset (o han desaparecido) desde la última generación.
    public static List<OffsetChange> ChangesSinceLastGeneration(Project project)
    {
        if (project.LastOffsets is null)
        {
            return [];
        }
        var rows = Rows(project).ToDictionary(r => r.Feature.Id);
        var changes = new List<OffsetChange>();
        foreach (var (id, before) in project.LastOffsets)
        {
            if (!rows.TryGetValue(id, out var row))
            {
                changes.Add(new OffsetChange(null, before, null));
            }
            else if (row.Offset != before)
            {
                changes.Add(new OffsetChange(row.Feature, before, row.Offset));
            }
        }
        return changes.OrderBy(c => c.Before, StringComparer.Ordinal).ToList();
    }
}

// Feature null: se ha borrado. After null: ya no tiene offset.
public sealed record OffsetChange(Feature? Feature, string Before, string? After);
