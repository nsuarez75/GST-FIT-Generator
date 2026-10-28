// Pantallas de techs del HMI: reparte las features de cada referencia (y las reservas) en recuadros por Fixture,
// con una columna por Asset, y los recuadros en pantallas desde la pantalla de inicio de cada referencia.
// Calcula posiciones en píxeles de la pantalla de 1920x966; la vista previa y el XML usan las mismas.
using System.Globalization;

namespace GstFitGenerator.Core;

// Una fila: el texto y el faceplate TechLayout de una feature o reserva.
public sealed record TechScreenRow(string Text, int Reference, int Technology, int Pointer, int MainPointer, int Left, int Top, int LabelWidth)
{
    public int FaceplateLeft => Left + LabelWidth + TechScreens.LabelGap;
}

public sealed record TechScreenBox(string Title, int Left, int Top, int Width, int Height, IReadOnlyList<TechScreenRow> Rows);

public sealed class TechScreenPage
{
    // Número de la pantalla (1…24), como en Screen Names.
    public int Number { get; init; }
    // Referencia de la pantalla; 0 en las de reservas.
    public int Reference { get; init; }
    public bool IsReserves => Reference == 0;
    public List<TechScreenBox> Boxes { get; } = [];

    // Texto para la descripción de Screen Names cuando está vacía.
    public string DefaultDescription => IsReserves ? "Reservas" : $"Ref{Reference} Techs";
}

public sealed class TechScreenPlan
{
    public List<TechScreenPage> Pages { get; } = [];
    public List<string> Warnings { get; } = [];
    // Con errores no se pueden generar las pantallas.
    public List<string> Errors { get; } = [];

    public TechScreenPage? Find(int number) => Pages.FirstOrDefault(p => p.Number == number);

    // Pantalla anterior o siguiente del mismo contenido (misma referencia, o reservas), para los botones.
    public TechScreenPage? Neighbour(TechScreenPage page, int step) =>
        Find(page.Number + step) is { } other && other.Reference == page.Reference ? other : null;
}

public static class TechScreens
{
    // Zona de la pantalla para los recuadros: debajo de la cabecera y por encima del pie de la plantilla.
    public const int AreaLeft = 16;
    public const int AreaTop = 100;
    public const int AreaRight = 1904;
    public const int AreaBottom = 840;

    public const int TitleHeight = 28;
    public const int Padding = 8;
    public const int RowHeight = 40;
    public const int LabelHeight = 25;
    public const int FaceplateWidth = 34;
    public const int FaceplateHeight = 35;
    public const int LabelGap = 6;
    public const int ColumnGap = 16;
    public const int BoxGap = 12;
    // Ancho aproximado de un carácter del texto de fila (Tahoma 14 negrita).
    public const int CharWidth = 9;

    public const string NoFixture = "Sin fixture";

    public static int RowsPerColumn => (AreaBottom - AreaTop - TitleHeight - 2 * Padding) / RowHeight;

    public static TechScreenPlan Plan(Project project)
    {
        var plan = new TechScreenPlan();
        var settings = project.TechScreens;
        var chains = new List<(int Start, int Reference, List<Box> Boxes)>();
        foreach (var reference in project.References.OrderBy(r => r.Number))
        {
            if (settings.ReferenceStarts.TryGetValue(reference.Number, out var start))
            {
                chains.Add((start, reference.Number, ReferenceBoxes(project, reference, plan)));
            }
        }
        if (settings.ReservesStart is int reservesStart)
        {
            chains.Add((reservesStart, 0, ReserveBoxes(project)));
        }

        var nextFree = 1;
        foreach (var (start, reference, boxes) in chains.OrderBy(c => c.Start).ThenBy(c => c.Reference == 0).ThenBy(c => c.Reference))
        {
            var pages = Pack(boxes);
            if (pages.Count == 0)
            {
                continue;
            }
            var name = reference == 0 ? L.T("Reservas") : $"Ref{reference}";
            var first = Math.Max(start, nextFree);
            if (first != start)
            {
                plan.Warnings.Add(L.T("{0}: empieza en la pantalla {1} en vez de en la {2}, porque la {2} ya está ocupada.", name, first, start));
            }
            var last = first + pages.Count - 1;
            if (last > Screen.Count)
            {
                plan.Errors.Add(L.T("{0}: hacen falta pantallas hasta la {1} y solo hay {2}.", name, last, Screen.Count));
            }
            for (var i = 0; i < pages.Count && first + i <= Screen.Count; i++)
            {
                var page = new TechScreenPage { Number = first + i, Reference = reference };
                page.Boxes.AddRange(pages[i]);
                plan.Pages.Add(page);
            }
            nextFree = last + 1;
        }
        return plan;
    }

    // Rellena la descripción de Screen Names de las pantallas usadas que no la tienen. True si ha cambiado algo.
    public static bool FillDescriptions(Project project, TechScreenPlan plan)
    {
        var changed = false;
        foreach (var page in plan.Pages)
        {
            var screen = project.Screens[page.Number - 1];
            if (screen.Description.Length == 0)
            {
                screen.Description = page.DefaultDescription;
                changed = true;
            }
        }
        return changed;
    }


    // ---------------------------------------------------------------- recuadros

    // Recuadro antes de colocarlo: título y sus grupos (cada Asset, o la tecnología en las reservas) con sus filas
    // en orden. Las columnas se forman al colocarlo, según el sitio que haya.
    private sealed record Box(string Title, List<List<Item>> Groups);

    private sealed record Item(string Text, int Reference, int Technology, int Pointer, int MainPointer);

    // Un recuadro por Fixture en el orden de la lista; dentro, un grupo por Asset ordenados por nombre y las filas
    // por Main Pointer. La feature sale una sola vez, en su primer Fixture.
    private static List<Box> ReferenceBoxes(Project project, Reference reference, TechScreenPlan plan)
    {
        var shortNames = project.TechScreens;
        var rows = Layout.Rows(project, reference).Where(r => r.Technology != 0 && !r.Feature.IsReserve).ToList();
        var withoutFixture = rows.Count(r => r.Feature.Fixtures.Count == 0);
        if (withoutFixture > 0)
        {
            plan.Warnings.Add(L.T("Ref{0}: {1} features sin fixture van al recuadro «{2}».", reference.Number, withoutFixture, NoFixture));
        }
        var boxes = new List<Box>();
        foreach (var fixture in rows.GroupBy(r => r.Feature.Fixtures.FirstOrDefault()?.Fixture ?? "").OrderBy(g => g.Min(r => r.MainPointer)))
        {
            var title = fixture.Key.Length > 0 ? shortNames.ShortName(fixture.Key) : NoFixture;
            var groups = fixture
                .GroupBy(r => r.Feature.Fixtures.FirstOrDefault()?.LayoutName ?? "")
                .OrderBy(g => g.Key, NaturalComparer.Instance)
                .Select(asset => asset.OrderBy(r => r.MainPointer)
                    .Select(r => new Item(RowText(shortNames, asset.Key, r.Feature.ClientReference), reference.Number, r.Technology, r.Pointer, r.MainPointer))
                    .ToList())
                .ToList();
            boxes.AddRange(SplitWide(title, groups));
        }
        return boxes;
    }

    private static string RowText(TechScreenSettings settings, string asset, string client) =>
        asset.Length > 0 ? $"{settings.ShortName(asset)} - {client}" : client;

    // Un recuadro por referencia y tecnología con sus reservas; el texto es «Tecnología - Pointer».
    private static List<Box> ReserveBoxes(Project project)
    {
        var boxes = new List<Box>();
        foreach (var reference in project.References.OrderBy(r => r.Number))
        {
            foreach (var tech in Layout.Rows(project, reference).Where(r => r.Feature.IsReserve && r.Technology != 0).GroupBy(r => r.Technology).OrderBy(g => g.Key))
            {
                var name = project.TechnologyName(tech.Key);
                var items = tech.OrderBy(r => r.Pointer)
                    .Select(r => new Item(string.Create(CultureInfo.InvariantCulture, $"{name} - {r.Pointer}"), reference.Number, r.Technology, r.Pointer, r.MainPointer))
                    .ToList();
                boxes.AddRange(SplitWide($"Ref{reference.Number} · {name}", [items]));
            }
        }
        return boxes;
    }

    // Columnas de un recuadro con un máximo de filas por columna: un grupo largo se reparte en varias columnas de
    // alto parecido, una al lado de otra.
    private static List<List<Item>> Columns(Box box, int maxRows) =>
        box.Groups.SelectMany(group =>
        {
            var count = (group.Count + maxRows - 1) / maxRows;
            var perColumn = (group.Count + count - 1) / count;
            return group.Chunk(perColumn).Select(c => c.ToList());
        }).ToList();

    // Un recuadro más ancho que la pantalla, con las columnas más altas posibles, sigue en otro con el título
    // «(cont.)». Se parte entre grupos; un grupo no se parte.
    private static IEnumerable<Box> SplitWide(string title, List<List<Item>> groups)
    {
        var current = new List<List<Item>>();
        var part = 0;
        foreach (var group in groups)
        {
            if (current.Count > 0 && Width(Columns(new Box(title, [.. current, group]), RowsPerColumn)) > AreaRight - AreaLeft)
            {
                yield return new Box(part++ == 0 ? title : $"{title} (cont.)", current);
                current = [];
            }
            current.Add(group);
        }
        if (current.Count > 0)
        {
            yield return new Box(part == 0 ? title : $"{title} (cont.)", current);
        }
    }

    private static int LabelWidth(List<Item> column) => Math.Max(60, column.Max(i => i.Text.Length) * CharWidth + 6);

    private static int ColumnWidth(List<Item> column) => LabelWidth(column) + LabelGap + FaceplateWidth;

    private static int Width(List<List<Item>> columns) =>
        2 * Padding + columns.Sum(ColumnWidth) + ColumnGap * (columns.Count - 1);

    private static int Height(List<List<Item>> columns) =>
        TitleHeight + 2 * Padding + columns.Max(c => c.Count) * RowHeight - (RowHeight - FaceplateHeight);

    // ---------------------------------------------------------------- colocación

    // Columnas más bajas que esto no se prueban para meter un recuadro en un hueco.
    private const int MinRowsPerColumn = 3;

    private sealed record Rect(int Left, int Top, int Width, int Height)
    {
        public int Right => Left + Width;
        public int Bottom => Top + Height;
    }

    // Coloca los recuadros en orden, cada uno en el hueco libre más arriba (y luego más a la izquierda). Si no cabe
    // con las columnas más altas, prueba con columnas más bajas (más anchas); si aun así no cabe, pantalla nueva.
    // Un recuadro no se parte entre pantallas.
    private static List<List<TechScreenBox>> Pack(List<Box> boxes)
    {
        var pages = new List<List<TechScreenBox>>();
        var page = new List<TechScreenBox>();
        foreach (var box in boxes)
        {
            var placed = TryPlace(box, page);
            if (placed is null && page.Count > 0)
            {
                pages.Add(page);
                page = [];
                placed = TryPlace(box, page);
            }
            // En una pantalla vacía siempre se coloca, aunque se salga (un recuadro enorme): mejor verlo que perderlo.
            page.Add(placed ?? Place(box, Columns(box, RowsPerColumn), AreaLeft, AreaTop));
        }
        if (page.Count > 0)
        {
            pages.Add(page);
        }
        return pages;
    }

    private static TechScreenBox? TryPlace(Box box, List<TechScreenBox> page)
    {
        var taken = page.Select(b => new Rect(b.Left, b.Top, b.Width, b.Height)).ToList();
        var maxRows = box.Groups.Max(g => g.Count);
        for (var rows = Math.Min(RowsPerColumn, maxRows); rows >= Math.Min(MinRowsPerColumn, maxRows); rows--)
        {
            var columns = Columns(box, rows);
            var (w, h) = (Width(columns), Height(columns));
            if (FindSpot(taken, w, h) is { } spot)
            {
                return Place(box, columns, spot.Left, spot.Top);
            }
        }
        return null;
    }

    // Hueco libre más arriba y más a la izquierda para un recuadro de w x h. Se prueban la esquina de la zona y,
    // por cada recuadro ya colocado, a su derecha y debajo.
    private static Rect? FindSpot(List<Rect> taken, int w, int h)
    {
        var candidates = new List<(int X, int Y)> { (AreaLeft, AreaTop) };
        foreach (var r in taken)
        {
            candidates.Add((r.Right + BoxGap, r.Top));
            candidates.Add((r.Left, r.Bottom + BoxGap));
            candidates.Add((AreaLeft, r.Bottom + BoxGap));
        }
        foreach (var (x, y) in candidates.Distinct().OrderBy(c => c.Y).ThenBy(c => c.X))
        {
            var spot = new Rect(x, y, w, h);
            if (spot.Right <= AreaRight && spot.Bottom <= AreaBottom && !taken.Any(t => Overlaps(t, spot)))
            {
                return spot;
            }
        }
        return null;
    }

    private static bool Overlaps(Rect a, Rect b) =>
        a.Left < b.Right + BoxGap && b.Left < a.Right + BoxGap && a.Top < b.Bottom + BoxGap && b.Top < a.Bottom + BoxGap;

    private static TechScreenBox Place(Box box, List<List<Item>> columns, int left, int top)
    {
        var rows = new List<TechScreenRow>();
        var x = left + Padding;
        foreach (var column in columns)
        {
            var labelWidth = LabelWidth(column);
            var y = top + TitleHeight + Padding;
            foreach (var item in column)
            {
                rows.Add(new TechScreenRow(item.Text, item.Reference, item.Technology, item.Pointer, item.MainPointer, x, y, labelWidth));
                y += RowHeight;
            }
            x += labelWidth + LabelGap + FaceplateWidth + ColumnGap;
        }
        return new TechScreenBox(box.Title, left, top, Width(columns), Height(columns), rows);
    }
}


// Orden por nombre con los números en su orden: 5000R2 antes que 5000R10.
public sealed class NaturalComparer : IComparer<string>
{
    public static readonly NaturalComparer Instance = new();

    public int Compare(string? a, string? b)
    {
        a ??= "";
        b ??= "";
        int i = 0, j = 0;
        while (i < a.Length && j < b.Length)
        {
            if (char.IsAsciiDigit(a[i]) && char.IsAsciiDigit(b[j]))
            {
                var si = i;
                var sj = j;
                while (i < a.Length && char.IsAsciiDigit(a[i]))
                {
                    i++;
                }
                while (j < b.Length && char.IsAsciiDigit(b[j]))
                {
                    j++;
                }
                var na = a[si..i].TrimStart('0');
                var nb = b[sj..j].TrimStart('0');
                var c = na.Length != nb.Length ? na.Length.CompareTo(nb.Length) : string.CompareOrdinal(na, nb);
                if (c != 0)
                {
                    return c;
                }
            }
            else
            {
                var c = char.ToUpperInvariant(a[i]).CompareTo(char.ToUpperInvariant(b[j]));
                if (c != 0)
                {
                    return c;
                }
                i++;
                j++;
            }
        }
        return (a.Length - i).CompareTo(b.Length - j);
    }
}
