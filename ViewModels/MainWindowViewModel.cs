using System;
using System.IO;
using System.Reactive;
using ReactiveUI;
using Avalonia.Threading;
using MessageBox.Avalonia;

namespace Installer.ViewModels;

public class MainWindowViewModel : ReactiveObject
{
    private string _titleText = "Welcome to the roblox-cs installer!";
    public string TitleText
    {
        get => _titleText;
        set => this.RaiseAndSetIfChanged(ref _titleText, value);
    }

    private bool _isNotInstalling = true;
    public bool IsNotInstalling
    {
        get => _isNotInstalling;
        set => this.RaiseAndSetIfChanged(ref _isNotInstalling, value);
    }

    private bool _selectDirectoryButtonEnabled = true;
    public bool SelectDirectoryButtonEnabled
    {
        get => _selectDirectoryButtonEnabled;
        set => this.RaiseAndSetIfChanged(ref _selectDirectoryButtonEnabled, value);
    }

    private string _selectedDirectory;
    public string SelectedDirectory
    {
        get => _selectedDirectory;
        set => this.RaiseAndSetIfChanged(ref _selectedDirectory, value);
    }

    private bool _progressBarVisible;
    public bool ProgressBarVisible
    {
        get => _progressBarVisible;
        set => this.RaiseAndSetIfChanged(ref _progressBarVisible, value);
    }

    private int _progressBarValue;
    public int ProgressBarValue
    {
        get => _progressBarValue;
        set => this.RaiseAndSetIfChanged(ref _progressBarValue, value);
    }

    private bool _finishedCloseVisible = false;
    public bool FinishedCloseVisible
    {
        get => _finishedCloseVisible;
        set => this.RaiseAndSetIfChanged(ref _finishedCloseVisible, value);
    }

    public ReactiveCommand<Unit, Unit> InstallCommand { get; }
    public ReactiveCommand<Unit, Unit> SuccessfulExitCommand { get; }

    private bool _errored = false;

    public MainWindowViewModel()
    {
        _selectedDirectory = GetDefaultInstallationDirectory();
        InstallCommand = ReactiveCommand.Create(InstallRbxcs);
        SuccessfulExitCommand = ReactiveCommand.Create(SuccessfulExit);
        Console.WriteLine("Initialized app.");
    }

    public void UpdateTitle(string title)
        => TitleText = title;

    private string GetDefaultInstallationDirectory()
    {
        string defaultDirectory = string.Empty;
        if (OperatingSystem.IsWindows())
            defaultDirectory = "C:\\Program Files";
        else if (OperatingSystem.IsLinux())
            defaultDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        else if (OperatingSystem.IsMacOS())
            defaultDirectory = "/Applications";

        return defaultDirectory;
    }

    private void InstallRbxcs()
    {
        ProgressBarVisible = true;
        IsNotInstalling = false;
        TitleText = "Installing...";

        var absolutePath = Path.Combine(Path.GetFullPath(_selectedDirectory), "roblox-cs");
        Dispatcher.UIThread.InvokeAsync(async () =>
        {
            try
            {
                await Installation.InstallRbxcs(
                  UpdateProgress,
                  UpdateTitle,
                  MarkErrored,
                  absolutePath
                );
            }
            catch (Exception err)
            {
                MarkErrored();
                UpdateTitle("Error!");

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    MessageBoxManager
                        .GetMessageBoxStandardWindow("Error", err.Message)
                        .Show()
                        .ContinueWith(_ => Environment.Exit(1));
                });
            }

            ProgressBarVisible = false;
            if (_errored) return;
            FinishedCloseVisible = true;
        });
    }

    private async void SuccessfulExit()
        => await Dispatcher.UIThread.InvokeAsync(() => Environment.Exit(0));

    private void MarkErrored()
    {
        _errored = true;
        ProgressBarVisible = false;
    }

    private void UpdateProgress(int progress)
        => ProgressBarValue = progress;
}