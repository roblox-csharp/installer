using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace CosmoInstaller.Views
{

  public partial class MainWindow : Window
  {
    public MainWindow()
    {
      _selectedDirectory = GetDefaultInstallationDirectory();
      InitializeComponent();
#if DEBUG
      this.AttachDevTools();
#endif
    }

    private void InitializeComponent()
    {
      AvaloniaXamlLoader.Load(this);
    }

    private string _selectedDirectory;

    private string GetDefaultInstallationDirectory()
    {
      string defaultDirectory = string.Empty;
      if (OperatingSystem.IsWindows())
        defaultDirectory = "C:\\Program Files\\Cosmo";
      else if (OperatingSystem.IsLinux())
        defaultDirectory = "~/.cosmo";
      else if (OperatingSystem.IsMacOS())
        defaultDirectory = "/Applications";

      return defaultDirectory;
    }

    private void InstallButton_Click(object sender, RoutedEventArgs e)
    {
      // TODO: Implement the install logic here
    }

    private async void SelectDirectoryButton_Click(object sender, RoutedEventArgs e)
    {
      OpenFolderDialog dialog = new OpenFolderDialog();
      dialog.Title = "Select installation directory";

      string? selectedDirectory = await dialog.ShowAsync(this);
      if (!string.IsNullOrEmpty(selectedDirectory))
        _selectedDirectory = selectedDirectory;
    }
  }
}
