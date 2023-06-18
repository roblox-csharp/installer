using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Text;
using Avalonia.Threading;
using MessageBox.Avalonia;

namespace CosmoInstaller;

public static class Installation
{
  private static readonly float _step = (1f / 13f) * 100;
  private static float _progress = 0;
  private static Action<int>? _updateProgress;
  private static Action<string>? _updateTitle;
  private static Action? _markErrored;
  private static Action? _markFinished;
  private static bool _errored = false;
  private static bool _finished = false;

  public static void InstallCosmo(
    Action<int> updateProgress,
    Action<string> updateTitle,
    Action markErrored,
    Action markFinished,
    string path,
    string installerPath
  ) {
    _updateProgress = updateProgress;
    _updateTitle = updateTitle;
    _markErrored = markErrored;
    _markFinished = markFinished;

    if (OperatingSystem.IsWindows() && !IsAdmin())
      ShowErrorMessageBox($"Cannot install. You are not running with elevated privileges.\nRestart the app as an administrator and try again.");

    if (_errored) return;
    Log("Creating installation environment...");

    if (Directory.Exists(path))
      Log("Installation directory exists, skipping creation...");
    else
      try
      {
        Directory.CreateDirectory(path);
      }
      catch (Exception err)
      {
        ShowErrorMessageBox($"Failed to create directory (run as administrator?): {err.Message}");
      }

    Log("Changing environment directory...");
    StepProgress();
    try
    {
      Environment.CurrentDirectory = path;
    }
    catch (Exception err)
    {
      ShowErrorMessageBox($"Failed to change directory (run as administrator?): {err.Message}");
    }

    StepProgress();
    Log("Pulling repository...");
    string[] dirEntries;
    try
    {
      dirEntries = Directory.GetFileSystemEntries(".");
      if (dirEntries.Length != 0)
      {
        if (OperatingSystem.IsWindows())
          ExecuteCommand("Failed to remove shard.lock", "powershell", "-c \"rm -Force ./shard.lock\"");
        else
          ExecuteCommand("Failed to remove shard.lock", "rm", "-f shard.lock");

        ExecuteGitCommand("pull origin master --allow-unrelated-histories", "Failed to pull from the repository (is git installed?)");
      }
      else
        ExecuteGitCommand("clone https://github.com/cosmo-lang/cosmo.git .", "Failed to clone the repository (is git installed?)");
    }
    catch (Exception err)
    {
      ShowErrorMessageBox($"Failed to read the directory (run as administrator?): {err.Message}");
    }

    StepProgress();
    Log("Fetching tags...");
    ExecuteGitCommand("fetch --tags", "Failed to fetch release tags");
    StepProgress();

    Log("Fetching latest release...");
    string latestTag = ExecuteGitCommand("describe --tags --abbrev=0", "Failed to get the latest release tag");
    StepProgress();

    Log("Checking out latest release...");
    string result = ExecuteGitCommand($"checkout {latestTag}", "Failed to checkout the latest release");
    StepProgress();

    Log("Checking for Crystal installation...");
    ProcessResult? crystalCheckOutput = null;
    try
    {
      crystalCheckOutput = ExecuteCommand(null, "crystal", "-v");
    }
    catch (Win32Exception) // shut up exceptions saying not found
    {
    }

    if (crystalCheckOutput == null || crystalCheckOutput.ExitCode != 0)
    {
      Log("Installing Crystal...");
      if (OperatingSystem.IsWindows())
      {
        Log("Checking for Scoop installation...");
        ExecuteCommand("Failed to install Crystal: \nScoop is not installed, but is required to install Crystal for Windows. \nIf you don't want to use Scoop, please manually install Crystal.", "powershell.exe", "-c \"scoop\"");

        Log("Found Scoop!");
        Log("Adding Crystal bucket...");
        ExecuteCommand("Failed to add Crystal bucket", "scoop", "bucket add crystal-preview https://github.com/neatorobito/scoop-crystal");
        StepProgress();

        ProcessResult crtCheckOutput = ExecuteCommand("Failed to execute 'where'", "where", "cl.exe");
        if (crtCheckOutput.ExitCode != 0)
        {
          Log("Installing C++ build tools...");
          ExecuteCommand("Failed to install C++ build tools", "scoop", "install vs_2022_cpp_build_tools");
        }

        StepProgress();
        Log("Installing Crystal bucket...");
        ExecuteCommand("Failed to install Crystal bucket", "scoop", "install crystal");
        StepProgress();
      }
      else if (OperatingSystem.IsLinux())
      {
        ExecuteCommand("Failed to install Crystal: \nSnapcraft is not installed, but is required to install Crystal for Linux. \nIf you don't want to use Snapcraft, please manually install Crystal.", "snap");
        StepProgress();

        string scriptPath = Path.Combine(installerPath, "snapd_setup.sh");
        ExecuteCommand("Failed to chmod Snapcraft setup script", "chmod", "+x", scriptPath);
        StepProgress();
        ExecuteCommand("Failed to setup", "pkexec", "--disable-internal-agent",  scriptPath);
        StepProgress();
      }
      else if (OperatingSystem.IsMacOS())
      {
        ExecuteCommand("Failed to install Crystal", "brew", "install crystal");
        StepProgress();
        StepProgress();
        StepProgress();
      }

      Log("Crystal has been successfully installed.\nPlease restart the installer and try again.\nYou may need to restart your shell, or even\nyour machine.");
      _finished = true;
      _markFinished!();
      return;
    }
    else
    {
      Log("Crystal is already installed, continuing...");
      if (OperatingSystem.IsWindows())
      {
        Log("Updating Scoop + Crystal...");
        ExecuteCommand("Failed to update Scoop", "powershell.exe", "-c \"scoop update\"");
        ExecuteCommand("Failed to update Crystal bucket", "powershell.exe", "-c \"scoop update crystal\"");
        // maybe dont terminate installation if it fails to update
      }
    }

    if (_finished) return;
    StepProgress();

    Log("Building Cosmo...");
    Log("Installing dependencies...");
    ExecuteCommand("Failed to install Cosmo dependencies", "shards", "install");
    StepProgress();

    Log("Compiling... (this may take a while)");
    ExecuteCommand("Failed to build Cosmo", "shards", "build --release");
    StepProgress();

    Log("Successfully built Cosmo.");
    if (!OperatingSystem.IsWindows())
      AddToPath(path);

    StepProgress();
    Log("Successfully installed Cosmo.");
  }

  private static bool IsAdmin()
  {
    bool isAdmin;

    try
    {
      WindowsIdentity user = WindowsIdentity.GetCurrent();
      WindowsPrincipal principal = new WindowsPrincipal(user);
      isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
    catch (UnauthorizedAccessException)
    {
      isAdmin = false;
    }
    catch (Exception)
    {
      isAdmin = false;
    }

    return isAdmin;
  }

  private static void AddToPath(string path)
  {
    if (_errored) return;

    Log("Adding Cosmo to PATH...");
    string binPath = Path.Combine(path, "bin");
    string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    string profilePath = Path.Combine(homeDir, ".bashrc");
    string pathUpdateCmd = $"export PATH=\"{binPath}:$PATH\"";

    WriteProfile(profilePath, pathUpdateCmd);
    Log("Successfully added Cosmo to your PATH.");
  }

  private static string ExecuteGitCommand(string arguments, string errorMessage)
  {
    ProcessResult result = ExecuteCommand(errorMessage, "git", arguments.Split(' '));
    return result.StandardOutput.Trim();
  }

  private static ProcessResult ExecuteCommand(string? errorMessage, string command, params string[] arguments)
  {
    ProcessStartInfo startInfo = new ProcessStartInfo
    {
      FileName = command,
      Arguments = string.Join(' ', arguments),
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
    process.BeginErrorReadLine();
    process.BeginOutputReadLine();
    process.WaitForExit();

    var result = new ProcessResult
    {
      ExitCode = process.ExitCode,
      StandardOutput = output.ToString(),
      StandardError = error.ToString()
    };

    if (result.ExitCode != 0 && errorMessage != null)
      ShowErrorMessageBox($"{errorMessage}: {result.StandardError}");

    return result;
  }

  private static void WriteProfile(string path, string pathUpdateCmd)
  {
    if (_errored) return;
    try
    {
      using (StreamWriter file = File.AppendText(path))
        file.WriteLine(pathUpdateCmd);
    }
    catch (Exception err)
    {
      ShowErrorMessageBox($"Failed to write to shell profile: {err.Message}");
    }
  }

  private static void StepProgress()
  {
    if (_errored) return;
    _progress += _step;
    _updateProgress!((int)_progress);
  }

  private static void ShowErrorMessageBox(string message)
  {
    if (_errored) return;

    _errored = true;
    _markErrored!();
    _updateTitle!("Error!");

    Dispatcher.UIThread.InvokeAsync(() =>
    {
      MessageBoxManager
        .GetMessageBoxStandardWindow("Error", message)
        .Show()
        .ContinueWith(_ => Environment.Exit(1));
    });
  }

  private static void Log(string msg)
  {
    if (_errored) return;
    _updateTitle!(msg);
    Console.WriteLine(msg);
  }
}

public class ProcessResult
{
  public int ExitCode { get; set; }
  public string StandardOutput { get; set; } = "";
  public string StandardError { get; set; } = "";
}