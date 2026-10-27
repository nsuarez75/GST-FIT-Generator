using GstFitGenerator.Core;

namespace GstFitGenerator.Tests;

public class PasteTests
{
    [Fact]
    public void Filas_con_el_orden_de_la_tabla()
    {
        var text = "FeatureReferenceClient\tTipo\tLocation\tFeatureId\tCritical\r\n" +
                   "W7\t1\t2\t10007\tFalse\t1000FIX01\t1000R1\t1\t1000FIX02\t1000R2\t2\r\n" +
                   "SEAM1\t90\t1\t9000\tTrue\r\n";

        var features = Paste.Parse(text);

        Assert.Equal(2, features.Count);
        Assert.Equal(("W7", 1, 2, 10007L, false), (features[0].ClientReference, features[0].Type, features[0].Location, features[0].FeatureId, features[0].Critical));
        Assert.Equal(["1000R1", "1000R2"], features[0].Fixtures.Select(f => f.LayoutName));
        Assert.Equal("2", features[0].Fixtures[1].Working);
        Assert.True(features[1].Critical);
        Assert.Empty(features[1].Fixtures);
    }

    [Fact]
    public void Filas_copiadas_de_una_plantilla()
    {
        var cells = new string[22];
        Array.Fill(cells, "");
        (cells[0], cells[1], cells[2]) = ("PLT", "L01", "1");
        (cells[3], cells[4], cells[5]) = ("1000FIX01", "1000R1", "1");
        (cells[15], cells[16], cells[17], cells[18], cells[19]) = ("W8", "Soldadura |Cordón", "1", "10008", "False");
        cells[21] = "1";

        var feature = Assert.Single(Paste.Parse(string.Join('\t', cells)));

        Assert.Equal(("W8", 1, 1, 10008L), (feature.ClientReference, feature.Type, feature.Location, feature.FeatureId));
        Assert.Equal("1000FIX01", Assert.Single(feature.Fixtures).Fixture);
    }

    [Fact]
    public void Texto_vacio_no_crea_nada() => Assert.Empty(Paste.Parse("\r\n  \n"));
}
