using ClosedXML.Excel;
using GstFitGenerator.Core;

namespace GstFitGenerator.Tests;

// Importar datos de instalaciones anteriores: plantillas en inglés, Excel FIT y sobre un proyecto abierto.
public class ImportTests
{
    // Guarda y vuelve a abrir el libro, para leer los valores como los dejaría Excel.
    private static XLWorkbook Reread(XLWorkbook book)
    {
        using var stream = new MemoryStream();
        book.SaveAs(stream);
        stream.Position = 0;
        return new XLWorkbook(stream);
    }

    [Fact]
    public void La_revision_se_toma_tambien_de_v_al_final_del_nombre()
    {
        Assert.Equal(2, TemplateImport.Revision("ABC_XYZ_FIT_20260115_v2"));
        Assert.Equal("ABC_XYZ", TemplateImport.ProjectName("ABC_XYZ_FIT_20260115_v2"));
        Assert.Equal(0, TemplateImport.Revision("ABC_v2_XYZ_FIT_20260115"));
    }

    [Fact]
    public void El_control_de_versiones_con_cabecera_en_ingles_se_importa()
    {
        var project = FakeProject.Create();
        project.Versions.Add(new VersionEntry { Date = new DateOnly(2026, 1, 15), Description = "First version" });
        using var book = ExcelWriters.Template(project, FakeProject.Catalog());
        var versions = book.Worksheet("Control Versiones");
        versions.CellsUsed().First(c => c.GetString().StartsWith("Fecha", StringComparison.Ordinal)).Value = "Date:";

        using var reread = Reread(book);
        var p = TemplateImport.Import(reread, FakeProject.Catalog()).Project;

        Assert.Equal(("First version", new DateOnly(2026, 1, 15)), (p.Versions.Single().Description, p.Versions.Single().Date));
    }

    [Fact]
    public void El_excel_FIT_se_importa_con_sus_features_utillajes_y_auxiliares()
    {
        var project = FakeProject.Create();
        Editing.DuplicateReference(project, project.References[0]);
        using var book = ExcelWriters.Fit(project, FakeProject.Catalog());
        using var reread = Reread(book);
        var catalog = FakeProject.Catalog();

        var result = TemplateImport.Import(reread, catalog);
        var p = result.Project;

        Assert.Empty(result.AddedTypes);
        Assert.Equal(("PLT", "L01"), (p.Plant, p.Line));
        Assert.Equal([1, 2], p.References.Select(r => r.Number));
        Assert.Equal(
            Layout.Rows(project).Select(r => (r.Reference, r.Feature.ClientReference, r.Feature.Type, r.Feature.FeatureId, r.Feature.Fixtures.Count)),
            Layout.Rows(p).Select(r => (r.Reference, r.Feature.ClientReference, r.Feature.Type, r.Feature.FeatureId, r.Feature.Fixtures.Count)));
        var w1 = FakeProject.Find(p, "W1");
        Assert.Equal(["1000R1", "1000R2"], w1.Fixtures.Select(f => f.LayoutName));
        Assert.Equal("2", w1.Fixtures[1].Working);
        Assert.Equal(("Botonera1", 100L), (p.Auxiliaries.Single().Asset, p.Auxiliaries.Single().FeatureId));
        Assert.Equal([1], p.Locations.Select(l => l.Number));
        Assert.Equal(2, result.Notes.Count);
    }

    [Fact]
    public void Del_excel_FIT_los_tipos_se_reconocen_sin_espacios_y_los_desconocidos_se_anaden()
    {
        var project = FakeProject.Create();
        using var book = ExcelWriters.Fit(project, FakeProject.Catalog());
        var sheet = book.Worksheet("FIT");
        IXLCell TypeOf(string client) => sheet.Column(8).CellsUsed().First(c => c.GetString() == client).CellRight();
        TypeOf("W3").Value = "SOLDADURA | cordón";
        TypeOf("CAM").Value = "Pegado | Cordón";
        using var reread = Reread(book);
        var catalog = FakeProject.Catalog();

        var result = TemplateImport.Import(reread, catalog);

        Assert.Equal((91, "Pegado", "Cordón"), (result.AddedTypes.Single().Number, result.AddedTypes.Single().Process, result.AddedTypes.Single().Detail));
        Assert.Contains(catalog.Types, t => t.Number == 91);
        Assert.Equal(1, FakeProject.Find(result.Project, "W3").Type);
        Assert.Equal(91, FakeProject.Find(result.Project, "CAM").Type);
    }

    [Fact]
    public void Un_libro_sin_hojas_PLC_Ref_ni_FIT_no_se_importa()
    {
        using var book = new XLWorkbook();
        book.Worksheets.Add("Otra");

        Assert.Throws<InvalidDataException>(() => TemplateImport.Import(book, FakeProject.Catalog()));
    }

    [Fact]
    public void Las_referencias_importadas_sustituyen_a_las_del_mismo_numero_y_se_anaden_las_demas()
    {
        var target = FakeProject.Create();
        target.TypeTechnologies[1] = 5;
        target.References[0].Features.RemoveAt(0);
        var imported = FakeProject.Create();
        Editing.DuplicateReference(imported, imported.References[0]);
        imported.Locations.Add(new Location { Name = "Location 7", Number = 7 });
        imported.TypeTechnologies[50] = 4;

        var skipped = TemplateImport.MergeReferences(target, imported);

        Assert.Empty(skipped);
        Assert.Equal([1, 2], target.References.Select(r => r.Number));
        Assert.Same(imported.References[0], target.References[0]);
        Assert.Equal([1, 2, 7], target.Locations.Select(l => l.Number));
        Assert.Equal("Puesto 1", target.FindLocation(1)!.Name);
        Assert.Equal(5, target.TypeTechnologies[1]);
        Assert.Equal(4, target.TypeTechnologies[50]);
        Assert.Equal("PRUEBA", target.Name);
    }

    [Fact]
    public void Las_referencias_que_no_caben_en_el_FB_no_se_importan()
    {
        var target = new Project { References = Enumerable.Range(1, Reference.Max).Select(n => new Reference { Number = n }).ToList() };
        var imported = new Project { References = [new Reference { Number = 1, Features = [new Feature()] }, new Reference { Number = 40 }] };

        var skipped = TemplateImport.MergeReferences(target, imported);

        Assert.Equal([40], skipped);
        Assert.Equal(Reference.Max, target.References.Count);
        Assert.Single(target.References[0].Features);
    }
}
