// Modelo de datos de un proyecto y del catálogo de tipos de feature. Es lo que se guarda en .fitproj y .fitcat.
namespace GstFitGenerator.Core;

public sealed class Project
{
    // Da nombre a los Excel generados (ABC_XYZ -> ABC_XYZ_FIT_rev3_20260115.xlsx).
    public string Name { get; set; } = "";
    public string Plant { get; set; } = "";
    public string Line { get; set; } = "";
    // Número de revisión de la plantilla (el rev del nombre del Excel); lo pone el usuario a mano.
    public int Revision { get; set; }
    // Tecnologías de la 0 a la 10 con su nombre (lista CO_Traceability_Tech del HMI).
    public List<Technology> Technologies { get; set; } = Technology.Defaults();
    // Tecnología de cada tipo de feature del catálogo (número de tipo -> número de tecnología).
    public Dictionary<int, int> TypeTechnologies { get; set; } = [];
    public List<Location> Locations { get; set; } = [];
    public List<Reference> References { get; set; } = [];
    public List<AuxFeature> Auxiliaries { get; set; } = [];
    // Parte común de los nombres de pantalla: CO_0000_000 -> CO_0000_000_Traceability_01…24.
    public string ScreenPrefix { get; set; } = "";
    public List<Screen> Screens { get; set; } = Screen.Defaults();
    // Pantallas de techs del HMI: dónde empieza cada referencia, las reservas y los nombres cortos.
    public TechScreenSettings TechScreens { get; set; } = new();
    public List<VersionEntry> Versions { get; set; } = [];
    // Carpeta donde se escribe lo generado; se recuerda por proyecto.
    public string? OutputFolder { get; set; }
    // Offset de cada feature (por su Id) en la última generación, para avisar de lo que se mueve.
    public Dictionary<string, string>? LastOffsets { get; set; }

    public Reference? FindReference(int number) => References.FirstOrDefault(r => r.Number == number);

    public Location? FindLocation(int number) => Locations.FirstOrDefault(l => l.Number == number);

    public string TechnologyName(int number) => Technologies.FirstOrDefault(t => t.Number == number)?.Name ?? "";

    // Tecnología de una feature: la de su reserva o la de su tipo. Null si su tipo no tiene tecnología asignada.
    public int? TechnologyOf(Feature feature) =>
        feature.ReserveTechnology ?? (feature.Type is int type && TypeTechnologies.TryGetValue(type, out var tech) ? tech : null);
}

public sealed class Technology
{
    public const int Max = 10;

    public int Number { get; set; }
    public string Name { get; set; } = "";

    public static List<Technology> Defaults() =>
        Enumerable.Range(0, Max + 1).Select(n => new Technology { Number = n, Name = n == 0 ? "No technology" : "Spare" }).ToList();
}

public sealed class Location
{
    public string Name { get; set; } = "";
    public int Number { get; set; }
}

public sealed class Reference
{
    public const int Max = 32;

    // PartReference: 1, 2…
    public int Number { get; set; }
    // En el orden de la lista; los pointers se calculan a partir de él (ver Layout).
    public List<Feature> Features { get; set; } = [];
}

public sealed class Feature
{
    // Identifica la feature aunque cambie todo lo demás; sirve para comparar con la última generación.
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public List<FixtureUse> Fixtures { get; set; } = [];
    public string ClientReference { get; set; } = "";
    // Número del tipo en el catálogo. Null en las reservas.
    public int? Type { get; set; }
    // Si tiene valor, la feature es una reserva de esa tecnología.
    public int? ReserveTechnology { get; set; }
    public int? Location { get; set; }
    public long? FeatureId { get; set; }
    public bool Critical { get; set; }

    public bool IsReserve => ReserveTechnology is not null;
}

// Un utillaje donde se hace la feature: Fixture, LayoutName (Asset) y Working.
public sealed class FixtureUse
{
    public string Fixture { get; set; } = "";
    public string LayoutName { get; set; } = "";
    public string Working { get; set; } = "";
}

// Feature auxiliar (hoja PLC-Aux): botoneras y HMIs que no pertenecen a ninguna referencia.
public sealed class AuxFeature
{
    public string Asset { get; set; } = "";
    public string Working { get; set; } = "";
    public long? FeatureId { get; set; }
}

public sealed class Screen
{
    public const int Count = 24;

    public string Description { get; set; } = "";

    public static List<Screen> Defaults() => Enumerable.Range(0, Count).Select(_ => new Screen()).ToList();

    // El prefijo es el nombre de la primera pantalla sin _Traceability_01 y su número del final aumenta en uno
    // en cada pantalla, como en TIA: CO_0000_100 -> CO_0000_100_Traceability_01, CO_0000_101_Traceability_02…
    public static string Name(string prefix, int number) => $"{Prefix(prefix, number)}_Traceability_{number:00}";

    // Número de la pantalla en TIA: el del final del prefijo más number - 1. Null si el prefijo no acaba en número.
    public static int? HmiNumber(string prefix, int number) =>
        SplitPrefix(prefix) is var (_, first, _) ? first + number - 1 : null;

    private static string Prefix(string prefix, int number) =>
        SplitPrefix(prefix) is var (head, first, digits)
            ? head + (first + number - 1).ToString(new string('0', digits), System.Globalization.CultureInfo.InvariantCulture)
            : prefix;

    // CO_0000_100 -> ("CO_0000_", 100, 3).
    private static (string Head, int First, int Digits)? SplitPrefix(string prefix)
    {
        var start = prefix.Length;
        while (start > 0 && char.IsAsciiDigit(prefix[start - 1]))
        {
            start--;
        }
        var digits = prefix.Length - start;
        if (digits == 0 || digits > 9 || start == 0 || prefix[start - 1] != '_')
        {
            return null;
        }
        return (prefix[..start], int.Parse(prefix[start..], System.Globalization.CultureInfo.InvariantCulture), digits);
    }
}

public sealed class TechScreenSettings
{
    // Pantalla de inicio (1…24) de cada referencia, por número de referencia. La que no está no sale en pantallas.
    public Dictionary<int, int> ReferenceStarts { get; set; } = [];
    // Pantalla de inicio de las reservas de todas las referencias. Null: las reservas no salen en pantallas.
    public int? ReservesStart { get; set; }
    // Nombre corto de cada Asset o Fixture (1000R1 -> R10.1) para los textos de las pantallas.
    public Dictionary<string, string> ShortNames { get; set; } = [];

    public string ShortName(string name) => ShortNames.TryGetValue(name, out var s) && s.Length > 0 ? s : name;

    public TechScreenStyle Style { get; set; } = new();
}

// Aspecto de las pantallas de techs. Colores en #RRGGBB; tamaños en píxeles de la pantalla del HMI.
public sealed class TechScreenStyle
{
    public const int MinFontSize = 8;
    public const int MaxFontSize = 30;
    public const int MaxRowHeight = 80;
    public const int MaxBoxGap = 60;

    public string BoxBack { get; set; } = "#EEF1F5";
    public string BoxBorder { get; set; } = "#B0BAC8";
    public string TitleBack { get; set; } = "#D6DEE8";
    public string TitleFore { get; set; } = "#232D3C";
    public string LabelFore { get; set; } = "#31344A";
    public int TitleFontSize { get; set; } = 15;
    public int LabelFontSize { get; set; } = 14;
    // Distancia entre filas; como mínimo el alto del faceplate más 1.
    public int RowHeight { get; set; } = 40;
    public int BoxGap { get; set; } = 12;

    // Alto de la franja del título y de los textos de fila, según su letra.
    public int TitleHeight => TitleFontSize + 13;
    public int LabelHeight => LabelFontSize + 11;
    // Ancho aproximado de un carácter del texto de fila (Tahoma negrita).
    public int CharWidth => (int)Math.Ceiling(LabelFontSize * 0.64);
}

public static class TechScreenColors
{
    // #RRGGBB (o RRGGBB) -> componentes. Lo que no se entiende es negro.
    public static (byte R, byte G, byte B) Parse(string hex)
    {
        var h = hex.Trim().TrimStart('#');
        return h.Length == 6 && int.TryParse(h, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var v)
            ? ((byte)(v >> 16), (byte)(v >> 8), (byte)v)
            : ((byte)0, (byte)0, (byte)0);
    }

    public static string Format(byte r, byte g, byte b) => $"#{r:X2}{g:X2}{b:X2}";
}

public sealed class VersionEntry
{
    public DateOnly Date { get; set; }
    public string Description { get; set; } = "";
}

// Catálogo de tipos de feature, común a todos los proyectos (hoja Feature Type Table).
public sealed class Catalog
{
    public List<FeatureType> Types { get; set; } = [];

    public FeatureType? Find(int number) => Types.FirstOrDefault(t => t.Number == number);
}

public sealed class FeatureType
{
    public int Number { get; set; }
    public string Process { get; set; } = "";
    public string Detail { get; set; } = "";

    // Texto de la columna Process | FeatureType, igual que la fórmula de la plantilla: B & " |" & C.
    public string Display => $"{Process} |{Detail}";
}

// Ajustes de la app (no del proyecto): idioma, tema y último proyecto abierto.
public sealed class AppSettings
{
    public const string ThemeSystem = "system";
    public const string ThemeLight = "light";
    public const string ThemeDark = "dark";

    public string Theme { get; set; } = ThemeSystem;
    // Vacío: el idioma de Windows.
    public string? Language { get; set; }
    public string? LastProject { get; set; }
}
