// FeatureId propuesto a partir del FeatureReferenceClient, con la regla de las plantillas:
//   W69    -> 10069  (10000 + cordón)
//   W73_01 -> 17301  (10000 + cordón × 100 + sufijo)
// Cualquier otro formato no tiene propuesta: el FeatureId se escribe a mano.
using System.Globalization;
using System.Text.RegularExpressions;

namespace GstFitGenerator.Core;

public static partial class FeatureIds
{
    public static long? Suggest(string? clientReference)
    {
        var m = WeldPattern().Match(clientReference?.Trim() ?? "");
        if (!m.Success)
        {
            return null;
        }
        var weld = long.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        return m.Groups[2].Success
            ? 10000 + weld * 100 + long.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture)
            : 10000 + weld;
    }

    [GeneratedRegex(@"^W(\d{1,2})(?:_(\d{1,2}))?$", RegexOptions.IgnoreCase)]
    private static partial Regex WeldPattern();
}
