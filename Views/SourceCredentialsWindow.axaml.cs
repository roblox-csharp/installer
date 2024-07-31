using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Installer.ViewModels;

namespace Installer.Views;

public partial class SourceCredentialsWindow : Window
{
    new SourceCredentialsWindowViewModel? DataContext { get; set; }
    private readonly bool _sourceAlreadyAdded;

    public SourceCredentialsWindow() : this(false)
    { 
    }

    public SourceCredentialsWindow(bool sourceAlreadyAdded)
    {
        DataContext = new SourceCredentialsWindowViewModel();
        _sourceAlreadyAdded = sourceAlreadyAdded;
        if (_sourceAlreadyAdded)
        {
            ContinueInstallation();
            return;
        }

        Closed += (s, e) => ContinueInstallation();
        InitializeComponent();
        Show();
    }

    private void InitializeComponent()
        => AvaloniaXamlLoader.Load(this);

    private void SubmitButton_Click(object sender, RoutedEventArgs e)
    {
        ContinueInstallation(true);
    }

    private void ContinueInstallation(bool close)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            Installation.OnCredentialsAcquired(this, _sourceAlreadyAdded);
            if (close)
            {
                Close();
            }
        });
    }

    private void ContinueInstallation()
    {
        ContinueInstallation(false);
    }
}