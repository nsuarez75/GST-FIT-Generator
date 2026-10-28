using System.Xml.Linq;
using GstFitGenerator.Core;

namespace GstFitGenerator.Tests;

public class TechScreenTests
{
    private static Project WithStart(int reference = 1, int start = 7)
    {
        var project = FakeProject.Create();
        project.ScreenPrefix = "CO_0000_100";
        project.TechScreens.ReferenceStarts[reference] = start;
        return project;
    }

    private static string Summary(TechScreenPage page) =>
        string.Join(" | ", page.Boxes.Select(b => b.Title + ": " + string.Join(", ", b.Rows.Select(r => r.Text))));

    [Fact]
    public void Un_recuadro_por_fixture_con_las_features_de_su_asset()
    {
        var plan = TechScreens.Plan(WithStart());

        var page = Assert.Single(plan.Pages);
        Assert.Equal(7, page.Number);
        // W1 se hace en dos fixtures pero sale una vez, en el primero.
        Assert.Equal("1000FIX01: 1000R1 - W1, 1000R1 - W2_01, 1000R1 - W3 | 3000FIX01: 3000M1 - MARCA | 4000FIX01: 4000V1 - CAM", Summary(page));
        Assert.Empty(plan.Warnings);
        Assert.Empty(plan.Errors);
    }

    [Fact]
    public void Las_columnas_van_por_asset_ordenadas_por_nombre()
    {
        var project = WithStart();
        var features = project.References[0].Features;
        features.Insert(0, FakeProject.Make("W9", 1, 10009, FakeProject.Fx("1000FIX01", "1000R10")));
        features.Add(FakeProject.Make("W8", 1, 10008, FakeProject.Fx("1000FIX01", "1000R2")));

        var box = TechScreens.Plan(project).Pages[0].Boxes[0];

        Assert.Equal(["1000R1 - W1", "1000R1 - W2_01", "1000R1 - W3", "1000R2 - W8", "1000R10 - W9"], box.Rows.Select(r => r.Text));
        Assert.Equal(3, box.Rows.Select(r => r.Left).Distinct().Count());
    }

    [Fact]
    public void Los_nombres_cortos_cambian_titulos_y_textos()
    {
        var project = WithStart();
        project.TechScreens.ShortNames["1000FIX01"] = "OP10";
        project.TechScreens.ShortNames["1000R1"] = "R10.1";

        var box = TechScreens.Plan(project).Pages[0].Boxes[0];

        Assert.Equal("OP10", box.Title);
        Assert.Equal("R10.1 - W1", box.Rows[0].Text);
    }

    [Fact]
    public void Sin_tecnologia_no_sale_y_sin_fixture_va_aparte_con_aviso()
    {
        var project = WithStart();
        var features = project.References[0].Features;
        features.Add(FakeProject.Make("SIN_FX", 1, 10010));
        features.Add(new Feature { ClientReference = "SIN_TECH", Type = 999 });

        var plan = TechScreens.Plan(project);

        Assert.DoesNotContain("SIN_TECH", Summary(plan.Pages[0]));
        Assert.Contains($"{TechScreens.NoFixture}: SIN_FX", Summary(plan.Pages[0]));
        Assert.Single(plan.Warnings);
    }

    [Fact]
    public void Las_variables_del_faceplate_salen_del_layout()
    {
        var plan = TechScreens.Plan(WithStart());

        var marca = plan.Pages[0].Boxes.SelectMany(b => b.Rows).Single(r => r.Text.EndsWith("MARCA"));

        Assert.Equal((1, 3, 1, 4), (marca.Reference, marca.Technology, marca.Pointer, marca.MainPointer));
    }

    [Fact]
    public void Una_columna_larga_sigue_al_lado()
    {
        var project = WithStart();
        for (var i = 0; i < TechScreens.RowsPerColumn(new TechScreenStyle()) + 5; i++)
        {
            project.References[0].Features.Add(FakeProject.Make($"X{i}", 1, 20000 + i, FakeProject.Fx("5000FIX01", "5000R1")));
        }

        var box = TechScreens.Plan(project).Pages[0].Boxes.Single(b => b.Title == "5000FIX01");

        Assert.Equal(2, box.Rows.Select(r => r.Left).Distinct().Count());
        Assert.True(box.Rows.Max(r => r.Top) + new TechScreenStyle().FaceplateHeight <= new TechScreenStyle().AreaBottom);
    }

    [Fact]
    public void Si_no_cabe_la_referencia_siguiente_se_corre_y_avisa()
    {
        var project = WithStart(1, 7);
        var second = Storage.Clone(project.References[0]);
        second.Number = 2;
        project.References.Add(second);
        project.TechScreens.ReferenceStarts[2] = 7;
        // Muchos fixtures de un Asset cada uno para que la Ref1 necesite más de una pantalla.
        for (var f = 0; f < 60; f++)
        {
            for (var i = 0; i < 10; i++)
            {
                project.References[0].Features.Add(FakeProject.Make($"F{f}_{i}", 1, 30000 + f * 10 + i, FakeProject.Fx($"{6000 + f}FIX01", $"{6000 + f}R1")));
            }
        }

        var plan = TechScreens.Plan(project);

        var ref1 = plan.Pages.Where(p => p.Reference == 1).Select(p => p.Number).ToList();
        var ref2 = plan.Pages.Where(p => p.Reference == 2).Select(p => p.Number).ToList();
        Assert.True(ref1.Count > 1);
        Assert.Equal(ref1[^1] + 1, ref2[0]);
        Assert.Contains(plan.Warnings, w => w.StartsWith("Ref2"));
    }

    [Fact]
    public void Si_se_pasa_de_la_24_es_un_error()
    {
        var plan = TechScreens.Plan(WithStart(1, 24));
        Assert.Empty(plan.Errors);

        var project = WithStart(1, 24);
        for (var f = 0; f < 60; f++)
        {
            for (var i = 0; i < 10; i++)
            {
                project.References[0].Features.Add(FakeProject.Make($"F{f}_{i}", 1, 30000 + f * 10 + i, FakeProject.Fx($"{6000 + f}FIX01", $"{6000 + f}R1")));
            }
        }

        Assert.Single(TechScreens.Plan(project).Errors);
    }

    [Fact]
    public void Las_reservas_solo_salen_con_pantalla_de_inicio()
    {
        var project = WithStart();
        project.References[0].Features.Add(new Feature { ReserveTechnology = 2 });
        project.Technologies[2].Name = "Arco";

        Assert.DoesNotContain(TechScreens.Plan(project).Pages, p => p.IsReserves);

        project.TechScreens.ReservesStart = 20;
        var reserves = Assert.Single(TechScreens.Plan(project).Pages, p => p.IsReserves);

        Assert.Equal(20, reserves.Number);
        Assert.Equal("Ref1 · Arco: Arco - 4", Summary(reserves));
    }

    [Fact]
    public void Rellena_solo_las_descripciones_vacias()
    {
        var project = WithStart();
        project.TechScreens.ReservesStart = 9;
        project.References[0].Features.Add(new Feature { ReserveTechnology = 2 });
        project.Screens[8].Description = "Mía";

        Assert.True(TechScreens.FillDescriptions(project, TechScreens.Plan(project)));

        Assert.Equal("Ref1 Techs", project.Screens[6].Description);
        Assert.Equal("Mía", project.Screens[8].Description);
        Assert.False(TechScreens.FillDescriptions(project, TechScreens.Plan(project)));
    }

    [Fact]
    public void El_xml_lleva_nombre_numero_subtitulo_y_faceplates()
    {
        var project = WithStart();
        var plan = TechScreens.Plan(project);
        var page = plan.Pages[0];

        var xml = TechScreenWriter.Write(project, plan, page);
        var doc = XDocument.Parse(xml);
        var screen = doc.Root!.Element("Hmi.Screen.Screen")!;

        Assert.Equal("CO_0000_106_Traceability_07.xml", TechScreenWriter.FileName(project, page));
        Assert.Equal("CO_0000_106_Traceability_07", screen.Element("AttributeList")!.Element("Name")!.Value);
        Assert.Equal("106", screen.Element("AttributeList")!.Element("Number")!.Value);
        Assert.Contains(screen.Descendants("Tag"), t => t.Element("Name")!.Value == "Int_Value_007");
        var faceplates = screen.Descendants("Hmi.Screen.FaceplateInstance")
            .Where(f => f.Element("AttributeList")!.Element("FaceplateTypeName")!.Value.Contains("TechLayout"))
            .ToList();
        Assert.Equal(5, faceplates.Count);
        var marca = faceplates.Single(f => Tags(f)["Status"] == "Tech_Status_004");
        Assert.Equal(("Int_Value_001", "Int_Value_003", "Int_Value_001"), (Tags(marca)["Reference_in"], Tags(marca)["Number_tech_in"], Tags(marca)["Position_tech_in"]));
        Assert.Equal("Form_TECH", Tags(marca)["Number_Tech_out"]);
        // Sin espacios ni saltos de línea dentro de los textos.
        Assert.Contains(doc.Descendants("p"), p => p.Value == "1000R1 - W1");
        Assert.All(doc.Descendants("p"), p => Assert.Equal(p.Value.Trim(), p.Value));
        var ids = doc.Descendants().Select(e => e.Attribute("ID")?.Value).OfType<string>().ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        // Solo hay una pantalla de la Ref1: sin botones de pantalla anterior ni siguiente.
        Assert.DoesNotContain(doc.Descendants("ObjectName"), n => n.Value is "btnNextScreen" or "btnPreviousScreen");
    }

    [Fact]
    public void Los_botones_enlazan_pantallas_de_la_misma_referencia()
    {
        var project = WithStart();
        for (var f = 0; f < 60; f++)
        {
            for (var i = 0; i < 10; i++)
            {
                project.References[0].Features.Add(FakeProject.Make($"F{f}_{i}", 1, 30000 + f * 10 + i, FakeProject.Fx($"{6000 + f}FIX01", $"{6000 + f}R1")));
            }
        }
        var plan = TechScreens.Plan(project);

        var first = XDocument.Parse(TechScreenWriter.Write(project, plan, plan.Pages[0]));
        var second = XDocument.Parse(TechScreenWriter.Write(project, plan, plan.Pages[1]));

        Assert.Equal("CO_0000_107_Traceability_08", Target(first, "btnNextScreen"));
        Assert.Null(Target(first, "btnPreviousScreen"));
        Assert.Equal("CO_0000_106_Traceability_07", Target(second, "btnPreviousScreen"));
    }

    [Fact]
    public void Generar_escribe_las_pantallas_en_su_subcarpeta()
    {
        var project = WithStart();
        var folder = Path.Combine(Path.GetTempPath(), "gstfit-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var options = new GenerationOptions { Template = false, FitExcel = false, TableInfoDb = false, ConfigReferenceFb = false, CheckTechDb = false };

            var written = Generation.Run(project, FakeProject.Catalog(), options, folder, new DateOnly(2026, 10, 28));

            Assert.Equal([Path.Combine(folder, "Pantallas", "CO_0000_106_Traceability_07.xml")], written);
            Assert.Equal("Ref1 Techs", project.Screens[6].Description);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void Orden_natural_de_nombres()
    {
        Assert.Equal(["5000R1", "5000R2", "5000R10"], new[] { "5000R10", "5000R2", "5000R1" }.Order(NaturalComparer.Instance));
    }

    private static Dictionary<string, string> Tags(XElement faceplate) =>
        faceplate.Descendants("Hmi.Screen.InterfacePropertySimple")
            .ToDictionary(p => p.Element("AttributeList")!.Element("Name")!.Value, p => p.Descendants("Tag").Single().Element("Name")!.Value);

    private static string? Target(XDocument doc, string button) =>
        doc.Descendants("Hmi.Screen.Button")
            .SingleOrDefault(b => b.Element("AttributeList")!.Element("ObjectName")!.Value == button)?
            .Descendants("Hmi.Event.FunctionListEntryParameter")
            .Single(p => p.Element("AttributeList")!.Element("Name")!.Value == "Screen name")
            .Descendants("Value").Single().Element("Name")!.Value;

    [Fact]
    public void El_estilo_cambia_colores_letras_y_separaciones()
    {
        var project = WithStart();
        var style = project.TechScreens.Style;
        style.BoxBack = "#102030";
        style.TitleFore = "#FF0000";
        style.LabelFontSize = 20;
        style.RowHeight = 50;
        var plan = TechScreens.Plan(project);
        var page = plan.Pages[0];

        var rows = page.Boxes[0].Rows;
        Assert.Equal(50, rows[1].Top - rows[0].Top);
        var doc = XDocument.Parse(TechScreenWriter.Write(project, plan, page));
        var rect = doc.Descendants("Hmi.Screen.Rectangle").First();
        Assert.Equal("16, 32, 48", rect.Element("AttributeList")!.Element("BackColor")!.Value);
        var title = doc.Descendants("Hmi.Screen.TextField").First(t => t.Element("AttributeList")!.Element("ObjectName")!.Value == "txtBox_1");
        Assert.Equal("255, 0, 0", title.Element("AttributeList")!.Element("ForeColor")!.Value);
        var label = doc.Descendants("Hmi.Screen.TextField").First(t => t.Element("AttributeList")!.Element("ObjectName")!.Value == "txtTech_1");
        Assert.All(label.Descendants("FontSize"), f => Assert.Equal("20", f.Value));
    }

    [Fact]
    public void Un_estilo_fuera_de_rango_se_ajusta()
    {
        var project = WithStart();
        project.TechScreens.Style.RowHeight = 10;
        project.TechScreens.Style.LabelFontSize = 200;

        var style = TechScreens.Plan(project).Pages[0].Style;

        Assert.Equal((new TechScreenStyle().FaceplateHeight + 1, TechScreenStyle.MaxFontSize), (style.RowHeight, style.LabelFontSize));
    }

    [Fact]
    public void Un_proyecto_sin_estilo_guardado_usa_el_de_por_defecto()
    {
        var project = Storage.FromJson<Project>("""{ "name": "X", "techScreens": { "referenceStarts": { "1": 7 } } }""");

        Assert.Equal(new TechScreenStyle().BoxBack, project.TechScreens.Style.BoxBack);
        Assert.Equal(7, project.TechScreens.ReferenceStarts[1]);
    }
}
