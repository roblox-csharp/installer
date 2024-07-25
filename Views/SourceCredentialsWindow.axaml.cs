using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Installer.ViewModels;

namespace Installer.Views;

public partial class SourceCredentialsWindow : Window
{
    new SourceCredentialsWindowViewModel? DataContext { get; set; }

    public SourceCredentialsWindow()
    {
        DataContext = new SourceCredentialsWindowViewModel();
        InitializeComponent();
    }

    private void InitializeComponent()
      => AvaloniaXamlLoader.Load(this);

    private void SubmitButton_Click(object sender, RoutedEventArgs e)
    {
        Installation.OnCredentialsAcquired(this);
        Close();
    }
}