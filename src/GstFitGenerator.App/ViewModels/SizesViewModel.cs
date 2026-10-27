using CommunityToolkit.Mvvm.Input;
using GstFitGenerator.Core;

namespace GstFitGenerator.App.ViewModels;

// Tamaños de los arrays en TIA: una fila por referencia y una columna por tecnología (features + reservas).
public sealed partial class SizesViewModel
{
    private readonly MainViewModel _host;
    private readonly List<SizeEntry> _entries;

    public SizesViewModel(MainViewModel host)
    {
        _host = host;
        _entries = Sizes.Compute(host.Session.Project);
        Headers = Enumerable.Range(1, Technology.Max)
            .Select(t => $"{t:00} · {host.Session.Project.TechnologyName(t)}")
            .ToList();
        Rows = _entries.GroupBy(e => e.Reference)
            .Select(g => new SizeRow(L.T("Referencia {0}", g.Key), g.Select(e => e.Count).ToList()))
            .ToList();
    }

    public IReadOnlyList<string> Headers { get; }
    public IReadOnlyList<SizeRow> Rows { get; }
    public bool IsEmpty => Rows.Count == 0;
    public string Text => Sizes.ToText(_entries);

    [RelayCommand]
    private async Task Copy()
    {
        await _host.Dialogs.CopyText(Text);
    }
}

public sealed record SizeRow(string Reference, IReadOnlyList<int> Counts);
