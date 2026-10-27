// Traducciones al inglés de los textos de la interfaz (ver L.cs).
namespace GstFitGenerator.Core;

public static partial class L
{
    private static readonly Dictionary<string, string> En = new()
    {
        // ---- Archivos
        ["El archivo está vacío."] = "The file is empty.",
        ["{0} no tiene un formato válido: {1}"] = "{0} does not have a valid format: {1}",

        // ---- Importar plantilla
        ["La plantilla no tiene ninguna hoja PLC-Ref."] = "The template has no PLC-Ref sheet.",
        ["Solo se importan las {0} primeras referencias."] = "Only the first {0} references are imported.",
        ["A la hoja «{0}» le falta la columna «{1}»."] = "The «{0}» sheet is missing the «{1}» column.",

        // ---- Avisos
        ["Hay {0} referencias; el FB admite como máximo {1}."] = "There are {0} references; the FB allows at most {1}.",
        ["El tipo {0} tiene la tecnología {1}; tiene que estar entre 0 y {2}."] = "Type {0} has technology {1}; it must be between 0 and {2}.",
        ["Ref{0} · {1}: no tiene tipo."] = "Ref{0} · {1}: has no type.",
        ["Ref{0} · {1}: el tipo {2} no está en el catálogo."] = "Ref{0} · {1}: type {2} is not in the catalog.",
        ["Ref{0} · {1}: su tipo no tiene tecnología asignada; cuenta como tecnología 0."] = "Ref{0} · {1}: its type has no technology assigned; it counts as technology 0.",
        ["Ref{0} · {1}: no tiene FeatureId."] = "Ref{0} · {1}: has no FeatureId.",
        ["Ref{0} · {1}: el FeatureId {2} está repetido."] = "Ref{0} · {1}: FeatureId {2} is duplicated.",
        ["Ref{0} · {1}: no tiene ningún LayoutName; no saldrá en el FIT ni en el PLC."] = "Ref{0} · {1}: has no LayoutName; it will not appear in the FIT or in the PLC.",
        ["Ref{0} · {1}: la location {2} no existe."] = "Ref{0} · {1}: location {2} does not exist.",
    };
}
