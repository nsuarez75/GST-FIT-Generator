// Importar una plantilla Excel de las de antes (o una generada por la app) y crear con ella un proyecto.
// Se leen los valores que Excel calculó y guardó (CachedValue), no las fórmulas: ClosedXML no sabe calcular
// todas las funciones de Excel (XLOOKUP, por ejemplo). Las columnas se buscan por su título, así que valen
// plantillas con más de 4 grupos de fixture.
using System.Globalization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;

namespace GstFitGenerator.Core;

public sealed class ImportResult
{
    public Project Project { get; set; } = new();
    // Tipos que no estaban en el catálogo y se han añadido.
    public List<FeatureType> AddedTypes { get; } = [];
    // Tipos que ya estaban en el catálogo con otro texto: se queda el del catálogo.
    public List<(FeatureType Catalog, FeatureType Template)> DifferentTypes { get; } = [];
    public List<string> Notes { get; } = [];
}

public static partial class TemplateImport
{
    public static ImportResult Import(string path, Catalog catalog)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var book = new XLWorkbook(stream);
        var result = Import(book, catalog);
        var fileName = Path.GetFileNameWithoutExtension(path);
        result.Project.Name = ProjectName(fileName);
        result.Project.Revision = Revision(fileName);
        return result;
    }

    public static ImportResult Import(XLWorkbook book, Catalog catalog)
    {
        var result = new ImportResult();
        var project = result.Project;
        project.Technologies = Technology.Defaults();

        if (book.Worksheets.TryGetWorksheet("Feature Type Table", out var types))
        {
            ReadTypes(types, catalog, result);
        }

        var referenceSheets = book.Worksheets
            .Select(s => (Sheet: s, Match: ReferenceSheet().Match(s.Name)))
            .Where(x => x.Match.Success)
            .Select(x => (x.Sheet, Number: int.Parse(x.Match.Groups[1].Value, CultureInfo.InvariantCulture)))
            .OrderBy(x => x.Number)
            .ToList();
        if (referenceSheets.Count == 0)
        {
            throw new InvalidDataException(L.T("La plantilla no tiene ninguna hoja PLC-Ref."));
        }
        foreach (var (sheet, number) in referenceSheets.Take(Reference.Max))
        {
            ReadReference(sheet, number, result);
        }
        if (referenceSheets.Count > Reference.Max)
        {
            result.Notes.Add(L.T("Solo se importan las {0} primeras referencias.", Reference.Max));
        }

        // Locations: la plantilla solo tiene números; se crean con un nombre provisional para renombrarlas.
        foreach (var number in project.References.SelectMany(r => r.Features).Select(f => f.Location).OfType<int>().Distinct().Order())
        {
            project.Locations.Add(new Location { Number = number, Name = $"Location {number}" });
        }

        if (book.Worksheets.TryGetWorksheet("PLC-Aux", out var aux))
        {
            ReadAux(aux, project);
        }
        if (book.Worksheets.TryGetWorksheet("Screens Names", out var screens))
        {
            ReadScreens(screens, project);
        }
        if (book.Worksheets.TryGetWorksheet("Control Versiones", out var versions))
        {
            ReadVersions(versions, project);
        }
        return result;
    }

    // ABC_XYZ_FIT_rev3_20260115 -> ABC_XYZ.
    public static string ProjectName(string fileName)
    {
        var m = ProjectNamePattern().Match(fileName);
        return m.Success ? m.Groups[1].Value : fileName;
    }

    // ABC_XYZ_FIT_rev3_20260115 -> 3. Sin rev en el nombre, 0.
    public static int Revision(string fileName)
    {
        var m = RevisionPattern().Match(fileName);
        return m.Success && int.TryParse(m.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var rev) ? rev : 0;
    }

    private static void ReadTypes(IXLWorksheet sheet, Catalog catalog, ImportResult result)
    {
        for (var row = 2; row <= LastRow(sheet); row++)
        {
            if (Int(sheet, row, 1) is not int number || Str(sheet, row, 2) is not { Length: > 0 } process)
            {
                continue;
            }
            var template = new FeatureType { Number = number, Process = process, Detail = Str(sheet, row, 3) };
            if (catalog.Find(number) is { } existing)
            {
                if (existing.Process != template.Process || existing.Detail != template.Detail)
                {
                    result.DifferentTypes.Add((existing, template));
                }
            }
            else
            {
                catalog.Types.Add(template);
                result.AddedTypes.Add(template);
            }
            if (Int(sheet, row, 4) is int tech)
            {
                result.Project.TypeTechnologies[number] = tech;
            }
        }
        catalog.Types.Sort((a, b) => a.Number.CompareTo(b.Number));
    }

    private static void ReadReference(IXLWorksheet sheet, int number, ImportResult result)
    {
        var project = result.Project;
        var columns = Columns(sheet);
        int Col(string header) => columns.TryGetValue(Normalize(header), out var c)
            ? c
            : throw new InvalidDataException(L.T("A la hoja «{0}» le falta la columna «{1}».", sheet.Name, header));

        var fixtureGroups = new List<(int Fixture, int LayoutName, int Working)>();
        for (var g = 1; columns.ContainsKey(Normalize($"Fixture_{g}")); g++)
        {
            fixtureGroups.Add((Col($"Fixture_{g}"), Col($"LayoutName Asset_{g}"), Col($"Working {g}")));
        }
        int plant = Col("Plant"), line = Col("Line"), client = Col("FeatureReference Client"), location = Col("Location");
        int featureId = Col("FeatureId"), critical = Col("CriticalFeature"), type = Col("Feature Type"), tech = Col("Tech");

        var reference = new Reference { Number = number };
        for (var row = 2; row <= LastRow(sheet); row++)
        {
            var clientText = Str(sheet, row, client);
            var typeNumber = Int(sheet, row, type);
            var techNumber = Int(sheet, row, tech);
            var fixtures = fixtureGroups
                .Select(g => new FixtureUse { Fixture = Str(sheet, row, g.Fixture), LayoutName = Str(sheet, row, g.LayoutName), Working = Str(sheet, row, g.Working) })
                .ToList();
            while (fixtures.Count > 0 && fixtures[^1] is { Fixture: "", LayoutName: "", Working: "" })
            {
                fixtures.RemoveAt(fixtures.Count - 1);
            }
            if (clientText == "" && typeNumber is null && fixtures.Count == 0)
            {
                continue;
            }

            if (project.Plant == "")
            {
                project.Plant = Str(sheet, row, plant);
                project.Line = Str(sheet, row, line);
            }

            var feature = new Feature();
            if (clientText == ExcelWriters.ReserveText && typeNumber is null)
            {
                feature.ReserveTechnology = techNumber ?? 0;
            }
            else
            {
                feature.ClientReference = clientText;
                feature.Type = typeNumber;
                feature.Fixtures = fixtures;
                feature.Location = Int(sheet, row, location);
                feature.FeatureId = Long(sheet, row, featureId);
                feature.Critical = string.Equals(Str(sheet, row, critical), "True", StringComparison.OrdinalIgnoreCase);
                // Si la tabla de tipos no traía la tecnología de este tipo, se toma la de la fila.
                if (typeNumber is int t && techNumber is int n && !project.TypeTechnologies.ContainsKey(t))
                {
                    project.TypeTechnologies[t] = n;
                }
            }
            reference.Features.Add(feature);
        }
        project.References.Add(reference);

        // Lista de tecnologías del HMI (CO_Traceability_Tech): número y, en la columna siguiente, el nombre.
        if (columns.TryGetValue(Normalize("CO_Traceability_Tech"), out var techList))
        {
            for (var row = 2; row <= LastRow(sheet); row++)
            {
                if (Int(sheet, row, techList) is int n && project.Technologies.FirstOrDefault(t => t.Number == n) is { } technology
                    && Str(sheet, row, techList + 1) is { Length: > 0 } name)
                {
                    technology.Name = name;
                }
            }
        }
    }

    private static void ReadAux(IXLWorksheet sheet, Project project)
    {
        for (var row = 2; row <= LastRow(sheet); row++)
        {
            var asset = Str(sheet, row, 1);
            if (asset != "")
            {
                project.Auxiliaries.Add(new AuxFeature { Asset = asset, Working = Str(sheet, row, 2), FeatureId = Long(sheet, row, 3) });
            }
        }
    }

    private static void ReadScreens(IXLWorksheet sheet, Project project)
    {
        var first = Str(sheet, 1, 1);
        var m = ScreenPattern().Match(first);
        project.ScreenPrefix = m.Success ? m.Groups[1].Value : first;
        for (var i = 0; i < Screen.Count; i++)
        {
            project.Screens[i].Description = Str(sheet, i + 1, 2);
        }
    }

    // La lista de cambios está bajo la celda «Fecha :»; la descripción, en la columna de al lado.
    private static void ReadVersions(IXLWorksheet sheet, Project project)
    {
        var header = sheet.CellsUsed().FirstOrDefault(c => Text(c).StartsWith("Fecha", StringComparison.OrdinalIgnoreCase));
        if (header is null)
        {
            return;
        }
        var column = header.Address.ColumnNumber;
        for (var row = header.Address.RowNumber + 1; row <= LastRow(sheet); row++)
        {
            var cell = sheet.Cell(row, column);
            var description = Str(sheet, row, column + 1);
            if (DateOf(cell) is not DateOnly date)
            {
                if (description == "" && Str(sheet, row, column) == "")
                {
                    break;
                }
                continue;
            }
            project.Versions.Add(new VersionEntry { Date = date, Description = description });
        }
    }

    private static DateOnly? DateOf(IXLCell cell)
    {
        var value = cell.CachedValue;
        if (value.IsDateTime)
        {
            return DateOnly.FromDateTime(value.GetDateTime());
        }
        if (value.IsNumber && value.GetNumber() is > 1 and < 2958466)
        {
            return DateOnly.FromDateTime(DateTime.FromOADate(value.GetNumber()));
        }
        return value.IsText && DateTime.TryParse(value.GetText(), CultureInfo.GetCultureInfo("es-ES"), DateTimeStyles.None, out var d)
            ? DateOnly.FromDateTime(d)
            : null;
    }

    private static Dictionary<string, int> Columns(IXLWorksheet sheet)
    {
        var columns = new Dictionary<string, int>();
        foreach (var cell in sheet.Row(1).CellsUsed())
        {
            columns.TryAdd(Normalize(Text(cell)), cell.Address.ColumnNumber);
        }
        return columns;
    }

    // Títulos sin espacios ni saltos de línea y en minúsculas: «LayoutName\nAsset_1» = «LayoutName Asset_1».
    private static string Normalize(string header) => WhiteSpace().Replace(header, "").ToLowerInvariant();

    private static int LastRow(IXLWorksheet sheet) => sheet.LastCellUsed()?.Address.RowNumber ?? 0;

    private static string Text(IXLCell cell)
    {
        try
        {
            var value = cell.CachedValue;
            return value.IsBlank || value.IsError ? "" : value.ToString(CultureInfo.InvariantCulture).Trim();
        }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException)
        {
            return "";
        }
    }

    private static string Str(IXLWorksheet sheet, int row, int column) => Text(sheet.Cell(row, column));

    private static long? Long(IXLWorksheet sheet, int row, int column) =>
        double.TryParse(Str(sheet, row, column), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && d == Math.Floor(d)
            ? (long)d
            : null;

    private static int? Int(IXLWorksheet sheet, int row, int column) => Long(sheet, row, column) is long v and >= int.MinValue and <= int.MaxValue ? (int)v : null;

    [GeneratedRegex(@"^PLC-Ref(\d+)$", RegexOptions.IgnoreCase)]
    private static partial Regex ReferenceSheet();

    [GeneratedRegex(@"^(.+?)_FIT(_|$)", RegexOptions.IgnoreCase)]
    private static partial Regex ProjectNamePattern();

    [GeneratedRegex(@"_rev(\d+)(_|$)", RegexOptions.IgnoreCase)]
    private static partial Regex RevisionPattern();

    [GeneratedRegex(@"^(.*)_Traceability_\d+$")]
    private static partial Regex ScreenPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhiteSpace();
}
