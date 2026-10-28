using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using GstFitGenerator.App.ViewModels;
using GstFitGenerator.App.Views;
using GstFitGenerator.Core;
using GstFitGenerator.Tests;

namespace GstFitGenerator.App.Tests;

public sealed class MainWindowTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("gst-fit-app-").FullName;

    public void Dispose()
    {
        L.Language = L.Spanish;
        App.ApplyTheme(AppSettings.ThemeSystem);
        Directory.Delete(_dir, recursive: true);
    }

    // Ventana con el proyecto de prueba importado desde una plantilla (con una reserva y dos referencias).
    private async Task<(MainWindow Window, MainViewModel Main, FakeDialogs Dialogs)> Open(double width = 1400, double height = 860)
    {
        var session = new Session(Path.Combine(_dir, "catalogo.fitcat"));
        var dialogs = new FakeDialogs();
        var main = new MainViewModel(session, dialogs, new AppSettings(), _ => { });

        var project = FakeProject.Create();
        project.References[0].Features.Insert(3, new Feature { ReserveTechnology = 2 });
        Editing.DuplicateReference(project, project.References[0]);
        project.Versions.Add(new VersionEntry { Date = new DateOnly(2026, 1, 15), Description = "Creación" });
        var template = Path.Combine(_dir, "PRUEBA_FIT_rev4_20260115.xlsx");
        using (var book = ExcelWriters.Template(project, FakeProject.Catalog()))
        {
            book.SaveAs(template);
        }
        dialogs.Files.Enqueue(template);
        await main.ImportTemplateCommand.ExecuteAsync(null);

        var window = new MainWindow { Width = width, Height = height };
        window.Attach(main);
        main.Dialogs = dialogs;
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, main, dialogs);
    }

    private static ReferenceViewModel ShowReference(MainViewModel main, int number = 1)
    {
        main.SelectedNode = main.FindNode(NavKind.Reference, number);
        Dispatcher.UIThread.RunJobs();
        return Assert.IsType<ReferenceViewModel>(main.Editor);
    }

    private static FeatureRowViewModel Row(ReferenceViewModel reference, string client) =>
        reference.Items.OfType<FeatureRowViewModel>().First(r => r.Feature.ClientReference == client);

    // GST_SCREENSHOTS: carpeta en la que dejar capturas de cada pantalla.
    private static void Shot(Window window, string name)
    {
        Dispatcher.UIThread.RunJobs();
        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        var dir = Environment.GetEnvironmentVariable("GST_SCREENSHOTS");
        if (string.IsNullOrEmpty(dir))
        {
            return;
        }
        Directory.CreateDirectory(dir);
        // Solo para revisar las pantallas a ojo; el formato PNG por defecto basta.
#pragma warning disable CS0618
        frame.Save(Path.Combine(dir, name + ".png"));
#pragma warning restore CS0618
    }

    [AvaloniaFact]
    public async Task Importar_la_plantilla_crea_el_proyecto()
    {
        var (window, main, dialogs) = await Open();

        Assert.Single(dialogs.ChangeLists);
        var project = main.Session.Project;
        Assert.Equal(("PRUEBA", 4, "PLT", "L01"), (project.Name, project.Revision, project.Plant, project.Line));
        Assert.Equal(2, project.References.Count);
        Assert.Equal(4, main.Session.Catalog.Types.Count);
        var references = main.FindNode(NavKind.References)!;
        Assert.Equal(["Referencia 1", "Referencia 2"], references.Children.Select(c => c.Title));
        Assert.True(main.Session.Dirty);
        Assert.Contains("PRUEBA", window.Title);
        Shot(window, "01-proyecto");
    }

    [AvaloniaTheory]
    [InlineData(AppSettings.ThemeLight, L.Spanish, 1400)]
    [InlineData(AppSettings.ThemeDark, L.English, 1400)]
    [InlineData(AppSettings.ThemeLight, L.Spanish, 900)]
    public async Task Todas_las_pantallas_se_dibujan(string theme, string language, double width)
    {
        L.Language = language;
        App.ApplyTheme(theme);
        var (window, main, _) = await Open(width);
        var suffix = $"{theme}-{language}-{width}";

        foreach (var kind in new[] { NavKind.Project, NavKind.Technologies, NavKind.Locations, NavKind.Reference, NavKind.Auxiliaries, NavKind.Screens, NavKind.TechScreens, NavKind.Versions, NavKind.Sizes })
        {
            main.SelectedNode = main.FindNode(kind, kind == NavKind.Reference ? 1 : 0);
            Dispatcher.UIThread.RunJobs();
            Assert.NotNull(main.Editor);
            if (main.Editor is ReferenceViewModel reference)
            {
                reference.Selected = Row(reference, "W1");
            }
            if (main.Editor is TechScreensViewModel techScreens)
            {
                techScreens.Starts[0].SelectedIndex = 7;
            }
            Shot(window, $"{(int)kind:00}-{kind}-{suffix}");
        }

        var catalog = new CatalogWindow { DataContext = new CatalogViewModel(main.Session, main.Dialogs), Width = Math.Min(width, 900), Height = 720 };
        catalog.Show();
        Shot(catalog, $"10-catalogo-{suffix}");
        catalog.Close();

        var generate = new GenerateWindow { DataContext = new GenerateViewModel(main), Width = Math.Min(width, 980), Height = 720 };
        generate.Show();
        Shot(generate, $"11-generar-{suffix}");
        generate.Close();
    }

    [AvaloniaFact]
    public async Task Anadir_una_feature_propone_el_FeatureId_y_se_puede_deshacer()
    {
        var (_, main, _) = await Open();
        var reference = ShowReference(main);
        reference.Selected = Row(reference, "W1");

        reference.AddCommand.Execute(null);
        var added = Assert.IsType<FeatureRowViewModel>(reference.Selected);
        added.ClientReference = "W12_03";

        Assert.Equal("0102002", added.Offset);
        Assert.Equal(11203, added.Feature.FeatureId);
        Assert.Equal("1000R1", added.Feature.Fixtures[0].LayoutName);
        Assert.Equal("0102003", Row(reference, "W2_01").Offset);

        main.UndoCommand.Execute(null);
        main.UndoCommand.Execute(null);
        reference = Assert.IsType<ReferenceViewModel>(main.Editor);
        Assert.Equal(6, main.Session.Project.References[0].Features.Count);
        Assert.Equal("0102002", Row(reference, "W2_01").Offset);
    }

    [AvaloniaFact]
    public async Task Cambiar_de_tecnologia_avisa_y_lleva_la_feature_al_final_de_su_bloque()
    {
        var (_, main, dialogs) = await Open();
        var reference = ShowReference(main);
        var w1 = Row(reference, "W1");

        w1.Type = reference.TypeOptions.First(o => o.Type == 71);
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(dialogs.Confirms, c => c.Contains("se desplazan 3 features"));
        Assert.Equal("0103002", Row(reference, "W1").Offset);
        Assert.Equal("0102001", Row(reference, "W2_01").Offset);
    }

    [AvaloniaFact]
    public async Task Cancelar_el_cambio_de_tecnologia_no_mueve_nada()
    {
        var (_, main, dialogs) = await Open();
        dialogs.ConfirmAnswer = false;
        var reference = ShowReference(main);

        Row(reference, "W1").Type = reference.TypeOptions.First(o => o.Type == 71);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("0102001", Row(reference, "W1").Offset);
        Assert.Equal(1, Row(reference, "W1").Feature.Type);
    }

    [AvaloniaFact]
    public async Task Eliminar_ofrece_convertir_en_reserva_o_borrar()
    {
        var (_, main, dialogs) = await Open();
        var reference = ShowReference(main);

        reference.Selected = Row(reference, "W2_01");
        await reference.DeleteCommand.ExecuteAsync(null);
        var reserve = Assert.IsType<FeatureRowViewModel>(reference.Selected);
        Assert.True(reserve.IsReserve);
        Assert.Equal("0102002", reserve.Offset);
        Assert.Equal("0102004", Row(reference, "W3").Offset);

        dialogs.DeleteAnswer = DeleteChoice.Delete;
        reference.Selected = Row(reference, "W1");
        await reference.DeleteCommand.ExecuteAsync(null);
        Assert.Equal("0102003", Row(reference, "W3").Offset);
    }

    [AvaloniaFact]
    public async Task Arrastrar_y_los_botones_mueven_dentro_de_la_tecnologia()
    {
        var (_, main, _) = await Open();
        var reference = ShowReference(main);

        reference.Drop(Row(reference, "W3"), Row(reference, "W1"));
        Assert.Equal("0102001", Row(reference, "W3").Offset);

        reference.Selected = Row(reference, "W3");
        reference.MoveDownCommand.Execute(null);
        Assert.Equal("0102002", Row(reference, "W3").Offset);

        // A otra tecnología no se puede soltar.
        reference.Drop(Row(reference, "W3"), Row(reference, "MARCA"));
        Assert.Equal("0102002", Row(reference, "W3").Offset);
    }

    [AvaloniaFact]
    public async Task Anadir_y_quitar_no_rehacen_la_lista()
    {
        var (_, main, dialogs) = await Open();
        var reference = ShowReference(main);
        var w3 = Row(reference, "W3");
        var marca = Row(reference, "MARCA");

        reference.Selected = Row(reference, "W1");
        reference.AddCommand.Execute(null);
        var added = Assert.IsType<FeatureRowViewModel>(reference.Selected);

        // Mismo editor y mismas filas: la lista no se vacía, así no vuelve arriba.
        Assert.Same(reference, main.Editor);
        Assert.Same(w3, Row(reference, "W3"));
        Assert.Same(marca, Row(reference, "MARCA"));
        Assert.Equal("0102005", w3.Offset);
        Assert.Equal("7", main.FindNode(NavKind.Reference, 1)!.Detail);

        // Al borrar del todo, queda seleccionada la siguiente de su tecnología.
        dialogs.DeleteAnswer = DeleteChoice.Delete;
        await reference.DeleteCommand.ExecuteAsync(null);
        Assert.Same(reference, main.Editor);
        Assert.Same(w3, Row(reference, "W3"));
        Assert.Equal("W2_01", reference.Selected!.Feature.ClientReference);
        Assert.Equal("0102004", w3.Offset);
        Assert.DoesNotContain(added, reference.Items);

        // La última de su tecnología: queda la anterior.
        reference.Selected = w3;
        await reference.DeleteCommand.ExecuteAsync(null);
        Assert.Same(reference, main.Editor);
        Assert.True(reference.Selected!.IsReserve);
    }

    [AvaloniaFact]
    public async Task Deshacer_se_queda_en_la_misma_posicion()
    {
        var (window, main, _) = await Open(height: 520);
        var reference = ShowReference(main);
        reference.Selected = Row(reference, "W3");
        for (var i = 0; i < 20; i++)
        {
            reference.AddCommand.Execute(null);
        }
        reference.Selected = Row(reference, "MARCA");
        reference.ShowForm = false;
        Dispatcher.UIThread.RunJobs();
        var scroll = window.GetVisualDescendants().OfType<ReferenceView>().Single().FindControl<ListBox>("Rows")!
            .GetVisualDescendants().OfType<ScrollViewer>().First();
        scroll.Offset = scroll.Offset.WithY(300);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(300, reference.ScrollY);

        // Deshacer el último añadido: la feature seleccionada sigue siendo MARCA y la lista no vuelve arriba.
        main.UndoCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        var undone = Assert.IsType<ReferenceViewModel>(main.Editor);
        Assert.NotSame(reference, undone);
        Assert.Equal("MARCA", undone.Selected!.Feature.ClientReference);
        Assert.False(undone.ShowForm);
        scroll = window.GetVisualDescendants().OfType<ReferenceView>().Single().FindControl<ListBox>("Rows")!
            .GetVisualDescendants().OfType<ScrollViewer>().First();
        Assert.Equal(300, scroll.Offset.Y);
        Assert.Equal(300, undone.ScrollY);
    }

    [AvaloniaFact]
    public async Task Expandir_una_tecnologia_con_muchas_features_es_rapido()
    {
        var (_, main, _) = await Open();
        var features = main.Session.Project.References[0].Features;
        for (var i = 0; i < 300; i++)
        {
            features.Insert(1, new Feature { ClientReference = $"N{i}", Type = 1, FeatureId = 20000 + i });
        }
        var reference = ShowReference(main);
        var changes = 0;
        reference.Items.CollectionChanged += (_, _) => changes++;

        reference.ToggleAllCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        Assert.Empty(reference.Items.OfType<FeatureRowViewModel>());
        var watch = System.Diagnostics.Stopwatch.StartNew();
        reference.Items.OfType<BlockHeaderViewModel>().First(h => h.Technology == 2).ToggleCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        watch.Stop();

        Assert.Equal(304, reference.Items.OfType<FeatureRowViewModel>().Count());
        // Contraer y expandir avisan a la lista una vez por bloque, no una por fila.
        Assert.True(changes <= 6, $"{changes} avisos a la lista");
        Assert.True(watch.ElapsedMilliseconds < 1000, $"{watch.ElapsedMilliseconds} ms");

        // Arrastrar de un extremo al otro de la tecnología también es un solo movimiento, en los dos sentidos.
        var block = reference.Items.OfType<FeatureRowViewModel>().ToList();
        changes = 0;
        reference.Drop(block[0], block[^1]);
        Assert.Equal(1, changes);
        Assert.Equal(304, reference.Items.OfType<FeatureRowViewModel>().First(r => r.Feature == block[0].Feature).Row.Pointer);
        changes = 0;
        reference.Drop(block[0], reference.Items.OfType<FeatureRowViewModel>().First());
        Assert.Equal(1, changes);
        Assert.Equal(1, block[0].Row.Pointer);
    }

    [AvaloniaFact]
    public async Task La_lista_siempre_queda_en_el_orden_de_la_referencia()
    {
        var (_, main, dialogs) = await Open();
        dialogs.DeleteAnswer = DeleteChoice.Delete;
        var reference = ShowReference(main);
        var random = new Random(7);

        for (var step = 0; step < 300; step++)
        {
            var rows = reference.Items.OfType<FeatureRowViewModel>().ToList();
            reference.Selected = rows.Count > 0 ? rows[random.Next(rows.Count)] : null;
            switch (random.Next(7))
            {
                case 0 or 1:
                    reference.AddCommand.Execute(null);
                    break;
                case 2 when rows.Count > 3:
                    await reference.DeleteCommand.ExecuteAsync(null);
                    break;
                case 3:
                    reference.MoveUpCommand.Execute(null);
                    break;
                case 4:
                    reference.MoveDownCommand.Execute(null);
                    break;
                case 5 when rows.Count > 1:
                    reference.Drop(rows[random.Next(rows.Count)], rows[random.Next(rows.Count)]);
                    break;
                default:
                    var headers = reference.Items.OfType<BlockHeaderViewModel>().ToList();
                    headers[random.Next(headers.Count)].ToggleCommand.Execute(null);
                    break;
            }

            // Lo mismo que montaría la lista desde cero: cabecera de cada tecnología y sus filas si está expandida.
            var expected = Layout.Rows(main.Session.Project, reference.Reference).GroupBy(r => r.Technology)
                .SelectMany(block =>
                {
                    var header = reference.Items.OfType<BlockHeaderViewModel>().Single(h => h.Technology == block.Key);
                    return new[] { $"T{block.Key}" }.Concat(header.IsExpanded ? block.Select(r => r.Offset) : []);
                });
            var actual = reference.Items.Select(i => i is BlockHeaderViewModel h ? $"T{h.Technology}" : ((FeatureRowViewModel)i).Offset);
            Assert.Equal(expected, actual);
            Assert.All(reference.Items.OfType<FeatureRowViewModel>(), r => Assert.Same(r.Feature, Layout.Rows(main.Session.Project, reference.Reference).Single(l => l.Offset == r.Offset).Feature));
        }
    }

    [AvaloniaFact]
    public async Task Contraer_una_tecnologia_oculta_sus_filas_y_se_recuerda()
    {
        var (_, main, _) = await Open();
        var reference = ShowReference(main);
        var header = reference.Items.OfType<BlockHeaderViewModel>().First(h => h.Technology == 2);
        var count = reference.Items.Count;

        header.ToggleCommand.Execute(null);
        Assert.False(header.IsExpanded);
        Assert.Equal(count - 4, reference.Items.Count);
        Assert.DoesNotContain(reference.Items.OfType<FeatureRowViewModel>(), r => r.Row.Technology == 2);

        // Sigue contraída al volver a la referencia.
        main.SelectedNode = main.FindNode(NavKind.Project);
        reference = ShowReference(main);
        Assert.False(reference.Items.OfType<BlockHeaderViewModel>().First(h => h.Technology == 2).IsExpanded);

        // Ir a una feature de esa tecnología (desde un aviso) la expande.
        reference.Select(main.Session.Project.References[0].Features.First(f => f.ClientReference == "W3"));
        Assert.Equal("W3", reference.Selected!.Feature.ClientReference);
        Assert.True(reference.Items.OfType<BlockHeaderViewModel>().First(h => h.Technology == 2).IsExpanded);

        reference.ToggleAllCommand.Execute(null);
        Assert.True(reference.AllCollapsed);
        Assert.Empty(reference.Items.OfType<FeatureRowViewModel>());
        reference.ToggleAllCommand.Execute(null);
        Assert.Equal(count, reference.Items.Count);
    }

    [AvaloniaFact]
    public async Task Al_arrastrar_se_ve_donde_caera_la_fila()
    {
        var (window, main, _) = await Open();
        var reference = ShowReference(main);
        Dispatcher.UIThread.RunJobs();
        var view = window.GetVisualDescendants().OfType<ReferenceView>().Single();
        var rows = view.FindControl<ListBox>("Rows")!;
        Point Centre(Control control) => control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
        Control Grip(string client) => rows.ContainerFromItem(Row(reference, client))!.GetVisualDescendants().OfType<Border>()
            .First(b => b.Cursor is not null);

        var from = Centre(Grip("W3"));
        var to = Centre(rows.ContainerFromItem(Row(reference, "W1"))!);
        window.MouseDown(from, MouseButton.Left);
        window.MouseMove(new Point(from.X, to.Y));
        Dispatcher.UIThread.RunJobs();

        Assert.True(view.FindControl<Border>("DropLine")!.IsVisible);
        Assert.Equal("0102004 → 0102001", view.FindControl<TextBlock>("DropText")!.Text);
        Assert.Contains("dragging", rows.ContainerFromItem(Row(reference, "W3"))!.Classes);
        Shot(window, "13-arrastrar");

        window.MouseUp(new Point(from.X, to.Y), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        Assert.False(view.FindControl<Border>("DropLine")!.IsVisible);
        Assert.Equal("0102001", Row(reference, "W3").Offset);
    }

    [AvaloniaTheory]
    [InlineData(1400)]
    [InlineData(900)]
    public async Task Un_tipo_largo_no_descuadra_las_columnas(double width)
    {
        var (window, main, _) = await Open(width);
        main.Session.Catalog.Types.First(t => t.Number == 2).Detail = string.Concat(Enumerable.Repeat(" texto muy largo del tipo", 12));
        var reference = ShowReference(main);
        reference.ShowForm = false;
        Dispatcher.UIThread.RunJobs();
        var rows = window.GetVisualDescendants().OfType<ReferenceView>().Single().FindControl<ListBox>("Rows")!;
        Rect Column(string client, int column)
        {
            var grid = rows.ContainerFromItem(Row(reference, client))!.GetVisualDescendants().OfType<Grid>().First();
            var cell = grid.Children.First(c => Grid.GetColumn(c) == column);
            return new Rect(cell.TranslatePoint(new Point(0, 0), window)!.Value, cell.Bounds.Size);
        }

        foreach (var column in new[] { 4, 5, 6 })
        {
            Assert.Equal(Column("W1", column).X, Column("W2_01", column).X, 0.5);
            Assert.Equal(Column("W1", column).Width, Column("W2_01", column).Width, 0.5);
        }
        Shot(window, $"14-tipo-largo-{width}");
    }

    [AvaloniaFact]
    public async Task Anadir_reservas_desde_la_cabecera_y_desde_el_menu()
    {
        var (window, main, dialogs) = await Open();
        var reference = ShowReference(main);
        var offsets = reference.Items.OfType<FeatureRowViewModel>().ToDictionary(r => r.Feature, r => r.Offset);
        BlockHeaderViewModel Header(int technology) => reference.Items.OfType<BlockHeaderViewModel>().Single(h => h.Technology == technology);
        int Count(int technology) => Layout.Rows(main.Session.Project, reference.Reference).Count(r => r.Technology == technology);

        // Desde la cabecera: solo esa tecnología, al final, y queda seleccionada la última.
        dialogs.PromptAnswer = ["3"];
        await Header(2).AddReservesCommand.ExecuteAsync(null);
        Assert.Contains(dialogs.Prompts, p => p.Contains("Añadir reservas a 02") && p.Contains("ahora 4 posiciones · 1 reservas"));
        Assert.Equal(7, Count(2));
        Assert.Equal((true, "0102007"), (reference.Selected!.IsReserve, reference.Selected.Offset));
        Assert.Equal("7 posiciones · 4 reservas", Header(2).Detail);
        Assert.All(offsets, o => Assert.Equal(o.Value, Layout.Rows(main.Session.Project, reference.Reference).Single(r => r.Feature == o.Key).Offset));
        Shot(window, "15-reservas");

        // Desde el menú: en cada tecnología de la referencia.
        dialogs.PromptAnswer = ["2"];
        await reference.AddReservesToAllCommand.ExecuteAsync(null);
        Assert.Equal((9, 3, 3), (Count(2), Count(3), Count(10)));

        // Un número que no vale avisa y no cambia nada; cancelar tampoco.
        dialogs.PromptAnswer = ["0"];
        await Header(3).AddReservesCommand.ExecuteAsync(null);
        Assert.Contains(dialogs.Messages, m => m.Contains("entre 1 y 999"));
        dialogs.PromptAnswer = null;
        Assert.Equal(3, Count(3));

        // Se deshace de una vez.
        main.UndoCommand.Execute(null);
        reference = Assert.IsType<ReferenceViewModel>(main.Editor);
        Assert.Equal(7, Layout.Rows(main.Session.Project, reference.Reference).Count(r => r.Technology == 2));
    }

    [AvaloniaFact]
    public async Task Solo_se_puede_soltar_en_la_misma_tecnologia()
    {
        var (_, main, _) = await Open();
        var reference = ShowReference(main);

        Assert.True(ReferenceViewModel.CanDrop(Row(reference, "W3"), Row(reference, "W1")));
        Assert.False(ReferenceViewModel.CanDrop(Row(reference, "W3"), Row(reference, "W3")));
        Assert.False(ReferenceViewModel.CanDrop(Row(reference, "W3"), Row(reference, "MARCA")));
    }

    [AvaloniaFact]
    public async Task Pegar_filas_del_portapapeles()
    {
        var (_, main, dialogs) = await Open();
        var reference = ShowReference(main);
        reference.Selected = Row(reference, "W3");
        dialogs.Clipboard = "W20\t1\t1\t10020\tFalse\t1000FIX01\t1000R1\t1\r\nW21\t1\t1\t10021\tTrue\t1000FIX01\t1000R1\t1\r\n";

        await reference.PasteCommand.ExecuteAsync(null);

        Assert.Equal("0102005", Row(reference, "W20").Offset);
        Assert.Equal("0102006", Row(reference, "W21").Offset);
        Assert.True(Row(reference, "W21").Critical);
    }

    [AvaloniaFact]
    public async Task Duplicar_referencia_la_abre()
    {
        var (_, main, _) = await Open();
        var reference = ShowReference(main);

        await reference.DuplicateCommand.ExecuteAsync(null);

        var copy = Assert.IsType<ReferenceViewModel>(main.Editor);
        Assert.Equal(3, copy.Reference.Number);
        Assert.Equal(main.Session.Project.References[0].Features.Count, copy.Reference.Features.Count);
    }

    [AvaloniaFact]
    public async Task Tamanos_copia_las_constantes()
    {
        var (_, main, dialogs) = await Open();
        main.SelectedNode = main.FindNode(NavKind.Sizes);
        var sizes = Assert.IsType<SizesViewModel>(main.Editor);

        await sizes.CopyCommand.ExecuteAsync(null);

        Assert.Contains("Tech02_Ref_01\t4\r\n", dialogs.Clipboard);
        Assert.Contains("Tech10_Ref_02\t1\r\n", dialogs.Clipboard);
    }

    [AvaloniaFact]
    public async Task Generar_escribe_los_archivos_y_anade_la_version()
    {
        var (_, main, dialogs) = await Open();
        var output = Path.Combine(_dir, "salida");
        dialogs.Folder = output;
        var generate = new GenerateViewModel(main) { AddVersion = true, VersionText = "Primera generación" };

        await generate.GenerateCommand.ExecuteAsync(null);

        var today = DateOnly.FromDateTime(DateTime.Today);
        Assert.True(File.Exists(Path.Combine(output, "Plantilla", ExcelWriters.TemplateFileName(main.Session.Project, today))));
        Assert.True(File.Exists(Path.Combine(output, "FIT", ExcelWriters.FitFileName(main.Session.Project, today))));
        Assert.True(File.Exists(Path.Combine(output, "Config_Reference_FB", "User_Config_Reference_FB.awl")));
        Assert.True(File.Exists(Path.Combine(output, "CheckTech_DB", "CheckTechTemplate_DB_Init_1000FIX01.scl")));
        Assert.Equal("Primera generación", main.Session.Project.Versions[^1].Description);
        Assert.NotNull(main.Session.Project.LastOffsets);
        Assert.Contains(dialogs.Messages, m => m.Contains("User_TableInfo_DB.db"));

        // Después de mover algo, la siguiente generación lo enseña.
        var reference = ShowReference(main);
        reference.Drop(Row(reference, "W3"), Row(reference, "W1"));
        var next = new GenerateViewModel(main);
        Assert.True(next.HasChanges);
        Assert.True(next.AddVersion);
        Assert.Contains(next.Changes, c => c.StartsWith("0102004 → 0102001", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public async Task Guardar_y_abrir_el_proyecto()
    {
        var (_, main, dialogs) = await Open();
        var path = Path.Combine(_dir, "PRUEBA.fitproj");
        dialogs.Files.Enqueue(path);

        await main.SaveAsCommand.ExecuteAsync(null);
        Assert.True(File.Exists(path));
        Assert.True(File.Exists(main.Session.CatalogPath));
        Assert.False(main.Session.Dirty);

        main.Session.New();
        dialogs.Files.Enqueue(path);
        await main.OpenCommand.ExecuteAsync(null);
        Assert.Equal("PRUEBA", main.Session.Project.Name);
        Assert.Equal(2, main.Session.Project.References.Count);
    }

    [AvaloniaFact]
    public async Task Los_avisos_llevan_a_la_feature()
    {
        var (_, main, _) = await Open();
        var reference = ShowReference(main, 2);
        Row(reference, "W3").FeatureId = "10001";

        var issue = main.Issues.First(i => i.Issue.Text.Contains("10001"));
        main.SelectedNode = main.FindNode(NavKind.Project);
        main.SelectedIssue = issue;

        var editor = Assert.IsType<ReferenceViewModel>(main.Editor);
        Assert.Equal(2, editor.Reference.Number);
        Assert.Equal("W3", editor.Selected!.Feature.ClientReference);
    }

    [AvaloniaFact]
    public async Task Pantallas_de_techs_reparto_nombres_cortos_y_deshacer()
    {
        var (_, main, _) = await Open();
        main.SelectedNode = main.FindNode(NavKind.TechScreens);
        var screens = Assert.IsType<TechScreensViewModel>(main.Editor);
        Assert.False(screens.HasPages);

        screens.Starts[0].SelectedIndex = 7;
        screens.Starts[1].SelectedIndex = 7;
        screens.ShortNames.First(s => s.Name == "1000R1").ShortName = "R10.1";

        Assert.Equal([7, 8], screens.Pages.Select(p => p.Page.Number));
        Assert.Single(screens.Warnings);
        // Lo elegido no cambia: solo se corre en el reparto, con su aviso.
        Assert.Equal(7, main.Session.Project.TechScreens.ReferenceStarts[2]);
        Assert.Contains(screens.SelectedPage!.Page.Boxes[0].Rows, r => r.Text == "R10.1 - W1");

        main.UndoCommand.Execute(null);
        Assert.False(main.Session.Project.TechScreens.ShortNames.ContainsKey("1000R1"));
    }

    [AvaloniaFact]
    public async Task Pantallas_de_techs_mover_recuadros_y_deshacer()
    {
        var (window, main, _) = await Open();
        main.SelectedNode = main.FindNode(NavKind.TechScreens);
        var screens = Assert.IsType<TechScreensViewModel>(main.Editor);
        screens.Starts[0].SelectedIndex = 7;
        Dispatcher.UIThread.RunJobs();

        var box = screens.SelectedPage!.Page.Boxes.Single(b => b.Title == "4000FIX01");
        screens.OnBoxClicked(box);
        Assert.True(screens.HasSelectedBox);
        Assert.Equal(["07", "Pantalla nueva"], screens.BoxScreenOptions);

        screens.OnBoxDragged(box, 1001, 501);
        Assert.Equal((1000m, 504m), (screens.BoxLeft, screens.BoxTop));
        Shot(window, "14-mover-recuadro");

        // A una pantalla nueva: la vista previa se va con él.
        screens.BoxScreenIndex = 1;
        Assert.Equal(8, screens.SelectedPage!.Page.Number);
        Assert.Equal("4000FIX01", screens.SelectedBoxTitle);

        main.UndoCommand.Execute(null);
        var undone = Assert.IsType<TechScreensViewModel>(main.Editor);
        Assert.Equal([7], undone.Pages.Select(p => p.Page.Number));
        Assert.Equal(1000, undone.Pages[0].Page.Boxes.Single(b => b.Title == "4000FIX01").Left);
    }

    [AvaloniaFact]
    public async Task Elegir_otro_recuadro_no_lo_mueve_de_pantalla()
    {
        var (_, main, _) = await Open();
        main.SelectedNode = main.FindNode(NavKind.TechScreens);
        Dispatcher.UIThread.RunJobs();
        var screens = Assert.IsType<TechScreensViewModel>(main.Editor);
        screens.Starts[0].SelectedIndex = 7;
        Dispatcher.UIThread.RunJobs();
        TechScreenBox Box(string title) => screens.SelectedPage!.Page.Boxes.Single(b => b.Title == title);

        // 3000FIX01 y 4000FIX01 a una pantalla nueva (la 08).
        screens.OnBoxClicked(Box("3000FIX01"));
        screens.BoxScreenIndex = 1;
        Dispatcher.UIThread.RunJobs();
        screens.SelectedPage = screens.Pages.Single(p => p.Page.Number == 7);
        Dispatcher.UIThread.RunJobs();
        screens.OnBoxClicked(Box("4000FIX01"));
        Dispatcher.UIThread.RunJobs();
        screens.BoxScreenIndex = 1;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(8, screens.SelectedPage!.Page.Number);

        // 3000FIX01 vuelve a la primera; luego, en la 08, se elige 4000FIX01 sin tocar nada.
        screens.OnBoxClicked(Box("3000FIX01"));
        Dispatcher.UIThread.RunJobs();
        screens.BoxScreenIndex = 0;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(7, screens.SelectedPage!.Page.Number);
        screens.SelectedPage = screens.Pages.Single(p => p.Page.Number == 8);
        Dispatcher.UIThread.RunJobs();
        screens.OnBoxClicked(Box("4000FIX01"));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(8, screens.SelectedPage!.Page.Number);
        Assert.Contains(screens.SelectedPage.Page.Boxes, b => b.Title == "4000FIX01");
        Assert.Equal(1, screens.BoxScreenIndex);
    }

    [AvaloniaFact]
    public async Task La_ficha_sale_cerrada_al_abrir_una_referencia()
    {
        var (_, main, _) = await Open();

        var reference = ShowReference(main);
        Assert.False(reference.ShowForm);

        reference.ToggleFormCommand.Execute(null);
        Assert.True(reference.ShowForm);
        Assert.False(ShowReference(main, 2).ShowForm);
    }

    [AvaloniaFact]
    public async Task Un_solo_menu_de_pantallas_con_screen_names_y_techs()
    {
        var (_, main, _) = await Open();

        var group = main.FindNode(NavKind.ScreenGroup)!;

        Assert.Equal([NavKind.Screens, NavKind.TechScreens], group.Children.Select(c => c.Kind));
        Assert.DoesNotContain(main.Tree[0].Children, c => c.Kind is NavKind.Screens or NavKind.TechScreens);
        main.SelectedNode = group;
        Assert.IsType<ScreensViewModel>(main.Editor);
    }

    [AvaloniaFact]
    public async Task Pantallas_de_techs_estilo_y_restaurar()
    {
        var (window, main, _) = await Open();
        main.SelectedNode = main.FindNode(NavKind.TechScreens);
        var screens = Assert.IsType<TechScreensViewModel>(main.Editor);
        screens.Starts[0].SelectedIndex = 7;

        screens.Colors[0].Color = Avalonia.Media.Color.FromRgb(255, 240, 200);
        screens.RowHeight = 60;

        Assert.Equal("#FFF0C8", main.Session.Project.TechScreens.Style.BoxBack);
        var rows = screens.SelectedPage!.Page.Boxes[0].Rows;
        Assert.Equal(60, rows[1].Top - rows[0].Top);
        Shot(window, "13-estilo-pantallas");

        screens.ResetStyleCommand.Execute(null);
        Assert.Equal(new TechScreenStyle().BoxBack, main.Session.Project.TechScreens.Style.BoxBack);
        Assert.Equal(40, screens.RowHeight);

        main.UndoCommand.Execute(null);
        Assert.Equal(60, main.Session.Project.TechScreens.Style.RowHeight);
    }

    [AvaloniaFact]
    public async Task Tecnologia_de_un_tipo_y_nombre_de_la_tecnologia()
    {
        var (_, main, _) = await Open();
        main.SelectedNode = main.FindNode(NavKind.Technologies);
        var techs = Assert.IsType<TechnologiesViewModel>(main.Editor);

        techs.Technologies[3].Name = "Marcadora";
        var marking = techs.Types.First(t => t.Number == 71);
        marking.Technology = marking.Choices.First(c => c.Number == 4);

        Assert.Equal("Marcadora", main.Session.Project.TechnologyName(3));
        Assert.Equal(4, main.Session.Project.TypeTechnologies[71]);
        var reference = ShowReference(main);
        Assert.Equal("0104001", Row(reference, "MARCA").Offset);
    }

    [AvaloniaFact]
    public async Task Acerca_de_y_version_nueva()
    {
        var (window, main, dialogs) = await Open();

        await main.AboutCommand.ExecuteAsync(null);
        Assert.Contains("Acerca de: Creado por Ignacio Suarez. https://github.com/nsuarez75/GST-FIT-Generator", dialogs.Messages);

        await main.CheckForUpdates(_ => Task.FromResult<ReleaseInfo?>(new ReleaseInfo("v99.0.0", new Version(99, 0, 0), "https://github.com/x/y/releases/tag/v99.0.0")));
        Assert.True(main.HasUpdate);
        Shot(window, "12-version-nueva");
        main.DismissUpdateCommand.Execute(null);
        Assert.False(main.HasUpdate);
    }
}
