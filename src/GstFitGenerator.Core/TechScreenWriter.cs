// XML de una pantalla de techs para importar en TIA (WinCC Comfort/Advanced). La parte fija (cabecera, botones,
// eventos, ScreenLocking, subtítulo) sale de TechScreen.template.xml; los recuadros, textos, faceplates y botones
// de pantalla anterior/siguiente se copian de las piezas de ejemplo de TechScreen.parts.xml y se rellenan.
using System.Globalization;
using System.Reflection;
using System.Xml.Linq;

namespace GstFitGenerator.Core;

public static class TechScreenWriter
{
    private static readonly Lazy<string> Template = new(() => Resource("TechScreen.template.xml"));
    // Con los espacios tal cual: dentro de <Text><body><p> un salto de línea de más se vería en el HMI.
    private static readonly Lazy<XElement> Parts = new(() => XElement.Parse(Resource("TechScreen.parts.xml"), LoadOptions.PreserveWhitespace));

    public static string FileName(Project project, TechScreenPage page) => Screen.Name(project.ScreenPrefix, page.Number) + ".xml";

    public static string Write(Project project, TechScreenPlan plan, TechScreenPage page)
    {
        var doc = XDocument.Parse(Template.Value, LoadOptions.PreserveWhitespace);
        var screen = doc.Root!.Element("Hmi.Screen.Screen")!;
        Set(screen, "Name", Screen.Name(project.ScreenPrefix, page.Number));
        Set(screen, "Number", Number(Screen.HmiNumber(project.ScreenPrefix, page.Number) ?? page.Number));

        // Subtítulo: el texto número N de la lista CO_Traceability_ID, que es la descripción de Screen Names.
        var subtitle = screen.Descendants("Hmi.Screen.Property").Single(p => p.Element("AttributeList")?.Element("Name")?.Value == "ProcessValue");
        subtitle.Descendants("Tag").Single().Element("Name")!.Value = IntValue(page.Number);

        var items = screen.Descendants("Hmi.Screen.ScreenLayer")
            .Single(l => l.Element("AttributeList")?.Element("Index")?.Value == "0")
            .Element("ObjectList")!;
        if (plan.Neighbour(page, -1) is { } previous)
        {
            items.AddFirst(NavButton(project, previous, "btnPreviousScreen", 510, "CC_ArrowLeftDoppel_24x24"));
        }
        if (plan.Neighbour(page, 1) is { } next)
        {
            items.AddFirst(NavButton(project, next, "btnNextScreen", 1342, "CC_ArrowRightDoppel_24x24"));
        }

        var s = page.Style;
        int box = 0, row = 0, tab = 10;
        foreach (var b in page.Boxes)
        {
            box++;
            items.Add(Rectangle(b, box, s));
            items.Add(Text($"txtBox_{box}", b.Title, b.Left, b.Top, b.Width, s.TitleHeight, s, title: true));
            foreach (var r in b.Rows)
            {
                row++;
                items.Add(Text($"txtTech_{row}", r.Text, r.Left, r.Top + (s.FaceplateHeight - s.LabelHeight) / 2, r.LabelWidth, s.LabelHeight, s, title: false));
                items.Add(Faceplate(r, row, tab++, s));
            }
        }

        // Los ID internos tienen que ser únicos en el documento; TIA los renumera al importar.
        var id = 0;
        foreach (var element in doc.Descendants().Where(e => e.Attribute("ID") is not null))
        {
            element.SetAttributeValue("ID", (id++).ToString("X", CultureInfo.InvariantCulture));
        }
        return "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" + doc.Root.ToString(SaveOptions.DisableFormatting).ReplaceLineEndings("\r\n") + "\r\n";
    }

    private static XElement NavButton(Project project, TechScreenPage target, string name, int left, string icon)
    {
        var button = Part("Hmi.Screen.Button");
        Set(button, "ObjectName", name);
        Set(button, "Left", Number(left));
        button.Element("LinkList")!.Element("PictureOff")!.Element("Name")!.Value = icon;
        var screenName = button.Descendants("Hmi.Event.FunctionListEntryParameter")
            .Single(p => p.Element("AttributeList")?.Element("Name")?.Value == "Screen name");
        screenName.Descendants("Value").Single().Element("Name")!.Value = Screen.Name(project.ScreenPrefix, target.Number);
        return button;
    }

    private static XElement Rectangle(TechScreenBox box, int number, TechScreenStyle s)
    {
        var rect = Part("Hmi.Screen.Rectangle");
        Set(rect, "ObjectName", $"rectBox_{number}");
        Set(rect, "BackColor", Rgb(s.BoxBack));
        Set(rect, "BorderColor", Rgb(s.BoxBorder));
        Set(rect, "BorderWidth", "1");
        Place(rect, box.Left, box.Top, box.Width, box.Height);
        return rect;
    }

    private static XElement Text(string name, string text, int left, int top, int width, int height, TechScreenStyle s, bool title)
    {
        var fontSize = title ? s.TitleFontSize : s.LabelFontSize;
        var field = Part("Hmi.Screen.TextField");
        Set(field, "ObjectName", name);
        Set(field, "FitToLargest", "false");
        Set(field, "BackFillStyle", title ? "Solid" : "Transparent");
        Set(field, "BackColor", title ? Rgb(s.TitleBack) : "255, 255, 255");
        Set(field, "ForeColor", Rgb(title ? s.TitleFore : s.LabelFore));
        Set(field, "HorizontalAlignment", title ? "Center" : "Left");
        Set(field, "VerticalAlignment", "Middle");
        Place(field, left, top, width, height);
        foreach (var font in field.Descendants("Hmi.Globalization.FontItem"))
        {
            Set(font, "FontFamily", "Tahoma");
            Set(font, "FontSize", Number(fontSize));
            Set(font, "FontStyle", "Bold");
        }
        foreach (var item in field.Descendants("MultilingualTextItem"))
        {
            item.Element("AttributeList")!.Element("Text")!.ReplaceWith(new XElement("Text", new XElement("body", new XElement("p", text))));
        }
        return field;
    }

    private static XElement Faceplate(TechScreenRow row, int number, int tabIndex, TechScreenStyle s)
    {
        var faceplate = Part("Hmi.Screen.FaceplateInstance");
        Set(faceplate, "ObjectName", $"fpTech_{number}");
        Set(faceplate, "TabIndex", Number(tabIndex));
        Place(faceplate, row.FaceplateLeft, row.Top, s.FaceplateWidth, s.FaceplateHeight);
        var tags = new Dictionary<string, string>
        {
            ["Status"] = $"Tech_Status_{row.MainPointer:000}",
            ["Reference_in"] = IntValue(row.Reference),
            ["Number_tech_in"] = IntValue(row.Technology),
            ["Position_tech_in"] = IntValue(row.Pointer),
        };
        foreach (var property in faceplate.Descendants("Hmi.Screen.InterfacePropertySimple"))
        {
            if (tags.TryGetValue(property.Element("AttributeList")!.Element("Name")!.Value, out var tag))
            {
                property.Descendants("Tag").Single().Element("Name")!.Value = tag;
            }
        }
        return faceplate;
    }

    // #RRGGBB -> "r, g, b", como escribe TIA los colores. Un color mal escrito sale negro.
    private static string Rgb(string hex)
    {
        var (r, g, b) = TechScreenColors.Parse(hex);
        return string.Create(CultureInfo.InvariantCulture, $"{r}, {g}, {b}");
    }

    // Variables internas del HMI con un valor fijo: Int_Value_022 vale 22.
    private static string IntValue(int value) => string.Create(CultureInfo.InvariantCulture, $"Int_Value_{value:000}");

    private static void Place(XElement item, int left, int top, int width, int height)
    {
        Set(item, "Left", Number(left));
        Set(item, "Top", Number(top));
        Set(item, "Width", Number(width));
        Set(item, "Height", Number(height));
    }

    private static XElement Part(string tag) => new(Parts.Value.Element(tag) ?? throw new InvalidOperationException($"Falta la pieza {tag}."));

    private static void Set(XElement item, string attribute, string value) =>
        (item.Element("AttributeList")?.Element(attribute) ?? throw new InvalidOperationException($"{item.Name} no tiene {attribute}.")).Value = value;

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Resource(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Falta el recurso {name}.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
