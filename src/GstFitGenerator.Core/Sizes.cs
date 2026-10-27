// Tamaños de los arrays del FB en TIA: por cada referencia y tecnología (1 a 10), features más reservas.
// Es el valor de las constantes TechNN_Ref_MM (los arrays van de 0 a la constante y los pointers empiezan en 1).
using System.Text;

namespace GstFitGenerator.Core;

public sealed record SizeEntry(int Reference, int Technology, int Count)
{
    public string Constant => Sizes.ConstantName(Technology, Reference);
}

public static class Sizes
{
    public static string ConstantName(int technology, int reference) => $"Tech{technology:00}_Ref_{reference:00}";

    public static List<SizeEntry> Compute(Project project)
    {
        var entries = new List<SizeEntry>();
        foreach (var reference in project.References.OrderBy(r => r.Number))
        {
            var counts = Layout.Rows(project, reference).GroupBy(r => r.Technology).ToDictionary(g => g.Key, g => g.Count());
            for (var tech = 1; tech <= Technology.Max; tech++)
            {
                entries.Add(new SizeEntry(reference.Number, tech, counts.GetValueOrDefault(tech)));
            }
        }
        return entries;
    }

    // Texto para copiar: una constante por línea, nombre y valor separados por tabulador.
    public static string ToText(IEnumerable<SizeEntry> entries)
    {
        var sb = new StringBuilder();
        foreach (var e in entries)
        {
            sb.Append(e.Constant).Append('\t').Append(e.Count).Append("\r\n");
        }
        return sb.ToString();
    }
}
