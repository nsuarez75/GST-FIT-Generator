using GstFitGenerator.Core;

namespace GstFitGenerator.Tests;

public class LayoutTests
{
    private static string Summary(Project project) =>
        string.Join(" ", Layout.Rows(project).Select(r => $"{(r.Feature.IsReserve ? "R" : r.Feature.ClientReference)}={r.Offset}/{r.MainPointer}"));

    [Fact]
    public void Agrupa_por_tecnologia_y_respeta_el_orden_de_la_lista()
    {
        var project = FakeProject.Create();

        Assert.Equal("W1=0102001/1 W2_01=0102002/2 W3=0102003/3 MARCA=0103001/4 CAM=0110001/5", Summary(project));
    }

    [Fact]
    public void La_reserva_ocupa_su_pointer()
    {
        var project = FakeProject.Create();
        var reference = project.References[0];
        reference.Features.Insert(2, new Feature { ReserveTechnology = 2 });

        Assert.Equal("W1=0102001/1 R=0102002/2 W2_01=0102003/3 W3=0102004/4 MARCA=0103001/5 CAM=0110001/6", Summary(project));
    }

    [Fact]
    public void Un_tipo_sin_tecnologia_cuenta_como_tecnologia_0()
    {
        var project = FakeProject.Create();
        project.TypeTechnologies.Remove(90);

        Assert.StartsWith("CAM=0100001/1 W1=0102001/2", Summary(project));
    }

    [Fact]
    public void Avisa_de_los_offsets_que_cambian_desde_la_ultima_generacion()
    {
        var project = FakeProject.Create();
        project.LastOffsets = Layout.Offsets(project);
        Assert.Empty(Layout.ChangesSinceLastGeneration(project));

        var reference = project.References[0];
        var w1 = FakeProject.Find(project, "W1");
        Editing.Delete(reference, w1);

        var changes = Layout.ChangesSinceLastGeneration(project);
        Assert.Equal(["0102001->", "0102002->0102001", "0102003->0102002"], changes.Select(c => $"{c.Before}->{c.After}"));
        Assert.Null(changes[0].Feature);
    }
}
