using GstFitGenerator.Core;

namespace GstFitGenerator.Tests;

// Proyecto inventado para los tests: nada sale de datos reales.
internal static class FakeProject
{
    public static Catalog Catalog() => new()
    {
        Types =
        [
            new() { Number = 1, Process = "Soldadura", Detail = "Cordón" },
            new() { Number = 2, Process = "Soldadura", Detail = "Punto" },
            new() { Number = 71, Process = "Marcado", Detail = "Láser" },
            new() { Number = 90, Process = "Inspección", Detail = "" },
        ],
    };

    public static Project Create()
    {
        var project = new Project
        {
            Name = "PRUEBA",
            Plant = "PLT",
            Line = "L01",
            TypeTechnologies = { [1] = 2, [2] = 2, [71] = 3, [90] = 10 },
            Locations = [new() { Name = "Puesto 1", Number = 1 }, new() { Name = "Puesto 2", Number = 2 }],
            ScreenPrefix = "CO_0000_000",
        };
        project.References.Add(new Reference
        {
            Number = 1,
            Features =
            [
                // Desordenadas a propósito: la de marcado va primero en la lista, pero su tecnología (3) va detrás.
                Make("MARCA", 71, 50000, Fx("3000FIX01", "3000M1")),
                Make("W1", 1, 10001, Fx("1000FIX01", "1000R1"), Fx("1000FIX02", "1000R2", "2")),
                Make("W2_01", 2, 10201, Fx("1000FIX01", "1000R1")),
                Make("W3", 1, 10003, Fx("1000FIX01", "1000R1")),
                Make("CAM", 90, 60000, Fx("4000FIX01", "4000V1")),
            ],
        });
        project.Auxiliaries.Add(new AuxFeature { Asset = "Botonera1", Working = "1", FeatureId = 100 });
        return project;
    }

    public static Feature Make(string client, int type, long featureId, params FixtureUse[] fixtures) => new()
    {
        ClientReference = client,
        Type = type,
        FeatureId = featureId,
        Location = 1,
        Fixtures = fixtures.ToList(),
    };

    public static FixtureUse Fx(string fixture, string layoutName, string working = "1") =>
        new() { Fixture = fixture, LayoutName = layoutName, Working = working };

    public static Feature Find(Project project, string client) =>
        project.References.SelectMany(r => r.Features).First(f => f.ClientReference == client);
}
