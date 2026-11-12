using System.Xml.Linq;
using GstFitGenerator.Core;

namespace GstFitGenerator.Tests;

// Retoques a mano de los recuadros de las pantallas de techs.
public class TechScreenEditTests
{
    private static Project Project()
    {
        var project = FakeProject.Create();
        project.ScreenPrefix = "CO_0000_100";
        project.TechScreens.ReferenceStarts[1] = 7;
        // Un segundo Asset en 1000FIX01, para poder partirlo.
        project.References[0].Features.Add(FakeProject.Make("W8", 1, 10008, FakeProject.Fx("1000FIX01", "1000R2")));
        return project;
    }

    private static (TechScreenPage Page, TechScreenBox Box) Find(Project project, string title)
    {
        var plan = TechScreens.Plan(project);
        var page = plan.Pages.First(p => p.Boxes.Any(b => b.Title == title));
        return (page, page.Boxes.First(b => b.Title == title));
    }

    [Fact]
    public void Move_Box_SnapsToGridAndPushesOthers()
    {
        var project = Project();
        var (page, box) = Find(project, "3000FIX01");

        TechScreenEdits.Move(project, page, box, 803, 405);

        var plan = TechScreens.Plan(project);
        var moved = plan.Pages[0].Boxes.Single(b => b.Title == "3000FIX01");
        Assert.Equal((800, 408, true), (moved.Left, moved.Top, moved.IsManual));
        // El automático se coloca sin pisarlo.
        var others = plan.Pages[0].Boxes.Where(b => b != moved).ToList();
        Assert.All(others, o => Assert.True(o.Right <= moved.Left || o.Left >= moved.Right || o.Bottom <= moved.Top || o.Top >= moved.Bottom));
        Assert.Empty(plan.Warnings);
    }

    [Fact]
    public void ToScreen_NewScreen_MovesBox()
    {
        var project = Project();
        var (page, box) = Find(project, "4000FIX01");

        TechScreenEdits.ToScreen(project, page, box, 1);

        var plan = TechScreens.Plan(project);
        Assert.Equal([7, 8], plan.Pages.Select(p => p.Number));
        Assert.Equal("4000FIX01", Assert.Single(plan.Pages[1].Boxes).Title);
        Assert.Equal((project.TechScreens.Style.AreaLeft, project.TechScreens.Style.AreaTop), (plan.Pages[1].Boxes[0].Left, plan.Pages[1].Boxes[0].Top));
    }

    [Fact]
    public void SetRows_RowsPerColumn_ReshapesBox()
    {
        var project = Project();
        var (page, box) = Find(project, "1000FIX01");

        TechScreenEdits.SetRows(project, page, box, 2);

        var changed = Find(project, "1000FIX01").Box;
        // 1000R1 tiene 3 features: dos columnas (2 + 1); 1000R2, una.
        Assert.Equal(3, changed.Rows.Select(r => r.Left).Distinct().Count());
        Assert.Equal(2, changed.RowsPerColumn);
        Assert.Equal((box.Left, box.Top), (changed.Left, changed.Top));
    }

    [Fact]
    public void Split_BetweenAssets_CreatesMovablePart()
    {
        var project = Project();
        var (page, box) = Find(project, "1000FIX01");
        Assert.Equal(["1000R1", "1000R2"], box.Assets);

        TechScreenEdits.Split(project, page, box, "1000R2");

        var plan = TechScreens.Plan(project);
        var first = plan.Pages[0].Boxes.Single(b => b.Title == "1000FIX01");
        var second = plan.Pages[0].Boxes.Single(b => b.Title == "1000FIX01 (cont.)");
        Assert.Equal(["1000R1"], first.Assets);
        Assert.Equal(1, second.Part);
        Assert.Equal(["1000R2"], second.Assets);

        TechScreenEdits.ToScreen(project, plan.Pages[0], second, 1);
        Assert.Equal("1000FIX01 (cont.)", Assert.Single(TechScreens.Plan(project).Pages[1].Boxes).Title);
    }

    [Fact]
    public void Apply_FixtureRenamed_KeepsEditByFeatures()
    {
        var project = Project();
        var (page, box) = Find(project, "3000FIX01");
        TechScreenEdits.Move(project, page, box, 1200, 600);

        project.References[0].Features.Single(f => f.ClientReference == "MARCA").Fixtures[0].Fixture = "3000FIX09";

        var plan = TechScreens.Plan(project);
        var renamed = plan.Pages[0].Boxes.Single(b => b.Title == "3000FIX09");
        Assert.Equal((1200, 600), (renamed.Left, renamed.Top));
        Assert.Empty(plan.Warnings);
    }

    [Fact]
    public void Apply_EditWithoutBox_WarnsAndSkips()
    {
        var project = Project();
        var (page, box) = Find(project, "4000FIX01");
        TechScreenEdits.Move(project, page, box, 1200, 600);

        project.References[0].Features.RemoveAll(f => f.ClientReference == "CAM");

        Assert.Contains(TechScreens.Plan(project).Warnings, w => w.Contains("4000FIX01"));
    }

    [Fact]
    public void Apply_OverlapOrOutside_Warns()
    {
        // Los automáticos se apartan solos: solo dos movidos a mano pueden pisarse.
        var project = Project();
        var (page, box) = Find(project, "3000FIX01");
        TechScreenEdits.Move(project, page, box, 400, 400);
        var (page2, box2) = Find(project, "4000FIX01");
        TechScreenEdits.Move(project, page2, box2, 400, 400);
        var (page3, box3) = Find(project, "1000FIX01");
        TechScreenEdits.Move(project, page3, box3, 1900, 900);

        var warnings = TechScreens.Plan(project).Warnings;
        Assert.Contains(warnings, w => w.Contains("pisa"));
        Assert.Contains(warnings, w => w.Contains("se sale"));
    }

    [Fact]
    public void AutoPlace_EditedBox_RemovesEdits()
    {
        var project = Project();
        var auto = Find(project, "3000FIX01").Box;
        var (page, box) = Find(project, "3000FIX01");
        TechScreenEdits.Move(project, page, box, 1200, 600);

        var (page2, moved) = Find(project, "3000FIX01");
        TechScreenEdits.Reset(project, page2, moved);

        var back = Find(project, "3000FIX01").Box;
        Assert.Equal((auto.Left, auto.Top, false), (back.Left, back.Top, back.IsManual));
        Assert.Empty(project.TechScreens.BoxEdits);
    }

    [Fact]
    public void Resize_Scale_ScalesBoxAndXml()
    {
        var project = Project();
        var (page, box) = Find(project, "1000FIX01");

        TechScreenEdits.Resize(project, page, box, 50);

        var plan = TechScreens.Plan(project);
        var small = plan.Pages[0].Boxes.Single(b => b.Title == "1000FIX01");
        Assert.Equal((50, box.Left, box.Top, box.RowsPerColumn), (small.Scale, small.Left, small.Top, small.RowsPerColumn));
        // Los márgenes interiores no se escalan: algo más de la mitad.
        Assert.True(small.Width < box.Width * 0.7 && small.Height < box.Height * 0.7);
        Assert.Equal((7, 17, 18), (small.Style.LabelFontSize, small.Style.FaceplateWidth, small.Style.FaceplateHeight));
        var doc = XDocument.Parse(TechScreenWriter.Write(project, plan, plan.Pages[0]));
        var faceplate = doc.Descendants("Hmi.Screen.FaceplateInstance")
            .First(f => f.Element("AttributeList")!.Element("FaceplateTypeName")!.Value.Contains("TechLayout"))
            .Element("AttributeList")!;
        Assert.Equal(("17", "18"), (faceplate.Element("Width")!.Value, faceplate.Element("Height")!.Value));
        // Los demás recuadros no cambian de tamaño.
        Assert.All(plan.Pages[0].Boxes.Where(b => b != small), b => Assert.Equal(100, b.Scale));

        TechScreenEdits.Resize(project, plan.Pages[0], small, 500);
        Assert.Equal(TechScreens.MaxScale, Find(project, "1000FIX01").Box.Scale);
    }

    [Fact]
    public void Plan_CanvasAndFaceplateSize_ChangesLayoutAndXml()
    {
        var project = Project();
        project.TechScreens.Style.MarginLeft = 200;
        project.TechScreens.Style.MarginTop = 150;
        project.TechScreens.Style.FaceplateWidth = 28;

        var plan = TechScreens.Plan(project);
        var page = plan.Pages[0];

        Assert.Equal((200, 150), (page.Boxes[0].Left, page.Boxes[0].Top));
        var doc = XDocument.Parse(TechScreenWriter.Write(project, plan, page));
        var faceplate = doc.Descendants("Hmi.Screen.FaceplateInstance")
            .First(f => f.Element("AttributeList")!.Element("FaceplateTypeName")!.Value.Contains("TechLayout"))
            .Element("AttributeList")!;
        Assert.Equal(("28", "29"), (faceplate.Element("Width")!.Value, faceplate.Element("Height")!.Value));
    }
}
