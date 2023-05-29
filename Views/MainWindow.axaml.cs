using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using CosmoInstaller.ViewModels;

namespace CosmoInstaller.Views;

public partial class MainWindow : Window
{
  public MainWindow()
  {
    DataContext = new MainWindowViewModel();
    InitializeComponent();
  }

  private void InitializeComponent()
    => AvaloniaXamlLoader.Load(this);

  private void UpdateProgress(int progress)
  {
    var viewModel = DataContext as MainWindowViewModel;
    if (viewModel != null)
      viewModel.ProgressBarValue = progress;
  }

  [System.Obsolete]
  private async void SelectDirectoryButton_Click(object sender, RoutedEventArgs e)
  {
    var viewModel = DataContext as MainWindowViewModel;
    OpenFolderDialog dialog = new OpenFolderDialog();
    dialog.Title = "Select installation directory";

    string? selectedDirectory = await dialog.ShowAsync(this);
    if (!string.IsNullOrEmpty(selectedDirectory) && viewModel != null)
      viewModel.SelectedDirectory = selectedDirectory;
  }
}