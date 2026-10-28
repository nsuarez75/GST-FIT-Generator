// Pantallas de techs del HMI: reparte las features de cada referencia (y las reservas) en recuadros por Fixture,
// con una columna por Asset, y los recuadros en pantallas desde la pantalla de inicio de cada referencia.
// Primero van los recuadros retocados a mano (TechBoxEdit) y luego los demás, en los huecos que queden.
// Calcula posiciones en píxeles de la pantalla de 1920x966; la vista previa y el XML usan las mismas.
using System.Globalization;

namespace GstFitGenerator.Core;

// Una fila: el texto y el faceplate TechLayout de una feature o reserva.
public sealed record TechScreenRow(string Text, int Reference, int Technology, int Pointer, int MainPointer, int Left, int Top, int LabelWidth)
{
    public int FaceplateLeft => Left + LabelWidth + TechScreens.LabelGap;
}

public sealed record TechScreenBox(string Title, int Left, int Top, int Width, int Height, IReadOnlyList<TechScreenRow> Rows)
{
    // Para retocarlo: Fixture (o «RefN:tecnología» en reservas), parte y features de todo el Fixture.
    public string Key { get; init; } = "";
    public int Part { get; init; }
    public IReadOnlyList<string> FeatureIds { get; init; } = [];
    // Assets de esta parte, en orden: donde se puede partir.
    public IReadOnlyList<string> Assets { get; init; } = [];
    public int RowsPerColumn { get; init; }
    public bool IsManual { get; init; }
    // Tamaño en % y el estilo con sus letras, faceplates y filas ya escalados: con él se dibuja.
    public int Scale { get; init; } = 100;
    public TechScreenStyle Style { get; init; } = new();

    public int Right => Left + Width;
    public int Bottom => Top + Height;
}

public sealed class TechScreenPage
{
    // Número de la pantalla (1…24), como en Screen Names.
    public int Number { get; init; }
    // Referencia de la pantalla; 0 en las de reservas.
    public int Reference { get; init; }
    // Posición entre las pantallas de su referencia: 0 es la primera.
    public int Index { get; init; }
    public bool IsReserves => Reference == 0;
    // Estilo con el que se ha repartido; el XML y la vista previa lo usan para colores y letras.
    public TechScreenStyle Style { get; init; } = new();
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

    public IEnumerable<TechScreenPage> PagesOf(int reference) => Pages.Where(p => p.Reference == reference);
}

public static class TechScreens
{
    public const int Padding = 8;
    public const int LabelGap = 6;
    public const int ColumnGap = 16;
    // Rejilla a la que se ajusta un recuadro movido a mano.
    public const int Grid = 8;

    public const string NoFixture = "Sin fixture";

    public static int RowsPerColumn(TechScreenStyle s) => Math.Max(1, (s.AreaBottom - s.AreaTop - s.TitleHeight - 2 * Padding) / s.RowHeight);

    // Estilo con los valores dentro de lo que se puede dibujar: letras de 8 a 30, filas más altas que el faceplate,
    // márgenes de 0 a 400.
    public static TechScreenStyle Clamp(TechScreenStyle style)
    {
        var s = new TechScreenStyle
        {
            BoxBack = style.BoxBack,
            BoxBorder = style.BoxBorder,
            TitleBack = style.TitleBack,
            TitleFore = style.TitleFore,
            LabelFore = style.LabelFore,
            TitleFontSize = Math.Clamp(style.TitleFontSize, TechScreenStyle.MinFontSize, TechScreenStyle.MaxFontSize),
            LabelFontSize = Math.Clamp(style.LabelFontSize, TechScreenStyle.MinFontSize, TechScreenStyle.MaxFontSize),
            BoxGap = Math.Clamp(style.BoxGap, 0, TechScreenStyle.MaxBoxGap),
            MarginLeft = Math.Clamp(style.MarginLeft, 0, TechScreenStyle.MaxMargin),
            MarginTop = Math.Clamp(style.MarginTop, 0, TechScreenStyle.MaxMargin),
            MarginRight = Math.Clamp(style.MarginRight, 0, TechScreenStyle.MaxMargin),
            MarginBottom = Math.Clamp(style.MarginBottom, 0, TechScreenStyle.MaxMargin),
            FaceplateWidth = Math.Clamp(style.FaceplateWidth, TechScreenStyle.MinFaceplateWidth, TechScreenStyle.MaxFaceplateWidth),
        };
        s.RowHeight = Math.Clamp(style.RowHeight, s.FaceplateHeight + 1, Math.Max(TechScreenStyle.MaxRowHeight, s.FaceplateHeight + 1));
        return s;
    }

    public const int MinScale = 40;
    public const int MaxScale = 200;

    // Estilo de un recuadro escalado: letras, faceplate y alto de fila en proporción; colores y lienzo, los mismos.
    public static TechScreenStyle Scaled(TechScreenStyle s, int percent)
    {
        if (percent == 100)
        {
            return s;
        }
        int Of(int value) => (int)Math.Round(value * percent / 100.0);
        var scaled = new TechScreenStyle
        {
            BoxBack = s.BoxBack,
            BoxBorder = s.BoxBorder,
            TitleBack = s.TitleBack,
            TitleFore = s.TitleFore,
            LabelFore = s.LabelFore,
            BoxGap = s.BoxGap,
            MarginLeft = s.MarginLeft,
            MarginTop = s.MarginTop,
            MarginRight = s.MarginRight,
            MarginBottom = s.MarginBottom,
            TitleFontSize = Math.Max(6, Of(s.TitleFontSize)),
            LabelFontSize = Math.Max(6, Of(s.LabelFontSize)),
            FaceplateWidth = Math.Max(10, Of(s.FaceplateWidth)),
        };
        scaled.RowHeight = Math.Max(scaled.FaceplateHeight + 1, Of(s.RowHeight));
        return scaled;
    }

    public static TechScreenPlan Plan(Project project)
    {
        var plan = new TechScreenPlan();
        var settings = project.TechScreens;
        var style = Clamp(settings.Style);
        var chains = new List<(int Start, int Reference, List<Box> Boxes)>();
        foreach (var reference in project.References.OrderBy(r => r.Number))
        {
            if (settings.ReferenceStarts.TryGetValue(reference.Number, out var start))
            {
                chains.Add((start, reference.Number, ApplyEdits(project, reference.Number, ReferenceBoxes(project, reference, plan), plan, style)));
            }
        }
        if (settings.ReservesStart is int reservesStart)
        {
            chains.Add((reservesStart, 0, ApplyEdits(project, 0, ReserveBoxes(project), plan, style)));
        }

        var nextFree = 1;
        foreach (var (start, reference, boxes) in chains.OrderBy(c => c.Start).ThenBy(c => c.Reference == 0).ThenBy(c => c.Reference))
        {
            var name = reference == 0 ? L.T("Reservas") : $"Ref{reference}";
            var pages = Pack(boxes, style, name, plan);
            if (pages.Count == 0)
            {
                continue;
            }
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
                var page = new TechScreenPage { Number = first + i, Reference = reference, Index = i, Style = style };
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
    // en orden. Las columnas se forman al colocarlo, según el sitio que haya. Placement: el retoque de esta parte.
    private sealed record Box(string Title, List<Group> Groups)
    {
        public string Key { get; init; } = "";
        public int Part { get; init; }
        public List<string> FeatureIds { get; init; } = [];
        public TechBoxPart? Placement { get; init; }
    }

    private sealed record Group(string Asset, List<Item> Items);

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
                .Select(asset => new Group(asset.Key, asset.OrderBy(r => r.MainPointer)
                    .Select(r => new Item(RowText(shortNames, asset.Key, r.Feature.ClientReference), reference.Number, r.Technology, r.Pointer, r.MainPointer))
                    .ToList()))
                .ToList();
            boxes.Add(new Box(title, groups) { Key = fixture.Key, FeatureIds = fixture.Select(r => r.Feature.Id).ToList() });
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
                boxes.Add(new Box($"Ref{reference.Number} · {name}", [new Group("", items)])
                {
                    Key = string.Create(CultureInfo.InvariantCulture, $"Ref{reference.Number}:{tech.Key}"),
                    FeatureIds = tech.Select(r => r.Feature.Id).ToList(),
                });
            }
        }
        return boxes;
    }

    // Busca el retoque de cada recuadro (por Fixture o, si cambió de nombre, por sus features), lo parte donde diga
    // el retoque y, si es más ancho que la pantalla, también solo. Cada parte lleva su colocación a mano, si la tiene.
    private static List<Box> ApplyEdits(Project project, int chain, List<Box> boxes, TechScreenPlan plan, TechScreenStyle s)
    {
        var edits = project.TechScreens.BoxEdits.Where(e => e.Reference == chain).ToList();
        var byBox = new Dictionary<Box, TechBoxEdit>();
        foreach (var box in boxes)
        {
            if (edits.FirstOrDefault(e => e.Key == box.Key) is { } edit)
            {
                byBox[box] = edit;
                edits.Remove(edit);
            }
        }
        // Un retoque sin su Fixture pasa al recuadro sin retoque que comparta con él al menos la mitad de las features.
        foreach (var edit in edits.ToList())
        {
            var ids = edit.FeatureIds.ToHashSet();
            var match = boxes.Where(b => !byBox.ContainsKey(b))
                .Select(b => (Box: b, Shared: b.FeatureIds.Count(ids.Contains)))
                .Where(x => x.Shared > 0 && x.Shared * 2 >= ids.Count)
                .OrderByDescending(x => x.Shared)
                .Select(x => x.Box)
                .FirstOrDefault();
            if (match is not null)
            {
                byBox[match] = edit;
                edits.Remove(edit);
            }
        }
        foreach (var edit in edits)
        {
            plan.Warnings.Add(L.T("{0}: los retoques de «{1}» ya no corresponden a ningún recuadro y no se aplican.", chain == 0 ? L.T("Reservas") : $"Ref{chain}", edit.Key));
        }

        var result = new List<Box>();
        foreach (var box in boxes)
        {
            var edit = byBox.GetValueOrDefault(box);
            var parts = new List<List<Group>>();
            foreach (var group in box.Groups)
            {
                if (parts.Count == 0 || (edit?.SplitBefore.Contains(group.Asset) ?? false))
                {
                    parts.Add([]);
                }
                parts[^1].Add(group);
            }
            var index = 0;
            foreach (var part in parts.SelectMany(p => SplitWide(p, s)))
            {
                var current = index;
                result.Add(box with
                {
                    Title = current == 0 ? box.Title : $"{box.Title} (cont.)",
                    Groups = part,
                    Part = current,
                    Placement = edit?.Parts.FirstOrDefault(p => p.Part == current),
                });
                index++;
            }
        }
        return result;
    }

    // Columnas de un recuadro con un máximo de filas por columna: un grupo largo se reparte en varias columnas de
    // alto parecido, una al lado de otra.
    private static List<List<Item>> Columns(Box box, int maxRows) =>
        box.Groups.SelectMany(group =>
        {
            var count = (group.Items.Count + maxRows - 1) / maxRows;
            var perColumn = (group.Items.Count + count - 1) / count;
            return group.Items.Chunk(perColumn).Select(c => c.ToList());
        }).ToList();

    // Grupos de un recuadro más ancho que la pantalla, con las columnas más altas posibles, partidos en varios.
    // Se parte entre grupos; un grupo no se parte.
    private static IEnumerable<List<Group>> SplitWide(List<Group> groups, TechScreenStyle s)
    {
        var current = new List<Group>();
        foreach (var group in groups)
        {
            if (current.Count > 0 && Width(Columns(new Box("", [.. current, group]), RowsPerColumn(s)), s) > s.AreaRight - s.AreaLeft)
            {
                yield return current;
                current = [];
            }
            current.Add(group);
        }
        if (current.Count > 0)
        {
            yield return current;
        }
    }

    private static int LabelWidth(List<Item> column, TechScreenStyle s) => Math.Max(60, column.Max(i => i.Text.Length) * s.CharWidth + 6);

    private static int ColumnWidth(List<Item> column, TechScreenStyle s) => LabelWidth(column, s) + LabelGap + s.FaceplateWidth;

    private static int Width(List<List<Item>> columns, TechScreenStyle s) =>
        2 * Padding + columns.Sum(c => ColumnWidth(c, s)) + ColumnGap * (columns.Count - 1);

    private static int Height(List<List<Item>> columns, TechScreenStyle s) =>
        s.TitleHeight + 2 * Padding + columns.Max(c => c.Count) * s.RowHeight - (s.RowHeight - s.FaceplateHeight);

    // ---------------------------------------------------------------- colocación

    // Columnas más bajas que esto no se prueban para meter un recuadro en un hueco.
    private const int MinRowsPerColumn = 3;

    private sealed record Rect(int Left, int Top, int Width, int Height)
    {
        public int Right => Left + Width;
        public int Bottom => Top + Height;
    }

    // Coloca los recuadros de una referencia. Primero los movidos a mano a su sitio, luego los que solo tienen
    // pantalla en el primer hueco de esa pantalla y, por último, los demás en orden: cada uno en el hueco libre más
    // arriba (y luego más a la izquierda) desde la pantalla del anterior; si no cabe con las columnas más altas,
    // prueba con columnas más bajas; si aun así no cabe, pantalla siguiente. Un recuadro no se parte entre pantallas.
    private static List<List<TechScreenBox>> Pack(List<Box> boxes, TechScreenStyle s, string name, TechScreenPlan plan)
    {
        var pages = new List<List<TechScreenBox>>();
        List<TechScreenBox> PageAt(int index)
        {
            while (pages.Count <= index)
            {
                pages.Add([]);
            }
            return pages[index];
        }

        foreach (var box in boxes.Where(b => b.Placement is { Screen: not null, Left: not null, Top: not null }))
        {
            var p = box.Placement!;
            PageAt(p.Screen!.Value).Add(Place(box, Columns(box, Rows(box, s)), p.Left!.Value, p.Top!.Value, StyleOf(box, s), manual: true));
        }
        foreach (var box in boxes.Where(b => b.Placement is { Screen: not null } && (b.Placement.Left is null || b.Placement.Top is null)))
        {
            var page = PageAt(box.Placement!.Screen!.Value);
            page.Add(TryPlace(box, page, s) ?? Place(box, Columns(box, Rows(box, s)), s.AreaLeft, s.AreaTop, StyleOf(box, s), manual: true));
        }
        var cursor = 0;
        foreach (var box in boxes.Where(b => b.Placement?.Screen is null))
        {
            while (true)
            {
                var page = PageAt(cursor);
                if (TryPlace(box, page, s) is { } placed)
                {
                    page.Add(placed);
                    break;
                }
                if (page.Count == 0)
                {
                    // En una pantalla vacía siempre se coloca, aunque se salga (un recuadro enorme): mejor verlo que perderlo.
                    page.Add(Place(box, Columns(box, Rows(box, s)), s.AreaLeft, s.AreaTop, StyleOf(box, s), manual: false));
                    break;
                }
                cursor++;
            }
        }

        for (var i = 0; i < pages.Count; i++)
        {
            if (pages[i].Count == 0)
            {
                plan.Warnings.Add(L.T("{0}: su pantalla {1} queda vacía.", name, i + 1));
            }
            foreach (var box in pages[i].Where(b => b.IsManual))
            {
                if (box.Left < s.AreaLeft || box.Top < s.AreaTop || box.Right > s.AreaRight || box.Bottom > s.AreaBottom)
                {
                    plan.Warnings.Add(L.T("{0}: «{1}» se sale del lienzo de su pantalla {2}.", name, box.Title, i + 1));
                }
                foreach (var other in pages[i].Where(o => o != box && Overlaps(Of(o), Of(box), 0)))
                {
                    plan.Warnings.Add(L.T("{0}: «{1}» pisa a «{2}» en su pantalla {3}.", name, box.Title, other.Title, i + 1));
                }
            }
        }
        return pages;
    }

    private static Rect Of(TechScreenBox b) => new(b.Left, b.Top, b.Width, b.Height);

    // Estilo del recuadro: el de la pantalla, escalado si el retoque le da otro tamaño.
    private static TechScreenStyle StyleOf(Box box, TechScreenStyle s) =>
        box.Placement?.Scale is int scale ? Scaled(s, Math.Clamp(scale, MinScale, MaxScale)) : s;

    // Filas por columna del recuadro: las del retoque o las que quepan.
    private static int Rows(Box box, TechScreenStyle s) => box.Placement?.RowsPerColumn is int r and > 0 ? r : RowsPerColumn(StyleOf(box, s));

    private static TechScreenBox? TryPlace(Box box, List<TechScreenBox> page, TechScreenStyle s)
    {
        var bs = StyleOf(box, s);
        var taken = page.Select(Of).ToList();
        var maxRows = box.Groups.Max(g => g.Items.Count);
        var fixedRows = box.Placement?.RowsPerColumn is int r and > 0 ? r : (int?)null;
        var from = fixedRows ?? Math.Min(RowsPerColumn(bs), maxRows);
        var to = fixedRows ?? Math.Min(MinRowsPerColumn, maxRows);
        for (var rows = from; rows >= to; rows--)
        {
            var columns = Columns(box, rows);
            var (w, h) = (Width(columns, bs), Height(columns, bs));
            if (FindSpot(taken, w, h, s) is { } spot)
            {
                return Place(box, columns, spot.Left, spot.Top, bs, manual: box.Placement is not null);
            }
        }
        return null;
    }

    // Hueco libre más arriba y más a la izquierda para un recuadro de w x h. Se prueban la esquina de la zona y,
    // por cada recuadro ya colocado, a su derecha y debajo.
    private static Rect? FindSpot(List<Rect> taken, int w, int h, TechScreenStyle s)
    {
        var gap = s.BoxGap;
        var candidates = new List<(int X, int Y)> { (s.AreaLeft, s.AreaTop) };
        foreach (var r in taken)
        {
            candidates.Add((r.Right + gap, r.Top));
            candidates.Add((r.Left, r.Bottom + gap));
            candidates.Add((s.AreaLeft, r.Bottom + gap));
        }
        foreach (var (x, y) in candidates.Distinct().OrderBy(c => c.Y).ThenBy(c => c.X))
        {
            var spot = new Rect(x, y, w, h);
            if (spot.Left >= s.AreaLeft && spot.Top >= s.AreaTop && spot.Right <= s.AreaRight && spot.Bottom <= s.AreaBottom
                && !taken.Any(t => Overlaps(t, spot, gap)))
            {
                return spot;
            }
        }
        return null;
    }

    private static bool Overlaps(Rect a, Rect b, int gap) =>
        a.Left < b.Right + gap && b.Left < a.Right + gap && a.Top < b.Bottom + gap && b.Top < a.Bottom + gap;

    private static TechScreenBox Place(Box box, List<List<Item>> columns, int left, int top, TechScreenStyle s, bool manual)
    {
        var rows = new List<TechScreenRow>();
        var x = left + Padding;
        foreach (var column in columns)
        {
            var labelWidth = LabelWidth(column, s);
            var y = top + s.TitleHeight + Padding;
            foreach (var item in column)
            {
                rows.Add(new TechScreenRow(item.Text, item.Reference, item.Technology, item.Pointer, item.MainPointer, x, y, labelWidth));
                y += s.RowHeight;
            }
            x += labelWidth + LabelGap + s.FaceplateWidth + ColumnGap;
        }
        return new TechScreenBox(box.Title, left, top, Width(columns, s), Height(columns, s), rows)
        {
            Key = box.Key,
            Part = box.Part,
            FeatureIds = box.FeatureIds,
            Assets = box.Groups.Select(g => g.Asset).ToList(),
            RowsPerColumn = columns.Max(c => c.Count),
            IsManual = manual,
            Scale = box.Placement?.Scale is int scale ? Math.Clamp(scale, MinScale, MaxScale) : 100,
            Style = s,
        };
    }
}

// Retoques a mano de los recuadros: mover, cambiar de pantalla, filas por columna, partir y volver a automático.
// La referencia del retoque es la de las pantallas donde está el recuadro (0 en las de reservas).
public static class TechScreenEdits
{
    public static TechBoxEdit EditOf(Project project, TechScreenPage page, TechScreenBox box)
    {
        var edits = project.TechScreens.BoxEdits;
        var chain = page.Reference;
        var edit = edits.FirstOrDefault(e => e.Reference == chain && e.Key == box.Key);
        if (edit is null)
        {
            // Si el Fixture cambió de nombre, el retoque que tenía sigue siendo el suyo.
            var ids = box.FeatureIds.ToHashSet();
            edit = edits.FirstOrDefault(e => e.Reference == chain && e.FeatureIds.Any(ids.Contains) && e.FeatureIds.Count(ids.Contains) * 2 >= e.FeatureIds.Count);
            if (edit is null)
            {
                edit = new TechBoxEdit { Reference = chain };
                edits.Add(edit);
            }
        }
        edit.Key = box.Key;
        edit.FeatureIds = [.. box.FeatureIds];
        return edit;
    }

    private static TechBoxPart PartOf(TechBoxEdit edit, int part)
    {
        var p = edit.Parts.FirstOrDefault(x => x.Part == part);
        if (p is null)
        {
            p = new TechBoxPart { Part = part };
            edit.Parts.Add(p);
        }
        return p;
    }

    // Lo deja en (left, top) de su pantalla, ajustado a la rejilla, con las columnas que tiene ahora.
    public static void Move(Project project, TechScreenPage page, TechScreenBox box, int left, int top)
    {
        var p = PartOf(EditOf(project, page, box), box.Part);
        p.Screen = page.Index;
        p.Left = Snap(left);
        p.Top = Snap(top);
        p.RowsPerColumn ??= box.RowsPerColumn;
    }

    // Lo manda a otra pantalla de su referencia (screen: 0 es la primera; una más que las que hay, pantalla nueva),
    // donde busca hueco solo.
    public static void ToScreen(Project project, TechScreenPage page, TechScreenBox box, int screen)
    {
        var p = PartOf(EditOf(project, page, box), box.Part);
        p.Screen = screen;
        p.Left = null;
        p.Top = null;
    }

    // Filas por columna; null vuelve a las que quepan. Se queda en su pantalla y en su sitio.
    public static void SetRows(Project project, TechScreenPage page, TechScreenBox box, int? rows)
    {
        var p = PartOf(EditOf(project, page, box), box.Part);
        p.RowsPerColumn = rows is > 0 ? rows : null;
        p.Screen ??= page.Index;
        p.Left ??= box.Left;
        p.Top ??= box.Top;
    }

    // Parte el recuadro antes de un Asset: ese Asset y los siguientes van a otra parte «(cont.)». Las partes que
    // vienen detrás se vuelven a colocar solas.
    public static void Split(Project project, TechScreenPage page, TechScreenBox box, string asset)
    {
        var edit = EditOf(project, page, box);
        if (!edit.SplitBefore.Contains(asset))
        {
            edit.SplitBefore.Add(asset);
        }
        edit.Parts.RemoveAll(p => p.Part > box.Part);
    }

    // Tamaño del recuadro en % (40 a 200). Se queda en su pantalla, en su sitio y con sus columnas: solo cambia
    // de tamaño.
    public static void Resize(Project project, TechScreenPage page, TechScreenBox box, int percent)
    {
        var p = PartOf(EditOf(project, page, box), box.Part);
        var scale = Math.Clamp(percent, TechScreens.MinScale, TechScreens.MaxScale);
        p.Scale = scale == 100 ? null : scale;
        p.Screen ??= page.Index;
        p.Left ??= box.Left;
        p.Top ??= box.Top;
        p.RowsPerColumn ??= box.RowsPerColumn;
    }

    // Quita todos los retoques del recuadro (todas sus partes): vuelve a colocarse solo.
    public static void Reset(Project project, TechScreenPage page, TechScreenBox box) =>
        project.TechScreens.BoxEdits.Remove(EditOf(project, page, box));

    public static void ResetAll(Project project) => project.TechScreens.BoxEdits.Clear();

    public static int Snap(int value) => (int)Math.Round(value / (double)TechScreens.Grid) * TechScreens.Grid;
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
