// Guardado en disco: proyecto (.fitproj), catálogo de tipos de feature (.fitcat) y ajustes de la app. Todo en JSON.
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GstFitGenerator.Core;

public static class Storage
{
    public const string ProjectExtension = ".fitproj";
    public const string CatalogExtension = ".fitcat";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        // Nombres como ++1000+FIX01 o Process | Detail se guardan legibles, sin escapar.
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string ToJson<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T FromJson<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, Options) ?? throw new InvalidDataException(L.T("El archivo está vacío."));

    // Copia profunda pasando por JSON; la usa deshacer/rehacer.
    public static T Clone<T>(T value) => FromJson<T>(ToJson(value));

    public static void SaveProject(Project project, string path) => WriteAtomic(path, ToJson(project));

    public static Project LoadProject(string path) => Read<Project>(path);

    public static void SaveCatalog(Catalog catalog, string path) => WriteAtomic(path, ToJson(catalog));

    public static Catalog LoadCatalog(string path) => File.Exists(path) ? Read<Catalog>(path) : new Catalog();

    public static AppSettings LoadSettings(string path)
    {
        try
        {
            return File.Exists(path) ? Read<AppSettings>(path) : new AppSettings();
        }
        catch (Exception e) when (e is IOException or JsonException or InvalidDataException or UnauthorizedAccessException)
        {
            return new AppSettings();
        }
    }

    public static void SaveSettings(AppSettings settings, string path) => WriteAtomic(path, ToJson(settings));

    // Carpeta de datos de la app: %APPDATA%\GST-FIT-Generator. Ahí viven el catálogo común y los ajustes.
    public static string AppDataFolder() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GST-FIT-Generator");

    private static T Read<T>(string path)
    {
        try
        {
            return FromJson<T>(File.ReadAllText(path));
        }
        catch (JsonException e)
        {
            throw new InvalidDataException(L.T("{0} no tiene un formato válido: {1}", Path.GetFileName(path), e.Message), e);
        }
    }

    // Se escribe en un temporal y se renombra, para no dejar un archivo a medias si algo falla.
    private static void WriteAtomic(string path, string content)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
        }
        var temp = path + ".tmp";
        File.WriteAllText(temp, content);
        File.Move(temp, path, overwrite: true);
    }
}
