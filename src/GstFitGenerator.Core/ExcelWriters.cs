// Excel generados: el FIT para la plataforma I4 (igual que GeneradorFIT V3.0.1) y la plantilla completa,
// que antes se rellenaba a mano (hojas Control Versiones, PLC-RefN, PLC-Aux, Screens Names y Feature Type Table).
// Los títulos de hojas y columnas son los de siempre y no cambian con el idioma de la app.
using System.Globalization;
using ClosedXML.Excel;

namespace GstFitGenerator.Core;

public static class ExcelWriters
{
    // Texto que identifica una reserva en la plantilla y en las listas del HMI.
    public const string ReserveText = "Reserva";

    public static string FitFileName(Project project, DateOnly date) => $"{BaseName(project)}_FIT_{date:yyyyMMdd}.xlsx";

    public static string TemplateFileName(Project project, DateOnly date) =>
        $"{BaseName(project)}_FIT_rev{project.Versions.Count}_{date:yyyyMMdd}.xlsx";

    private static string BaseName(Project project)
    {
        var name = string.IsNullOrWhiteSpace(project.Name) ? "Proyecto" : project.Name.Trim();
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }
        return name;
    }

    // ---- Excel FIT: una hoja FIT con una fila por feature y utillaje; Asset y GUID en blanco para rellenar después.
    // Los valores van como texto, igual que los escribía la V3.
    public static XLWorkbook Fit(Project project, Catalog catalog)
    {
        var book = new XLWorkbook();
        var sheet = book.Worksheets.Add("FIT");
        string[] headers =
        [
            "Plant", "Line", "GUID", "PartReference", "Asset", "LayoutName",
            "Working", "FeatureReferenceClient", "Process | FeatureType",
            "Location", "FeatureId", "CriticalFeature",
        ];
        for (var c = 0; c < headers.Length; c++)
        {
            sheet.Cell(1, c + 1).Value = headers[c];
        }

        var row = 2;
        foreach (var e in FitEntry.All(project))
        {
            sheet.Cell(row, 1).Value = project.Plant;
            sheet.Cell(row, 2).Value = project.Line;
            sheet.Cell(row, 4).Value = e.Row.Reference.ToString(CultureInfo.InvariantCulture);
            sheet.Cell(row, 6).Value = e.Fixture.LayoutName;
            sheet.Cell(row, 7).Value = e.Fixture.Working;
            sheet.Cell(row, 8).Value = e.Feature.ClientReference;
            sheet.Cell(row, 9).Value = TypeText(catalog, e.Feature);
            sheet.Cell(row, 10).Value = PlcWriters.Text(e.Feature.Location);
            sheet.Cell(row, 11).Value = PlcWriters.Text(e.Feature.FeatureId);
            sheet.Cell(row, 12).Value = CriticalText(e.Feature.Critical);
            row++;
        }
        foreach (var aux in project.Auxiliaries.Where(a => !string.IsNullOrWhiteSpace(a.Asset)))
        {
            sheet.Cell(row, 1).Value = project.Plant;
            sheet.Cell(row, 2).Value = project.Line;
            sheet.Cell(row, 4).Value = "99";
            sheet.Cell(row, 6).Value = aux.Asset;
            sheet.Cell(row, 7).Value = aux.Working;
            sheet.Cell(row, 10).Value = "1";
            sheet.Cell(row, 11).Value = PlcWriters.Text(aux.FeatureId);
            sheet.Cell(row, 12).Value = "False";
            row++;
        }
        sheet.Range(1, 1, row - 1, headers.Length).CreateTable("FIT");
        return book;
    }

    // ---- Plantilla completa.
    public static XLWorkbook Template(Project project, Catalog catalog)
    {
        var book = new XLWorkbook();
        WriteVersions(book.Worksheets.Add("Control Versiones"), project);
        var layout = TemplateLayout.For(project);
        foreach (var reference in project.References.OrderBy(r => r.Number))
        {
            WriteReference(book.Worksheets.Add($"PLC-Ref{reference.Number}"), project, catalog, reference, layout);
        }
        WriteAux(book.Worksheets.Add("PLC-Aux"), project);
        WriteScreens(book.Worksheets.Add("Screens Names"), project);
        WriteTypes(book.Worksheets.Add("Feature Type Table"), project, catalog);
        return book;
    }

    private static void WriteVersions(IXLWorksheet sheet, Project project)
    {
        sheet.Cell(7, 4).Value = "Fecha :";
        sheet.Cell(7, 5).Value = "DESCRIPCIÓN CAMBIOS";
        sheet.Range(7, 4, 7, 5).Style.Font.Bold = true;
        var row = 8;
        foreach (var v in project.Versions)
        {
            sheet.Cell(row, 4).Value = v.Date.ToDateTime(TimeOnly.MinValue);
            sheet.Cell(row, 4).Style.DateFormat.Format = "dd/mm/yyyy";
            sheet.Cell(row, 5).Value = v.Description;
            row++;
        }
        sheet.Column(4).Width = 12;
        sheet.Column(5).Width = 90;
    }

    private static void WriteReference(IXLWorksheet sheet, Project project, Catalog catalog, Reference reference, TemplateLayout layout)
    {
        foreach (var (column, header) in layout.Headers())
        {
            sheet.Cell(1, column).Value = header;
        }
        var headerRow = sheet.Row(1);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Alignment.WrapText = true;

        var row = 2;
        foreach (var r in Layout.Rows(project, reference))
        {
            var f = r.Feature;
            sheet.Cell(row, 1).Value = project.Plant;
            sheet.Cell(row, 2).Value = project.Line;
            sheet.Cell(row, 3).Value = r.Reference;
            for (var i = 0; i < f.Fixtures.Count; i++)
            {
                var column = layout.FixtureColumn(i + 1);
                sheet.Cell(row, column).Value = f.Fixtures[i].Fixture;
                sheet.Cell(row, column + 1).Value = f.Fixtures[i].LayoutName;
                sheet.Cell(row, column + 2).Value = f.Fixtures[i].Working;
            }
            sheet.Cell(row, layout.Client).Value = f.IsReserve ? ReserveText : f.ClientReference;
            sheet.Cell(row, layout.Client + 1).Value = f.IsReserve ? "" : TypeText(catalog, f);
            SetNumber(sheet.Cell(row, layout.Client + 2), f.Location);
            SetNumber(sheet.Cell(row, layout.Client + 3), f.IsReserve ? 0 : f.FeatureId);
            sheet.Cell(row, layout.Client + 4).Value = CriticalText(f.Critical);
            SetNumber(sheet.Cell(row, layout.FeatureType), f.Type);
            sheet.Cell(row, layout.FeatureType + 1).Value = r.Technology;
            sheet.Cell(row, layout.FeatureType + 2).Value = r.Pointer;
            sheet.Cell(row, layout.FeatureType + 3).Value = r.MainPointer;
            sheet.Cell(row, layout.FeatureType + 4).Value = r.Offset;

            // Listas del HMI: descripción, FeatureId y lugar de cada offset.
            var hmi = layout.HmiLists;
            sheet.Cell(row, hmi).Value = r.Offset;
            sheet.Cell(row, hmi + 1).Value = f.IsReserve ? ReserveText : f.ClientReference;
            sheet.Cell(row, hmi + 2).Value = r.Offset;
            SetNumber(sheet.Cell(row, hmi + 3), f.IsReserve ? 0 : f.FeatureId);
            sheet.Cell(row, hmi + 4).Value = r.Offset;
            sheet.Cell(row, hmi + 5).Value = f.IsReserve || f.Fixtures.Count == 0 ? "" : f.Fixtures[0].LayoutName;
            row++;
        }

        // Lista de tecnologías del HMI (número y nombre).
        var techRow = 2;
        foreach (var tech in project.Technologies.OrderBy(t => t.Number))
        {
            sheet.Cell(techRow, layout.HmiLists + 6).Value = tech.Number;
            sheet.Cell(techRow, layout.HmiLists + 7).Value = tech.Name;
            techRow++;
        }

        sheet.SheetView.FreezeRows(1);
        sheet.Columns(1, layout.HmiLists + 7).AdjustToContents(1, Math.Min(row, 200));
    }

    private static void WriteAux(IXLWorksheet sheet, Project project)
    {
        sheet.Cell(1, 1).Value = "Asset";
        sheet.Cell(1, 2).Value = "Working";
        sheet.Cell(1, 3).Value = "FeatureId";
        sheet.Row(1).Style.Font.Bold = true;
        var row = 2;
        foreach (var aux in project.Auxiliaries)
        {
            sheet.Cell(row, 1).Value = aux.Asset;
            sheet.Cell(row, 2).Value = aux.Working;
            SetNumber(sheet.Cell(row, 3), aux.FeatureId);
            row++;
        }
        sheet.Columns(1, 3).AdjustToContents();
    }

    private static void WriteScreens(IXLWorksheet sheet, Project project)
    {
        for (var i = 0; i < Screen.Count; i++)
        {
            sheet.Cell(i + 1, 1).Value = Screen.Name(project.ScreenPrefix, i + 1);
            sheet.Cell(i + 1, 2).Value = i < project.Screens.Count ? project.Screens[i].Description : "";
        }
        sheet.Columns(1, 2).AdjustToContents();
    }

    private static void WriteTypes(IXLWorksheet sheet, Project project, Catalog catalog)
    {
        sheet.Cell(1, 1).Value = "NR";
        sheet.Cell(1, 2).Value = "Process";
        sheet.Cell(1, 3).Value = "Detail";
        sheet.Cell(1, 4).Value = "Tech PLC";
        sheet.Row(1).Style.Font.Bold = true;
        var row = 2;
        foreach (var type in catalog.Types.OrderBy(t => t.Number))
        {
            sheet.Cell(row, 1).Value = type.Number;
            sheet.Cell(row, 2).Value = type.Process;
            sheet.Cell(row, 3).Value = type.Detail;
            if (project.TypeTechnologies.TryGetValue(type.Number, out var tech))
            {
                sheet.Cell(row, 4).Value = tech;
            }
            row++;
        }
        sheet.Columns(1, 4).AdjustToContents();
    }

    internal static string TypeText(Catalog catalog, Feature feature) =>
        feature.Type is int type && catalog.Find(type) is { } t ? t.Display : "";

    private static string CriticalText(bool critical) => critical ? "True" : "False";

    private static void SetNumber(IXLCell cell, long? value)
    {
        if (value is not null)
        {
            cell.Value = value.Value;
        }
    }
}

// Columnas de las hojas PLC-RefN. Con 4 grupos de fixture (lo normal) son las de siempre: Fixture_1 en D,
// FeatureReferenceClient en P, Feature Type en V y las listas del HMI desde AD. Con más grupos, todo se desplaza.
public sealed record TemplateLayout(int FixtureGroups)
{
    public const int MinFixtureGroups = 4;

    public static TemplateLayout For(Project project) =>
        new(Math.Max(MinFixtureGroups, project.References.SelectMany(r => r.Features).Select(f => f.Fixtures.Count).DefaultIfEmpty(0).Max()));

    public int FixtureColumn(int group) => 4 + (group - 1) * 3;
    public int Client => 4 + FixtureGroups * 3;
    public int FeatureType => Client + 6;
    public int HmiLists => FeatureType + 8;

    public IEnumerable<(int Column, string Header)> Headers()
    {
        yield return (1, "Plant");
        yield return (2, "Line");
        yield return (3, "PartReference");
        for (var g = 1; g <= FixtureGroups; g++)
        {
            yield return (FixtureColumn(g), $"Fixture_{g}");
            yield return (FixtureColumn(g) + 1, $"LayoutName\nAsset_{g}");
            yield return (FixtureColumn(g) + 2, $"Working {g}");
        }
        yield return (Client, "FeatureReference\nClient");
        yield return (Client + 1, "Process | FeatureType");
        yield return (Client + 2, "Location");
        yield return (Client + 3, "FeatureId");
        yield return (Client + 4, "CriticalFeature");
        yield return (FeatureType, "Feature Type");
        yield return (FeatureType + 1, "Tech");
        yield return (FeatureType + 2, "Pointer");
        yield return (FeatureType + 3, "Main Pointer");
        yield return (FeatureType + 4, "Offset");
        yield return (HmiLists, "CO_Traceability_Tech_Description");
        yield return (HmiLists + 2, "CO_Traceability_Tech_ID");
        yield return (HmiLists + 4, "CO_Traceability_Tech_Place");
        yield return (HmiLists + 6, "CO_Traceability_Tech");
    }
}
