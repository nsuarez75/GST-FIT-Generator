using ClosedXML.Excel;
using GstFitGenerator.Core;

namespace GstFitGenerator.Tests;

public class GenerationTests
{
    [Fact]
    public void Tamanos_con_reservas_y_nombre_de_constante()
    {
        var project = FakeProject.Create();
        project.References[0].Features.Add(new Feature { ReserveTechnology = 2 });

        var sizes = Sizes.Compute(project);

        Assert.Equal(10, sizes.Count);
        Assert.Equal(4, sizes.Single(s => s.Technology == 2).Count);
        Assert.Equal(1, sizes.Single(s => s.Technology == 10).Count);
        Assert.Equal(0, sizes.Single(s => s.Technology == 1).Count);
        Assert.Equal("Tech02_Ref_01", sizes.Single(s => s.Technology == 2).Constant);
        Assert.Contains("Tech10_Ref_01\t1\r\n", Sizes.ToText(sizes));
    }

    [Fact]
    public void FB_carga_cada_feature_en_su_posicion_con_la_tecnologia_a_dos_cifras()
    {
        var project = FakeProject.Create();
        // Detrás de W1 (la lista empieza por MARCA).
        project.References[0].Features.Insert(2, new Feature { ReserveTechnology = 2 });

        var fb = PlcWriters.ConfigReferenceFb(project);

        Assert.StartsWith("FUNCTION_BLOCK \"User_Config_Reference_FB\"", fb);
        Assert.Contains("TITLE = Referencia 01\r\n\r\n      //1000R1 W1\r\n      L     10001;\r\n      T     #Ref_1.Tech02[1].Name;\r\n", fb);
        // La reserva no se carga, pero ocupa el pointer 2.
        Assert.Contains("      //1000R1 W2_01\r\n      L     10201;\r\n      T     #Ref_1.Tech02[3].Name;\r\n", fb);
        Assert.Contains("#Ref_1.Tech10[1].Name;", fb);
        Assert.DoesNotContain("Tech010", fb);
        Assert.EndsWith("END_FUNCTION_BLOCK\r\n", fb);
    }

    [Fact]
    public void FB_sin_referencias_lleva_un_segmento_vacio()
    {
        var fb = PlcWriters.ConfigReferenceFb(new Project());

        Assert.EndsWith("BEGIN\r\nNETWORK\r\nTITLE = \r\nEND_FUNCTION_BLOCK\r\n", fb);
    }

    [Fact]
    public void TableInfo_una_tabla_por_columna_de_fixture_sin_repetidos_y_con_auxiliares()
    {
        var project = FakeProject.Create();

        var db = PlcWriters.TableInfoDb(project);

        Assert.Contains("TableInfo_1 { S7_SetPoint := 'False'} : Array[1..6] of \"TableInfo_UDT\";", db);
        Assert.Contains("TableInfo_2 { S7_SetPoint := 'False'} : Array[1..2] of \"TableInfo_UDT\";", db);
        Assert.Contains("   TableInfo_2[1].Asset := '1000R2';\r\n   TableInfo_2[1].Working := '2';\r\n   TableInfo_2[1].FeatureID := 10001;\r\n", db);
        Assert.Contains("   TableInfo_2[2].Asset := 'Botonera1';", db);
        Assert.DoesNotContain("TableInfo_3", db);
    }

    [Fact]
    public void CheckTech_una_variable_por_fixture_y_su_inicializacion()
    {
        var project = FakeProject.Create();

        var fixtures = PlcWriters.CheckTechFixtures(project);
        var fix01 = fixtures.Single(f => f.Name == "++1000+FIX01");

        Assert.Equal(["++1000+FIX01", "++1000+FIX02", "++3000+FIX01", "++4000+FIX01"], fixtures.Select(f => f.Name).Order());
        Assert.Contains("      \"++1000+FIX01\" { S7_SetPoint := 'False'} : \"CheckTechTemplate_UDT\";", PlcWriters.CheckTechDb(fixtures));
        Assert.Equal("CheckTechTemplate_DB_Init_1000FIX01.scl", fix01.FileName);
        Assert.Equal(
            "// ++1000+FIX01\r\n" +
            "\"CheckTechTemplate_DB\".\"++1000+FIX01\".TechSelection[2] := TRUE;\r\n" +
            "\r\nREGION Ref1\r\n" +
            "    \"CheckTechTemplate_DB\".\"++1000+FIX01\".Ref1.Tech2[1] := TRUE;\r\n" +
            "    \"CheckTechTemplate_DB\".\"++1000+FIX01\".Ref1.Tech2[2] := TRUE;\r\n" +
            "    \"CheckTechTemplate_DB\".\"++1000+FIX01\".Ref1.Tech2[3] := TRUE;\r\n" +
            "END_REGION\r\n",
            PlcWriters.CheckTechInit(fix01));
    }

    [Fact]
    public void Excel_FIT_una_fila_por_feature_y_utillaje_mas_los_auxiliares()
    {
        var project = FakeProject.Create();
        project.References[0].Features.Add(new Feature { ReserveTechnology = 2 });

        using var book = ExcelWriters.Fit(project, FakeProject.Catalog());
        var sheet = book.Worksheet("FIT");

        Assert.Equal("Process | FeatureType", sheet.Cell(1, 9).GetString());
        Assert.Equal(["W1", "W1", "W2_01", "W3", "MARCA", "CAM", ""], Enumerable.Range(2, 7).Select(r => sheet.Cell(r, 8).GetString()));
        Assert.Equal("1000R2", sheet.Cell(3, 6).GetString());
        Assert.Equal("Soldadura |Cordón", sheet.Cell(2, 9).GetString());
        Assert.Equal("Inspección |", sheet.Cell(7, 9).GetString());
        Assert.Equal("99", sheet.Cell(8, 4).GetString());
        Assert.Equal("Botonera1", sheet.Cell(8, 6).GetString());
        Assert.True(sheet.Cell(9, 1).IsEmpty());
    }

    [Fact]
    public void La_plantilla_generada_se_importa_igual()
    {
        var project = FakeProject.Create();
        project.References[0].Features.Insert(2, new Feature { ReserveTechnology = 2 });
        project.References[0].Features[1].Fixtures.AddRange([FakeProject.Fx("1000FIX03", "1000R3"), FakeProject.Fx("1000FIX04", "1000R4"), FakeProject.Fx("1000FIX05", "1000R5")]);
        project.Technologies[2].Name = "Arco";
        project.Screens[0].Description = "Pantalla uno";
        project.Versions.Add(new VersionEntry { Date = new DateOnly(2026, 1, 15), Description = "Creación" });

        using var book = ExcelWriters.Template(project, FakeProject.Catalog());
        using var stream = new MemoryStream();
        book.SaveAs(stream);
        stream.Position = 0;
        using var reread = new XLWorkbook(stream);
        var catalog = FakeProject.Catalog();
        var imported = TemplateImport.Import(reread, catalog);
        var p = imported.Project;

        Assert.Empty(imported.AddedTypes);
        Assert.Empty(imported.DifferentTypes);
        Assert.Equal(("PLT", "L01", "CO_0000_000"), (p.Plant, p.Line, p.ScreenPrefix));
        Assert.Equal(Layout.Offsets(project).Values.Order(), Layout.Offsets(p).Values.Order());
        Assert.Equal(
            Layout.Rows(project).Select(r => (r.Offset, r.Feature.ClientReference, r.Feature.FeatureId, r.Feature.IsReserve, r.Feature.Fixtures.Count)),
            Layout.Rows(p).Select(r => (r.Offset, r.Feature.ClientReference, r.Feature.FeatureId, r.Feature.IsReserve, r.Feature.Fixtures.Count)));
        Assert.Equal(5, p.References[0].Features.Max(f => f.Fixtures.Count));
        Assert.Equal("Arco", p.TechnologyName(2));
        Assert.Equal("Pantalla uno", p.Screens[0].Description);
        Assert.Equal(new DateOnly(2026, 1, 15), p.Versions.Single().Date);
        Assert.Equal(100, p.Auxiliaries.Single().FeatureId);
        Assert.Equal(project.TypeTechnologies.OrderBy(t => t.Key), p.TypeTechnologies.OrderBy(t => t.Key));
    }

    [Fact]
    public void Con_cinco_fixtures_las_columnas_se_desplazan()
    {
        var layout = new TemplateLayout(5);

        Assert.Equal(16, new TemplateLayout(4).Client);
        Assert.Equal(19, layout.Client);
        Assert.Equal(33, layout.HmiLists);
        Assert.Contains((16, "Fixture_5"), layout.Headers());
    }

    [Fact]
    public void Importar_anade_al_catalogo_los_tipos_nuevos_y_avisa_de_los_distintos()
    {
        using var book = ExcelWriters.Template(FakeProject.Create(), FakeProject.Catalog());
        var catalog = new Catalog { Types = [new() { Number = 1, Process = "Soldadura", Detail = "Otro" }] };

        var result = TemplateImport.Import(book, catalog);

        Assert.Equal([2, 71, 90], result.AddedTypes.Select(t => t.Number));
        Assert.Equal("Otro", result.DifferentTypes.Single().Catalog.Detail);
        Assert.Equal([1, 2, 71, 90], catalog.Types.Select(t => t.Number));
    }

    [Fact]
    public void Nombres_de_los_excel()
    {
        var project = FakeProject.Create();
        project.Revision = 2;
        project.Versions.Add(new VersionEntry());
        var date = new DateOnly(2026, 10, 27);

        Assert.Equal("PRUEBA_FIT_rev2_20261027.xlsx", ExcelWriters.TemplateFileName(project, date));
        Assert.Equal("PRUEBA_FIT_20261027.xlsx", ExcelWriters.FitFileName(project, date));
        Assert.Equal("ABC_XYZ", TemplateImport.ProjectName("ABC_XYZ_FIT_rev3_20260115"));
        Assert.Equal(3, TemplateImport.Revision("ABC_XYZ_FIT_rev3_20260115"));
        Assert.Equal(0, TemplateImport.Revision("ABC_XYZ_FIT_20260115"));
    }

    [Fact]
    public void Generar_escribe_lo_marcado_y_guarda_los_offsets()
    {
        var project = FakeProject.Create();
        var folder = Path.Combine(Path.GetTempPath(), "gstfit-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var date = new DateOnly(2026, 10, 27);
            var options = new GenerationOptions { FitExcel = false };

            var written = Generation.Run(project, FakeProject.Catalog(), options, folder, date);

            Assert.Equal(Generation.Planned(project, options, folder, date), written);
            Assert.Contains(Path.Combine(folder, "CheckTechTemplate_DB_Init_1000FIX02.scl"), written);
            Assert.All(written, f => Assert.True(File.Exists(f)));
            Assert.False(File.Exists(Path.Combine(folder, "PRUEBA_FIT_20261027.xlsx")));
            Assert.Equal(Layout.Offsets(project), project.LastOffsets);
            Assert.Equal(folder, project.OutputFolder);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void Avisos_del_proyecto()
    {
        var project = FakeProject.Create();
        var w3 = FakeProject.Find(project, "W3");
        w3.FeatureId = 10001;
        w3.Location = 7;
        project.References[0].Features.Add(new Feature { ClientReference = "SIN", Fixtures = [FakeProject.Fx("1000FIX01", "")] });

        var texts = Validation.Check(project, FakeProject.Catalog()).Select(i => i.Text).ToList();

        Assert.Contains("Ref1 · W3: el FeatureId 10001 está repetido.", texts);
        Assert.Contains("Ref1 · W3: la location 7 no existe.", texts);
        Assert.Contains("Ref1 · SIN: no tiene tipo.", texts);
        Assert.Contains("Ref1 · SIN: no tiene FeatureId.", texts);
        Assert.Contains("Ref1 · SIN: no tiene ningún LayoutName; no saldrá en el FIT ni en el PLC.", texts);
        Assert.Equal(5, texts.Count);
    }

    [Fact]
    public void El_proyecto_se_guarda_y_se_abre_igual()
    {
        var project = FakeProject.Create();
        project.References[0].Features.Add(new Feature { ReserveTechnology = 3 });

        var json = Storage.ToJson(project);
        var again = Storage.FromJson<Project>(json);

        Assert.Equal(json, Storage.ToJson(again));
        Assert.Contains("\"1000FIX01\"", json);
    }
}
