// Código de PLC (fuentes externas de TIA Portal), igual que lo generaba GeneradorFIT V3.0.1:
//   User_TableInfo_DB.db, User_Config_Reference_FB.awl, CheckTechTemplate_DB.db y CheckTechTemplate_DB_Init_<fixture>.scl.
// Diferencias con la V3: el número de tecnología siempre lleva 2 cifras en el FB (Tech10, no Tech010), hay tantas
// TableInfo como columnas de fixture y las features de tecnología 0 (No technology) no van al FB ni a CheckTechTemplate,
// porque no tienen array en el PLC.
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace GstFitGenerator.Core;

// Una feature en uno de sus utillajes: lo que en la V3 era una fila leída de la plantilla.
// Las reservas no generan entradas, y solo cuentan los utillajes con LayoutName.
public sealed record FitEntry(LayoutRow Row, int FixtureIndex, FixtureUse Fixture)
{
    public Feature Feature => Row.Feature;

    public static List<FitEntry> All(Project project) =>
        Layout.Rows(project)
            .Where(r => !r.Feature.IsReserve)
            .SelectMany(r => r.Feature.Fixtures
                .Select((f, i) => new FitEntry(r, i + 1, f))
                .Where(e => !string.IsNullOrWhiteSpace(e.Fixture.LayoutName)))
            .ToList();
}

public static partial class PlcWriters
{
    public const string TableInfoFile = "User_TableInfo_DB.db";
    public const string ConfigReferenceFile = "User_Config_Reference_FB.awl";
    public const string CheckTechFile = "CheckTechTemplate_DB.db";

    // ---- User_TableInfo_DB: un array TableInfo_N por cada columna de fixture con datos, sin FeatureId repetidos,
    // con los auxiliares al final.
    public static string TableInfoDb(Project project)
    {
        var entries = FitEntry.All(project);
        var groups = new List<(int Index, List<(string Asset, string Working, string Fid)> Items)>();
        var columns = entries.Count == 0 ? 0 : entries.Max(e => e.FixtureIndex);
        for (var index = 1; index <= columns; index++)
        {
            var seen = new HashSet<string>();
            var items = new List<(string Asset, string Working, string Fid)>();
            foreach (var e in entries.Where(e => e.FixtureIndex == index))
            {
                var fid = Text(e.Feature.FeatureId);
                if (seen.Add(fid))
                {
                    items.Add((e.Fixture.LayoutName, e.Fixture.Working, fid));
                }
            }
            if (items.Count == 0)
            {
                continue;
            }
            // Los auxiliares no tienen LayoutName: va su Asset, que es lo que el PLC carga en .Asset.
            foreach (var aux in project.Auxiliaries.Where(a => !string.IsNullOrWhiteSpace(a.Asset)))
            {
                var fid = Text(aux.FeatureId);
                if (seen.Add(fid))
                {
                    items.Add((aux.Asset, aux.Working, fid));
                }
            }
            groups.Add((index, items));
        }

        var sb = new StringBuilder();
        sb.Append("DATA_BLOCK \"User_TableInfo_DB\"\r\n");
        sb.Append("{ S7_Optimized_Access := 'TRUE' }\r\n");
        sb.Append("VERSION : 0.1\r\n");
        sb.Append("NON_RETAIN\r\n");
        sb.Append("//Title_english \r\n");
        sb.Append("   VAR \r\n");
        foreach (var (index, items) in groups)
        {
            sb.Append($"      TableInfo_{index} {{ S7_SetPoint := 'False'}} : Array[1..{items.Count}] of \"TableInfo_UDT\";\r\n");
        }
        sb.Append("   END_VAR\r\n");
        sb.Append("\r\n\r\n");
        sb.Append("BEGIN\r\n");
        foreach (var (index, items) in groups)
        {
            for (var i = 0; i < items.Count; i++)
            {
                var (asset, working, fid) = items[i];
                sb.Append($"   TableInfo_{index}[{i + 1}].Asset := '{asset}';\r\n");
                sb.Append($"   TableInfo_{index}[{i + 1}].Working := '{working}';\r\n");
                sb.Append($"   TableInfo_{index}[{i + 1}].FeatureID := {(fid == "" ? "0" : fid)};\r\n");
            }
        }
        sb.Append("END_DATA_BLOCK\r\n");
        return sb.ToString();
    }

    // ---- User_Config_Reference_FB: cabecera fija (interfaz con Ref_1..Ref_32) y un segmento por referencia que
    // carga el FeatureId de cada feature en su posición. Solo cuenta el primer utillaje de cada feature.
    public static string ConfigReferenceFb(Project project)
    {
        var sb = new StringBuilder();
        sb.Append(FbHeader());
        sb.Append("\r\n\r\nBEGIN\r\n");

        var any = false;
        foreach (var group in FitEntry.All(project)
                     .Where(e => e.FixtureIndex == 1 && e.Row.Technology >= 1)
                     .GroupBy(e => e.Row.Reference))
        {
            sb.Append("NETWORK\r\n");
            sb.Append($"TITLE = Referencia {group.Key:00}\r\n");
            // En fuente AWL, lo que va entre TITLE y la primera línea en blanco es el comentario del segmento.
            sb.Append("\r\n");
            foreach (var e in group)
            {
                sb.Append($"      //{e.Fixture.LayoutName} {e.Feature.ClientReference}\r\n");
                sb.Append($"      L     {Text(e.Feature.FeatureId)};\r\n");
                sb.Append($"      T     #Ref_{group.Key}.Tech{e.Row.Technology:00}[{e.Row.Pointer}].Name;\r\n");
                sb.Append("\r\n");
            }
            any = true;
        }
        // Sin referencias, un segmento vacío, como el de un FB recién creado en TIA.
        if (!any)
        {
            sb.Append("NETWORK\r\n");
            sb.Append("TITLE = \r\n");
        }
        sb.Append("END_FUNCTION_BLOCK\r\n");
        return sb.ToString();
    }

    private static string FbHeader()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("User_Config_Reference_FB.header.awl")
            ?? throw new InvalidOperationException("Falta la cabecera del FB.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    // ---- CheckTechTemplate_DB: una variable por fixture (1000FIX01 -> "++1000+FIX01") y, por cada fixture,
    // un SCL que activa sus tecnologías y la posición de cada feature en cada referencia.
    public sealed record FixtureChecks(string Name, SortedSet<(int Reference, int Technology, int Pointer)> Uses)
    {
        public string FileName
        {
            get
            {
                var clean = Name.Replace("+", "");
                foreach (var c in Path.GetInvalidFileNameChars())
                {
                    clean = clean.Replace(c, '_');
                }
                return $"CheckTechTemplate_DB_Init_{clean}.scl";
            }
        }
    }

    public static List<FixtureChecks> CheckTechFixtures(Project project)
    {
        var fixtures = new List<FixtureChecks>();
        var index = new Dictionary<string, FixtureChecks>();
        foreach (var e in FitEntry.All(project))
        {
            if (string.IsNullOrWhiteSpace(e.Fixture.Fixture))
            {
                continue;
            }
            var name = FixtureVariable(e.Fixture.Fixture.Trim());
            if (!index.TryGetValue(name, out var fixture))
            {
                fixture = new FixtureChecks(name, []);
                index.Add(name, fixture);
                fixtures.Add(fixture);
            }
            if (e.Row.Technology >= 1)
            {
                fixture.Uses.Add((e.Row.Reference, e.Row.Technology, e.Row.Pointer));
            }
        }
        return fixtures;
    }

    public static string FixtureVariable(string fixture)
    {
        var m = FixturePattern().Match(fixture);
        return m.Success ? $"++{m.Groups[1].Value}+{m.Groups[2].Value}" : fixture;
    }

    public static string CheckTechDb(IEnumerable<FixtureChecks> fixtures)
    {
        var sb = new StringBuilder();
        sb.Append("DATA_BLOCK \"CheckTechTemplate_DB\"\r\n");
        sb.Append("{ S7_Optimized_Access := 'TRUE' }\r\n");
        sb.Append("VERSION : 0.1\r\n");
        sb.Append("NON_RETAIN\r\n");
        sb.Append("   VAR \r\n");
        foreach (var fixture in fixtures)
        {
            sb.Append($"      \"{fixture.Name}\" {{ S7_SetPoint := 'False'}} : \"CheckTechTemplate_UDT\";\r\n");
        }
        sb.Append("   END_VAR\r\n");
        sb.Append("\r\n\r\n");
        sb.Append("BEGIN\r\n");
        sb.Append("\r\n");
        sb.Append("END_DATA_BLOCK\r\n");
        sb.Append("\r\n");
        return sb.ToString();
    }

    public static string CheckTechInit(FixtureChecks fixture)
    {
        var sb = new StringBuilder();
        var prefix = $"\"CheckTechTemplate_DB\".\"{fixture.Name}\"";
        sb.Append($"// {fixture.Name}\r\n");
        foreach (var tech in fixture.Uses.Select(u => u.Technology).Distinct().Order())
        {
            sb.Append($"{prefix}.TechSelection[{tech}] := TRUE;\r\n");
        }
        // Uses ya va ordenado por referencia, tecnología y pointer.
        foreach (var group in fixture.Uses.GroupBy(u => u.Reference))
        {
            sb.Append("\r\n");
            sb.Append($"REGION Ref{group.Key}\r\n");
            foreach (var use in group)
            {
                sb.Append($"    {prefix}.Ref{use.Reference}.Tech{use.Technology}[{use.Pointer}] := TRUE;\r\n");
            }
            sb.Append("END_REGION\r\n");
        }
        return sb.ToString();
    }

    internal static string Text(long? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "";

    [GeneratedRegex(@"^(\d{4})(.+)$")]
    private static partial Regex FixturePattern();
}
