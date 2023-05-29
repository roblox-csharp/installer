using ReactiveUI;
using System;
using System.Reactive;
using System.Threading.Tasks;

namespace CosmoInstaller.ViewModels
{
  public class MainWindowViewModel : ReactiveObject
  {
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
        defaultDirectory = "~";
      else if (OperatingSystem.IsMacOS())
        defaultDirectory = "/Applications";

      return defaultDirectory;
    }

    private async void InstallCosmo()
    {
      ProgressBarVisible = true;

      await Task.Run(() => Installation.InstallCosmo(UpdateProgress, _selectedDirectory));

      // ProgressBarVisible = false;
    }

    private void UpdateProgress(int progress)
    {
      ProgressBarValue = progress;
    }
  }
}
