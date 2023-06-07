using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using CosmoInstaller.ViewModels;

namespace CosmoInstaller.Views;

public partial class MainWindow : Window
{
  new MainWindowViewModel? DataContext { get; set; }

  public MainWindow()
  {
    DataContext = new MainWindowViewModel();
    InitializeComponent();
  }

  private void InitializeComponent()
    => AvaloniaXamlLoader.Load(this);

  private void UpdateProgress(int progress)
  {
    if (DataContext != null)
      DataContext.ProgressBarValue = progress;
  }

  [System.Obsolete]
  private async void SelectDirectoryButton_Click(object sender, RoutedEventArgs e)
  {
    OpenFolderDialog dialog = new OpenFolderDialog();
    dialog.Title = "Select installation directory";

    string? selectedDirectory = await dialog.ShowAsync(this);
    if (!string.IsNullOrEmpty(selectedDirectory) && DataContext != null)
      DataContext.SelectedDirectory = selectedDirectory;
  }
}