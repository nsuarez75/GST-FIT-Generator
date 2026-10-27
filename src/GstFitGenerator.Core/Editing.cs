// Cambios en las features de una referencia que afectan al orden (y por tanto a los pointers).
// Los pointers solo se mueven cuando el usuario intercala, arrastra, borra o cambia de tecnología algo.
namespace GstFitGenerator.Core;

public static class Editing
{
    // Feature nueva justo detrás de la seleccionada, con sus fixtures, su tipo y su location.
    // Sin selección, va al final de la lista.
    public static Feature AddAfter(Reference reference, Feature? selected)
    {
        var feature = new Feature();
        if (selected is not null)
        {
            feature.Fixtures = selected.Fixtures.Select(Copy).ToList();
            feature.Type = selected.Type;
            feature.ReserveTechnology = selected.ReserveTechnology;
            feature.Location = selected.Location;
        }
        var index = selected is null ? -1 : reference.Features.IndexOf(selected);
        reference.Features.Insert(index < 0 ? reference.Features.Count : index + 1, feature);
        return feature;
    }

    // Cambia el tipo (o lo convierte en reserva de una tecnología). Si cambia de tecnología, la feature pasa
    // al final del bloque de la nueva tecnología. Devuelve true si se ha movido.
    public static bool ChangeType(Project project, Reference reference, Feature feature, int? type, int? reserveTechnology)
    {
        var before = Layout.TechnologyOf(project, feature);
        feature.Type = reserveTechnology is null ? type : null;
        feature.ReserveTechnology = reserveTechnology;
        var after = Layout.TechnologyOf(project, feature);
        if (before == after)
        {
            return false;
        }
        reference.Features.Remove(feature);
        var last = reference.Features.FindLastIndex(f => Layout.TechnologyOf(project, f) == after);
        reference.Features.Insert(last < 0 ? reference.Features.Count : last + 1, feature);
        return true;
    }

    // Features que cambian de pointer si se quita esta: las que van detrás en su tecnología.
    public static int FollowingInBlock(Project project, Reference reference, Feature feature)
    {
        var technology = Layout.TechnologyOf(project, feature);
        var block = reference.Features.Where(f => Layout.TechnologyOf(project, f) == technology).ToList();
        return block.Count - block.IndexOf(feature) - 1;
    }

    // Convertir en reserva: conserva su sitio y su tecnología, no se mueve nada.
    public static void ConvertToReserve(Project project, Feature feature)
    {
        feature.ReserveTechnology = Layout.TechnologyOf(project, feature);
        feature.Type = null;
        feature.ClientReference = "";
        feature.FeatureId = null;
        feature.Critical = false;
        feature.Fixtures.Clear();
    }

    public static void Delete(Reference reference, Feature feature) => reference.Features.Remove(feature);

    // Arrastrar dentro de su tecnología: la deja en la posición indicada del bloque (0 = primera).
    public static void MoveInBlock(Project project, Reference reference, Feature feature, int position)
    {
        var technology = Layout.TechnologyOf(project, feature);
        var block = reference.Features.Where(f => Layout.TechnologyOf(project, f) == technology).ToList();
        block.Remove(feature);
        position = Math.Clamp(position, 0, block.Count);
        reference.Features.Remove(feature);
        var index = position < block.Count
            ? reference.Features.IndexOf(block[position])
            : reference.Features.IndexOf(block[^1]) + 1;
        reference.Features.Insert(block.Count == 0 ? reference.Features.Count : index, feature);
    }

    // Referencia nueva con el número siguiente y una copia de todas las features, reservas incluidas.
    public static Reference? DuplicateReference(Project project, Reference reference)
    {
        if (project.References.Count >= Reference.Max)
        {
            return null;
        }
        var copy = new Reference
        {
            Number = project.References.Max(r => r.Number) + 1,
            Features = reference.Features.Select(Copy).ToList(),
        };
        project.References.Add(copy);
        return copy;
    }

    public static Feature Copy(Feature feature)
    {
        var copy = Storage.Clone(feature);
        copy.Id = Guid.NewGuid().ToString("N");
        return copy;
    }

    public static FixtureUse Copy(FixtureUse fixture) =>
        new() { Fixture = fixture.Fixture, LayoutName = fixture.LayoutName, Working = fixture.Working };
}
