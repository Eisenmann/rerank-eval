using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ReRankEval.App.ViewModels;

namespace ReRankEval.App.Views;

public partial class DeepAnalysisView : UserControl
{
    public DeepAnalysisView()
    {
        InitializeComponent();
    }

    private async void OnExportWorstCsvClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not DeepAnalysisViewModel vm) return;
        var topLevel = TopLevel.GetTopLevel(this)!;
        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export worst queries",
            SuggestedFileName = "worst_queries.csv",
            FileTypeChoices = [new FilePickerFileType("CSV") { Patterns = ["*.csv"] }]
        });
        if (file is null) return;
        await vm.ExportWorstCsvCommand.ExecuteAsync(file.Path.LocalPath);
    }
}
