using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Avalonia.Threading;
using MessageBox.Avalonia;

namespace CosmoInstaller;

public static class Installation
{
  private static readonly float _step = (1f / 9f) * 100;
  private static float progress = 0;
  private static Action<int>? _updateProgress;
  private static Action<string>? _updateTitle;

  public static void InstallCosmo(
    Action<int> updateProgress,
    Action<string> updateTitle,
    string path
  ) {
    _updateProgress = updateProgress;
    _updateTitle = updateTitle;

    Log("Creating installation environment...");
    if (Directory.Exists(path))
    {
      Log("Installation directory exists, skipping creation...");
    }
    else
    {
      try
      {
        Directory.CreateDirectory(path);
      }
      catch (Exception err)
      {
        string msg = $"Failed to create directory (run as administrator?): {err.Message}";
        ShowErrorMessageBox(msg);
        return;
      }
    }

    Log("Changing environment directory...");
    StepProgress();
    try
    {
      Environment.CurrentDirectory = path;
    }
    catch (Exception err)
    {
      string msg = $"Failed to change directory (run as administrator?): {err.Message}";
      ShowErrorMessageBox(msg);
      return;
    }

    StepProgress();
    Log("Pulling repository...");
    string[] dirEntries;
    try
    {
      dirEntries = Directory.GetFileSystemEntries(".");
    }
    catch (Exception err)
    {
      string msg = $"Failed to read the directory (run as administrator?): {err.Message}";
      ShowErrorMessageBox(msg);
      return;
    }

    if (dirEntries.Length != 0)
    {
      string pullArgs = "pull origin master --allow-unrelated-histories";
      ExecuteGitCommand(pullArgs, "Failed to pull from the repository (is git installed?)");
    }
    else
    {
      string cloneArgs = "clone https://github.com/cosmo-lang/cosmo.git .";
      ExecuteGitCommand(cloneArgs, "Failed to clone the repository (is git installed?)");
    }

    StepProgress();
    Log("Fetching tags...");
    ExecuteGitCommand("fetch --tags", "Failed to fetch release tags");

    StepProgress();
    Log("Fetching latest release...");
    string latestTag = ExecuteGitCommand("describe --tags --abbrev=0", "Failed to get the latest release tag");

    StepProgress();
    Log("Checking out latest release...");
    ExecuteGitCommand($"checkout {latestTag}", "Failed to checkout the latest release");

    ProcessResult crystalCheckOutput = ExecuteCommand("crystal", "-v");
    if (crystalCheckOutput.ExitCode != 0)
    {
      Log("Installing Crystal...");

      string crystalInstallArgs;
      if (OperatingSystem.IsWindows())
      {
        crystalInstallArgs = "-Command \"& { iex ((new-object net.webclient).DownloadString('https://dist.crystal-lang.org/install.ps1')) }\"";
        ExecuteCommand("powershell.exe", crystalInstallArgs);
      }
      else if (OperatingSystem.IsLinux())
      {
        crystalInstallArgs = "curl -sSL https://dist.crystal-lang.org/rpm/setup.sh | sudo bash";
        ExecuteCommand("sh", "-c", crystalInstallArgs);
      }
      else if (OperatingSystem.IsMacOS())
      {
        crystalInstallArgs = "install crystal";
        ExecuteCommand("brew", crystalInstallArgs);
      }
      Log("Successfully installed Crystal.");
    }
    else
    {
      Log("Crystal is already installed, continuing...");
    }

    StepProgress();

    Log("Building Cosmo...");
    Log("Installing dependencies...");
    ExecuteCommand("shards", "install");
    StepProgress();

    Log("Compiling...");
    ExecuteCommand("shards", "build --release");
    StepProgress();

    Log("Successfully built Cosmo.");
    Log("Adding Cosmo to PATH...");

    AddToPath(path);
    StepProgress();
    Log("Successfully installed Cosmo.");
  }

  private static string ExecuteGitCommand(string arguments, string errorMessage)
  {
    ProcessResult result = ExecuteCommand("git", arguments.Split(' '));
    if (result.ExitCode != 0)
    {
      string msg = $"{errorMessage}: {result.StandardError}";
      ShowErrorMessageBox(msg);
      Environment.Exit(1);
    }

    return result.StandardOutput.Trim();
  }

  private static void AddToPath(string path)
  {
    string binPath = Path.Combine(path, "bin");
    string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    string profilePath;
    string pathUpdateCmd;

    if (OperatingSystem.IsWindows())
    {
      profilePath = Path.Combine(homeDir, "AppData", "Local", "Microsoft", "Windows", "PowerShell", "profile.ps1");
      pathUpdateCmd = $"$env:PATH += \";{binPath}\"";
    }
    else
    {
      profilePath = Path.Combine(homeDir, ".bashrc");
      pathUpdateCmd = $"export PATH=\"{binPath}:$PATH\"";
    }

    WriteProfile(profilePath, pathUpdateCmd);
    Log("Successfully added Cosmo to your PATH.");
  }

  private static ProcessResult ExecuteCommand(string command, params string[] arguments)
  {
    ProcessStartInfo startInfo = new ProcessStartInfo
    {
      FileName = command,
      Arguments = string.Join(" ", arguments),
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      UseShellExecute = false,
      CreateNoWindow = true
    };

    Process process = new Process
    {
      StartInfo = startInfo
    };

    StringBuilder output = new StringBuilder();
    StringBuilder error = new StringBuilder();

    process.OutputDataReceived += (s, e) => output.AppendLine(e.Data);
    process.ErrorDataReceived += (s, e) => error.AppendLine(e.Data);

    process.Start();
    process.BeginOutputReadLine();
    process.BeginErrorReadLine();
    process.WaitForExit();

    return new ProcessResult
    {
      ExitCode = process.ExitCode,
      StandardOutput = output.ToString(),
      StandardError = error.ToString()
    };
  }

  private static void WriteProfile(string path, string pathUpdateCmd)
  {
    try
    {
      using (StreamWriter file = File.AppendText(path))
        file.WriteLine(pathUpdateCmd);
    }
    catch (Exception e)
    {
      string msg = $"Failed to write to shell profile: {e.Message}";
      ShowErrorMessageBox(msg);
    }
  }

  private static void StepProgress()
  {
    progress += _step;
    if (_updateProgress == null)
      throw new Exception("Attempt to call StepProgress() while _updateProgress is null");
    else
      _updateProgress((int)progress);
  }

  private static void ShowErrorMessageBox(string message)
  {
    Dispatcher.UIThread.InvokeAsync(async () => {
      await MessageBoxManager
        .GetMessageBoxStandardWindow("Error", message)
        .Show();

      Environment.Exit(1);
    });
  }

  private static void Log(string msg)
  {
    Console.WriteLine(msg);
    if (_updateTitle != null)
      _updateTitle(msg);
  }
}

public class ProcessResult
{
  public int ExitCode { get; set; }
  public string StandardOutput { get; set; } = "";
  public string StandardError { get; set; } = "";
}