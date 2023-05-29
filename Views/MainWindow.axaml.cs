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
      InitializeComponent();
#if DEBUG
      this.AttachDevTools();
#endif
    }

    private void InitializeComponent()
    {
      AvaloniaXamlLoader.Load(this);
    }

    private void InstallButton_Click(object sender, RoutedEventArgs e)
    {
      // TODO: Implement the install logic here
    }

    private void SelectDirectoryButton_Click(object sender, RoutedEventArgs e)
    {
      // TODO: Implement the directory selection logic here
    }
  }
}
