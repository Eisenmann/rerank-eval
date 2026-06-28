using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ReRankEval.App.ViewModels;

namespace ReRankEval.App.Views;

public partial class DatasetView : UserControl
{
    public DatasetView()
    {
        InitializeComponent();
    }

    private async void OnLoadJsonlClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not DatasetViewModel vm) return;
        var files = await TopLevel.GetTopLevel(this)!.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Open JSONL Dataset",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("JSONL / JSON") { Patterns = ["*.jsonl", "*.json"] },
                    new FilePickerFileType("All files")    { Patterns = ["*"]                  }
                ]
            });
        if (files.Count > 0)
            await vm.LoadJsonlCommand.ExecuteAsync(files[0].Path.LocalPath);
    }

    private async void OnLoadCsvClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not DatasetViewModel vm) return;
        var files = await TopLevel.GetTopLevel(this)!.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Open CSV Dataset",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("CSV") { Patterns = ["*.csv"] },
                    new FilePickerFileType("All files") { Patterns = ["*"] }
                ]
            });
        if (files.Count > 0)
            await vm.LoadCsvCommand.ExecuteAsync(files[0].Path.LocalPath);
    }

    private async void OnLoadStatsClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is DatasetViewModel vm)
            await vm.LoadStatsForSelectedAsync();
    }
}
