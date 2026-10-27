// Escribe en la carpeta de salida lo que se haya marcado y guarda en el proyecto los offsets de esta generación.
using System.Text;

namespace GstFitGenerator.Core;

public sealed class GenerationOptions
{
    public bool Template { get; set; } = true;
    public bool FitExcel { get; set; } = true;
    public bool TableInfoDb { get; set; } = true;
    public bool ConfigReferenceFb { get; set; } = true;
    public bool CheckTechDb { get; set; } = true;

    public bool Any => Template || FitExcel || TableInfoDb || ConfigReferenceFb || CheckTechDb;
}

public static class Generation
{
    // Rutas de los archivos que se escribirán, para avisar antes de sobrescribir.
    public static List<string> Planned(Project project, GenerationOptions options, string folder, DateOnly date)
    {
        var files = new List<string>();
        if (options.Template)
        {
            files.Add(ExcelWriters.TemplateFileName(project, date));
        }
        if (options.FitExcel)
        {
            files.Add(ExcelWriters.FitFileName(project, date));
        }
        if (options.TableInfoDb)
        {
            files.Add(PlcWriters.TableInfoFile);
        }
        if (options.ConfigReferenceFb)
        {
            files.Add(PlcWriters.ConfigReferenceFile);
        }
        if (options.CheckTechDb)
        {
            files.Add(PlcWriters.CheckTechFile);
            files.AddRange(PlcWriters.CheckTechFixtures(project).Where(f => f.Uses.Count > 0).Select(f => f.FileName));
        }
        return files.Select(f => Path.Combine(folder, f)).ToList();
    }

    public static List<string> Run(Project project, Catalog catalog, GenerationOptions options, string folder, DateOnly date)
    {
        Directory.CreateDirectory(folder);
        var written = new List<string>();
        var encoding = new UTF8Encoding(true);

        void Write(string name, string content)
        {
            var path = Path.Combine(folder, name);
            File.WriteAllText(path, content, encoding);
            written.Add(path);
        }

        if (options.Template)
        {
            var path = Path.Combine(folder, ExcelWriters.TemplateFileName(project, date));
            using var book = ExcelWriters.Template(project, catalog);
            book.SaveAs(path);
            written.Add(path);
        }
        if (options.FitExcel)
        {
            var path = Path.Combine(folder, ExcelWriters.FitFileName(project, date));
            using var book = ExcelWriters.Fit(project, catalog);
            book.SaveAs(path);
            written.Add(path);
        }
        if (options.TableInfoDb)
        {
            Write(PlcWriters.TableInfoFile, PlcWriters.TableInfoDb(project));
        }
        if (options.ConfigReferenceFb)
        {
            Write(PlcWriters.ConfigReferenceFile, PlcWriters.ConfigReferenceFb(project));
        }
        if (options.CheckTechDb)
        {
            var fixtures = PlcWriters.CheckTechFixtures(project);
            Write(PlcWriters.CheckTechFile, PlcWriters.CheckTechDb(fixtures));
            // Un SCL por fixture que tenga algo que inicializar.
            foreach (var fixture in fixtures.Where(f => f.Uses.Count > 0))
            {
                Write(fixture.FileName, PlcWriters.CheckTechInit(fixture));
            }
        }

        project.LastOffsets = Layout.Offsets(project);
        project.OutputFolder = folder;
        return written;
    }
}
