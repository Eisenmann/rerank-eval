using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ReRankEval.App.ViewModels;

namespace ReRankEval.App.Views;

public partial class FineTuningView : UserControl
{
    public FineTuningView()
    {
        InitializeComponent();
    }

    private async void OnBrowseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not FineTuningViewModel vm) return;
        var topLevel = TopLevel.GetTopLevel(this)!;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select training data file",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("JSONL / CSV") { Patterns = ["*.jsonl", "*.csv", "*.txt"] },
                new FilePickerFileType("All files")   { Patterns = ["*.*"] }
            ]
        });

        if (files.Count > 0)
            vm.SetDataFilePath(files[0].Path.LocalPath);
    }
}
