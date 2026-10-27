// Features a partir de filas copiadas de un Excel (columnas separadas por tabulador).
// Se aceptan dos formas:
//   - Filas de una plantilla (de la columna A en adelante: Plant, Line, PartReference, 4 grupos Fixture /
//     LayoutName / Working, FeatureReferenceClient, Process | FeatureType, Location, FeatureId, CriticalFeature
//     y, si se copia hasta ahí, Feature Type en la V). Se reconocen porque tienen 20 columnas o más.
//   - Filas con el orden de la tabla de la app: FeatureReferenceClient, Tipo, Location, FeatureId, Critical
//     y después tantos grupos Fixture / LayoutName / Working como haya.
// Lo que no se entiende se deja vacío. Una fila de títulos (sin FeatureId ni tipo numéricos) se salta.
using System.Globalization;

namespace GstFitGenerator.Core;

public static class Paste
{
    private const int TemplateColumns = 20;

    public static List<Feature> Parse(string text)
    {
        var features = new List<Feature>();
        foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
        {
            if (line.Trim().Length == 0)
            {
                continue;
            }
            var cells = line.Split('\t').Select(c => c.Trim()).ToArray();
            var feature = cells.Length >= TemplateColumns ? FromTemplate(cells) : FromTable(cells);
            if (feature is not null)
            {
                features.Add(feature);
            }
        }
        return features;
    }

    private static Feature? FromTemplate(string[] c)
    {
        var feature = new Feature
        {
            ClientReference = c[15],
            Location = Int(c[17]),
            FeatureId = Long(c[18]),
            Critical = IsTrue(c[19]),
            Type = c.Length > 21 ? Int(c[21]) : null,
        };
        for (var g = 0; g < 4; g++)
        {
            AddFixture(feature, c[3 + g * 3], c[4 + g * 3], c[5 + g * 3]);
        }
        return IsHeader(feature) ? null : feature;
    }

    private static Feature? FromTable(string[] c)
    {
        string At(int i) => i < c.Length ? c[i] : "";
        var feature = new Feature
        {
            ClientReference = At(0),
            Type = Int(At(1)),
            Location = Int(At(2)),
            FeatureId = Long(At(3)),
            Critical = IsTrue(At(4)),
        };
        for (var i = 5; i < c.Length; i += 3)
        {
            AddFixture(feature, At(i), At(i + 1), At(i + 2));
        }
        return IsHeader(feature) ? null : feature;
    }

    private static void AddFixture(Feature feature, string fixture, string layoutName, string working)
    {
        if (fixture.Length > 0 || layoutName.Length > 0 || working.Length > 0)
        {
            feature.Fixtures.Add(new FixtureUse { Fixture = fixture, LayoutName = layoutName, Working = working });
        }
    }

    private static bool IsHeader(Feature f) => f.FeatureId is null && f.Type is null && f.Location is null;

    private static bool IsTrue(string text) =>
        text.Equals("True", StringComparison.OrdinalIgnoreCase) || text.Equals("Verdadero", StringComparison.OrdinalIgnoreCase) || text == "1";

    private static long? Long(string text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && d == Math.Floor(d) ? (long)d : null;

    private static int? Int(string text) => Long(text) is long v and >= int.MinValue and <= int.MaxValue ? (int)v : null;
}
