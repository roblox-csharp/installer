using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Installer.ViewModels;

namespace Installer.Views;

public partial class MainWindow : Window
{
    new MainWindowViewModel? DataContext { get; set; }

    public MainWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
        => AvaloniaXamlLoader.Load(this);

    [Obsolete]
    private async void SelectDirectoryButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog();
        dialog.Title = "Select installation directory";

        var selectedDirectory = await dialog.ShowAsync(this);
        Console.WriteLine("selected dir: " + selectedDirectory);
        if (!string.IsNullOrEmpty(selectedDirectory))
        {
            ((MainWindowViewModel)base.DataContext!).SelectedDirectory = selectedDirectory;
        }
    }
}