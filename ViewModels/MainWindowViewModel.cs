using Avalonia.Threading;
using MessageBox.Avalonia;
using ReactiveUI;
using System;
using System.IO;
using System.Reactive;
using System.Threading.Tasks;

namespace CosmoInstaller.ViewModels
{
  public class MainWindowViewModel : ReactiveObject
  {
    private string _titleText = "Welcome to the Cosmo installer!";
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

    public ReactiveCommand<Unit, Unit> InstallCommand { get; }

    public MainWindowViewModel()
    {
      _selectedDirectory = GetDefaultInstallationDirectory();
      InstallCommand = ReactiveCommand.Create(InstallCosmo);
    }

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

    private async void InstallCosmo()
    {
      ProgressBarVisible = true;
      IsNotInstalling = false;
      TitleText = "Installing...";

      await Task.Run(() => Installation.InstallCosmo(
        UpdateProgress,
        UpdateTitle,
        Path.GetFullPath(Path.Combine(_selectedDirectory, ".cosmo"))
      )).ContinueWith(prev => ProgressBarVisible = false)
        .ContinueWith(prev => {
          Dispatcher.UIThread.InvokeAsync(async () => {
            await MessageBoxManager
              .GetMessageBoxStandardWindow("Installation Finished", "Successfully installed Cosmo! You may have to restart your shell for changes to take effect.")
              .Show();

            Environment.Exit(0);
          });
        });
    }

    private void UpdateTitle(string title)
    {
      TitleText = title;
    }

    private void UpdateProgress(int progress)
    {
      ProgressBarValue = progress;
    }
  }
}
