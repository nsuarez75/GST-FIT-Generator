using GstFitGenerator.Core;

namespace GstFitGenerator.Tests;

public class EditingTests
{
    private static string Order(Project project) =>
        string.Join(" ", Layout.Rows(project).Select(r => r.Feature.IsReserve ? $"R{r.Technology}" : r.Feature.ClientReference));

    [Fact]
    public void La_feature_nueva_va_detras_de_la_seleccionada_y_copia_fixtures_tipo_y_location()
    {
        var project = FakeProject.Create();
        var reference = project.References[0];
        var w1 = FakeProject.Find(project, "W1");

        var nueva = Editing.AddAfter(reference, w1);
        nueva.ClientReference = "W1B";

        Assert.Equal("W1 W1B W2_01 W3 MARCA CAM", Order(project));
        Assert.Equal(1, nueva.Type);
        Assert.Equal(1, nueva.Location);
        Assert.Equal(["1000R1", "1000R2"], nueva.Fixtures.Select(f => f.LayoutName));
        Assert.NotSame(w1.Fixtures[0], nueva.Fixtures[0]);
        Assert.Null(nueva.FeatureId);
    }

    [Fact]
    public void Cambiar_a_un_tipo_de_la_misma_tecnologia_no_mueve_nada()
    {
        var project = FakeProject.Create();
        var w1 = FakeProject.Find(project, "W1");

        Assert.False(Editing.ChangeType(project, project.References[0], w1, 2, null));
        Assert.Equal("W1 W2_01 W3 MARCA CAM", Order(project));
    }

    [Fact]
    public void Cambiar_de_tecnologia_lleva_la_feature_al_final_de_su_nuevo_bloque()
    {
        var project = FakeProject.Create();
        project.References[0].Features.Add(FakeProject.Make("MARCA2", 71, 50001));
        var w1 = FakeProject.Find(project, "W1");

        Assert.True(Editing.ChangeType(project, project.References[0], w1, 71, null));
        Assert.Equal("W2_01 W3 MARCA MARCA2 W1 CAM", Order(project));
    }

    [Fact]
    public void Una_reserva_convertida_en_tipo_de_su_tecnologia_conserva_el_pointer()
    {
        var project = FakeProject.Create();
        var reference = project.References[0];
        var reserva = new Feature { ReserveTechnology = 2 };
        reference.Features.Insert(2, reserva);
        var antes = Layout.Rows(project, reference).First(r => r.Feature == reserva).Offset;

        Assert.False(Editing.ChangeType(project, reference, reserva, 1, null));
        reserva.ClientReference = "W9";

        Assert.False(reserva.IsReserve);
        Assert.Equal(antes, Layout.Rows(project, reference).First(r => r.Feature == reserva).Offset);
    }

    [Fact]
    public void Convertir_en_reserva_no_mueve_nada_y_borrar_desplaza_las_siguientes()
    {
        var project = FakeProject.Create();
        var reference = project.References[0];
        var w1 = FakeProject.Find(project, "W1");
        var antes = Layout.Offsets(project);

        Assert.Equal(2, Editing.FollowingInBlock(project, reference, w1));
        Editing.ConvertToReserve(project, w1);

        Assert.True(w1.IsReserve);
        Assert.Equal(2, w1.ReserveTechnology);
        Assert.Empty(w1.Fixtures);
        Assert.Equal(antes, Layout.Offsets(project));

        Editing.Delete(reference, w1);
        Assert.Equal("0102001", Layout.Rows(project, reference).First(r => r.Feature.ClientReference == "W2_01").Offset);
    }

    [Fact]
    public void Anadir_reservas_las_pone_al_final_de_su_tecnologia_sin_mover_nada()
    {
        var project = FakeProject.Create();
        var reference = project.References[0];
        var antes = Layout.Offsets(project);

        var reserves = Editing.AddReserves(project, reference, 2, 3);

        Assert.Equal(3, reserves.Count);
        Assert.All(reserves, r => Assert.Equal(2, r.ReserveTechnology));
        var rows = Layout.Rows(project, reference);
        Assert.Equal(["0102004", "0102005", "0102006"], reserves.Select(r => rows.Single(x => x.Feature == r).Offset));
        Assert.All(antes, o => Assert.Equal(o.Value, Layout.Offsets(project)[o.Key]));

        // Una tecnología que aún no tiene features en la referencia: empieza en el pointer 1.
        var nueva = Editing.AddReserves(project, reference, 5, 2);
        Assert.Equal("0105001", Layout.Rows(project, reference).Single(x => x.Feature == nueva[0]).Offset);
    }

    [Theory]
    [InlineData(0, "W3 W1 W2_01")]
    [InlineData(1, "W1 W3 W2_01")]
    [InlineData(2, "W1 W2_01 W3")]
    [InlineData(9, "W1 W2_01 W3")]
    public void Arrastrar_dentro_de_su_tecnologia(int position, string expected)
    {
        var project = FakeProject.Create();
        var w3 = FakeProject.Find(project, "W3");

        Editing.MoveInBlock(project, project.References[0], w3, position);

        Assert.Equal(expected + " MARCA CAM", Order(project));
    }

    [Fact]
    public void Duplicar_referencia_copia_todo_con_otro_numero_y_otros_ids()
    {
        var project = FakeProject.Create();
        project.References[0].Features.Add(new Feature { ReserveTechnology = 3 });

        var copia = Editing.DuplicateReference(project, project.References[0]);

        Assert.NotNull(copia);
        Assert.Equal(2, copia.Number);
        Assert.Equal(project.References[0].Features.Select(f => f.ClientReference), copia.Features.Select(f => f.ClientReference));
        Assert.Equal(1, copia.Features.Count(f => f.IsReserve));
        Assert.Empty(copia.Features.Select(f => f.Id).Intersect(project.References[0].Features.Select(f => f.Id)));
        Assert.Equal("0202001", Layout.Rows(project, copia)[0].Offset);
    }

    [Fact]
    public void No_se_duplica_por_encima_de_32_referencias()
    {
        var project = FakeProject.Create();
        for (var n = 2; n <= Reference.Max; n++)
        {
            project.References.Add(new Reference { Number = n });
        }

        Assert.Null(Editing.DuplicateReference(project, project.References[0]));
    }

    [Theory]
    [InlineData("W69", 10069L)]
    [InlineData("W73_01", 17301L)]
    [InlineData("w21_02", 12102L)]
    [InlineData(" W5 ", 10005L)]
    [InlineData("SEAM1", null)]
    [InlineData("DMC OP22", null)]
    [InlineData("", null)]
    public void Propone_el_FeatureId(string client, long? expected) =>
        Assert.Equal(expected, FeatureIds.Suggest(client));
}
