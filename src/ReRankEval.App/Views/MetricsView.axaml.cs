using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ReRankEval.App.ViewModels;

namespace ReRankEval.App.Views;

public partial class MetricsView : UserControl
{
    public MetricsView()
    {
        InitializeComponent();
    }

    private async void OnExportCsvClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MetricsViewModel vm) return;
        var file = await TopLevel.GetTopLevel(this)!.StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = "Export metrics as CSV",
                SuggestedFileName = "metrics.csv",
                FileTypeChoices = [new FilePickerFileType("CSV") { Patterns = ["*.csv"] }]
            });
        if (file is not null)
            await vm.ExportCsvAsync(file.Path.LocalPath);
    }

    private async void OnExportJsonClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MetricsViewModel vm) return;
        var file = await TopLevel.GetTopLevel(this)!.StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = "Export metrics as JSON",
                SuggestedFileName = "metrics.json",
                FileTypeChoices = [new FilePickerFileType("JSON") { Patterns = ["*.json"] }]
            });
        if (file is not null)
            await vm.ExportJsonAsync(file.Path.LocalPath);
    }
}
