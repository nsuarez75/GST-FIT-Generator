using GstFitGenerator.App.ViewModels;

namespace GstFitGenerator.App.Tests;

// Diálogos que responden solos: aceptan todo y devuelven las rutas y respuestas preparadas por el test.
internal sealed class FakeDialogs : IDialogs
{
    public List<string> Messages { get; } = [];
    public List<string> Confirms { get; } = [];
    public List<IReadOnlyList<string>> ChangeLists { get; } = [];
    public Queue<string?> Files { get; } = new();
    public string? Folder { get; set; }
    public bool ConfirmAnswer { get; set; } = true;
    public DeleteChoice DeleteAnswer { get; set; } = DeleteChoice.Reserve;
    public string? Clipboard { get; set; }

    public Task Message(string title, string text)
    {
        Messages.Add(title + ": " + text);
        return Task.CompletedTask;
    }

    public Task About(string title, string text, string link) => Message(title, text + " " + link);

    public Task<bool> Confirm(string title, string text, string accept)
    {
        Confirms.Add(title + ": " + text);
        return Task.FromResult(ConfirmAnswer);
    }

    public Task<SaveChoice> AskSave() => Task.FromResult(SaveChoice.Discard);

    public Task<DeleteChoice> AskDelete(string title, string text) => Task.FromResult(DeleteAnswer);

    public Task<(bool Accepted, bool Option)> Changes(string title, IReadOnlyList<string> changes, IReadOnlyList<string> warnings, string? option = null)
    {
        ChangeLists.Add(changes);
        return Task.FromResult((true, true));
    }

    public Task<string[]?> Prompt(string title, IReadOnlyList<string> labels, IReadOnlyList<string> values) =>
        Task.FromResult<string[]?>(values.ToArray());

    public Task<string?> OpenFile(string title, FileFilter filter) => Task.FromResult(Files.Count > 0 ? Files.Dequeue() : null);

    public Task<string?> SaveFile(string title, FileFilter filter, string suggestedName) => Task.FromResult(Files.Count > 0 ? Files.Dequeue() : null);

    public Task<string?> PickFolder(string title) => Task.FromResult(Folder);

    public Task CopyText(string text)
    {
        Clipboard = text;
        return Task.CompletedTask;
    }

    public Task<string?> PasteText() => Task.FromResult(Clipboard);

    public Task ShowCatalog(CatalogViewModel catalog) => Task.CompletedTask;

    public Task ShowGenerate(GenerateViewModel generate) => Task.CompletedTask;
}
