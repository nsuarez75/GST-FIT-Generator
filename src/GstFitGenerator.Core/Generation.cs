// Escribe en la carpeta de salida lo que se haya marcado, cada casilla en su subcarpeta, y guarda en el proyecto
// los offsets de esta generación.
using System.Text;

namespace GstFitGenerator.Core;

public sealed class GenerationOptions
{
    public bool Template { get; set; } = true;
    public bool FitExcel { get; set; } = true;
    public bool TableInfoDb { get; set; } = true;
    public bool ConfigReferenceFb { get; set; } = true;
    public bool CheckTechDb { get; set; } = true;
    public bool TechScreens { get; set; } = true;

    public bool Any => Template || FitExcel || TableInfoDb || ConfigReferenceFb || CheckTechDb || TechScreens;
}

public static class Generation
{
    // Subcarpeta de la carpeta de salida para cada casilla.
    public const string TemplateFolder = "Plantilla";
    public const string FitFolder = "FIT";
    public const string TableInfoFolder = "TableInfo_DB";
    public const string ConfigReferenceFolder = "Config_Reference_FB";
    public const string CheckTechFolder = "CheckTech_DB";
    public const string TechScreensFolder = "Pantallas";

    // Rutas de los archivos que se escribirán, para avisar antes de sobrescribir.
    public static List<string> Planned(Project project, GenerationOptions options, string folder, DateOnly date)
    {
        var files = new List<string>();
        if (options.Template)
        {
            files.Add(Path.Combine(TemplateFolder, ExcelWriters.TemplateFileName(project, date)));
        }
        if (options.FitExcel)
        {
            files.Add(Path.Combine(FitFolder, ExcelWriters.FitFileName(project, date)));
        }
        if (options.TableInfoDb)
        {
            files.Add(Path.Combine(TableInfoFolder, PlcWriters.TableInfoFile));
        }
        if (options.ConfigReferenceFb)
        {
            files.Add(Path.Combine(ConfigReferenceFolder, PlcWriters.ConfigReferenceFile));
        }
        if (options.CheckTechDb)
        {
            files.Add(Path.Combine(CheckTechFolder, PlcWriters.CheckTechFile));
            files.AddRange(PlcWriters.CheckTechFixtures(project).Where(f => f.Uses.Count > 0)
                .Select(f => Path.Combine(CheckTechFolder, f.FileName)));
        }
        if (options.TechScreens)
        {
            files.AddRange(Core.TechScreens.Plan(project).Pages.Select(p => Path.Combine(TechScreensFolder, TechScreenWriter.FileName(project, p))));
        }
        return files.Select(f => Path.Combine(folder, f)).ToList();
    }

    public static List<string> Run(Project project, Catalog catalog, GenerationOptions options, string folder, DateOnly date)
    {
        var written = new List<string>();
        var encoding = new UTF8Encoding(true);

        // Crea la subcarpeta si hace falta y devuelve la ruta del archivo.
        string PathIn(string subfolder, string name)
        {
            var dir = Path.Combine(folder, subfolder);
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, name);
        }

        void Write(string subfolder, string name, string content)
        {
            var path = PathIn(subfolder, name);
            File.WriteAllText(path, content, encoding);
            written.Add(path);
        }

        // Las pantallas de techs rellenan las descripciones vacías de Screen Names, que también van en la plantilla.
        var plan = options.TechScreens ? Core.TechScreens.Plan(project) : null;
        if (plan is not null)
        {
            if (plan.Errors.Count > 0)
            {
                throw new InvalidDataException(string.Join(Environment.NewLine, plan.Errors));
            }
            Core.TechScreens.FillDescriptions(project, plan);
        }

        if (options.Template)
        {
            var path = PathIn(TemplateFolder, ExcelWriters.TemplateFileName(project, date));
            using var book = ExcelWriters.Template(project, catalog);
            book.SaveAs(path);
            written.Add(path);
        }
        if (options.FitExcel)
        {
            var path = PathIn(FitFolder, ExcelWriters.FitFileName(project, date));
            using var book = ExcelWriters.Fit(project, catalog);
            book.SaveAs(path);
            written.Add(path);
        }
        if (options.TableInfoDb)
        {
            Write(TableInfoFolder, PlcWriters.TableInfoFile, PlcWriters.TableInfoDb(project));
        }
        if (options.ConfigReferenceFb)
        {
            Write(ConfigReferenceFolder, PlcWriters.ConfigReferenceFile, PlcWriters.ConfigReferenceFb(project));
        }
        if (options.CheckTechDb)
        {
            var fixtures = PlcWriters.CheckTechFixtures(project);
            Write(CheckTechFolder, PlcWriters.CheckTechFile, PlcWriters.CheckTechDb(fixtures));
            // Un SCL por fixture que tenga algo que inicializar.
            foreach (var fixture in fixtures.Where(f => f.Uses.Count > 0))
            {
                Write(CheckTechFolder, fixture.FileName, PlcWriters.CheckTechInit(fixture));
            }
        }
        if (plan is not null)
        {
            foreach (var page in plan.Pages)
            {
                Write(TechScreensFolder, TechScreenWriter.FileName(project, page), TechScreenWriter.Write(project, plan, page));
            }
        }

        project.LastOffsets = Layout.Offsets(project);
        project.OutputFolder = folder;
        return written;
    }
}
