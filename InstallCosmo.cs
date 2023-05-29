using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Avalonia.Threading;
using MessageBox.Avalonia;

namespace CosmoInstaller;

public static class Installation
{
  private static readonly float _step = (1f / 10f) * 100;
  private static float progress = 0;
  private static Action<int>? _updateProgress;
  private static Action<string>? _updateTitle;
  private static Action? _markErrored;
  private static bool _errored = false;

  public static void InstallCosmo(
    Action<int> updateProgress,
    Action<string> updateTitle,
    Action markErrored,
    string path
  ) {
    _updateProgress = updateProgress;
    _updateTitle = updateTitle;
    _markErrored = markErrored;

    Log("Creating installation environment...");
    if (Directory.Exists(path))
      Log("Installation directory exists, skipping creation...");
    else
    {
      try
      {
        Directory.CreateDirectory(path);
      }
      catch (Exception err)
      {
        ShowErrorMessageBox($"Failed to create directory (run as administrator?): {err.Message}");
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
      ShowErrorMessageBox($"Failed to change directory (run as administrator?): {err.Message}");
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
      ShowErrorMessageBox($"Failed to read the directory (run as administrator?): {err.Message}");
      return;
    }

    if (dirEntries.Length != 0)
      ExecuteGitCommand("pull origin master --allow-unrelated-histories", "Failed to pull from the repository (is git installed?)");
    else
      ExecuteGitCommand("clone https://github.com/cosmo-lang/cosmo.git .", "Failed to clone the repository (is git installed?)");

    StepProgress();
    Log("Fetching tags...");
    ExecuteGitCommand("fetch --tags", "Failed to fetch release tags");
    StepProgress();

    Log("Fetching latest release...");
    string latestTag = ExecuteGitCommand("describe --tags --abbrev=0", "Failed to get the latest release tag");
    StepProgress();

    Log("Checking out latest release...");
    ExecuteGitCommand($"checkout {latestTag}", "Failed to checkout the latest release");
    StepProgress();

    ProcessResult crystalCheckOutput = ExecuteCommand("crystal", "-v");
    if (crystalCheckOutput.ExitCode != 0)
    {
      Log("Installing Crystal...");
      if (OperatingSystem.IsWindows())
      {
        Log("Installing Scoop...");
        ExecuteCommand("irm", "get.scoop.sh | iex");
        ExecuteCommand("scoop", "bucket add crystal-preview https://github.com/neatorobito/scoop-crystal");
        Log("Installing C++ build tools...");
        ExecuteCommand("scoop", "install vs_2022_cpp_build_tools");
        ExecuteCommand("scoop", "install crystal");
        Log("Successfully installed Crystal via Scoop!...");
      }
      else if (OperatingSystem.IsLinux())
        ExecuteCommand("curl", "-sSL https://dist.crystal-lang.org/rpm/setup.sh");
      else if (OperatingSystem.IsMacOS())
        ExecuteCommand("brew", "install crystal");

      Log("Successfully installed Crystal.");
    }
    else
    {
      Log("Crystal is already installed, continuing...");
      if (OperatingSystem.IsWindows())
      {
        Log("Updating Scoop + Crystal...");
        ExecuteCommand("scoop", "update");
        ExecuteCommand("scoop", "update crystal");
      }
    }

    StepProgress();

    Log("Building Cosmo...");
    Log("Installing dependencies...");
    ExecuteCommand("shards", "install");
    StepProgress();

    Log("Compiling...");
    ExecuteCommand("shards", $"build --release");
    StepProgress();

    Log("Successfully built Cosmo.");
    Log("Adding Cosmo to PATH...");
    if (!OperatingSystem.IsWindows())
      AddToPath(path);

    StepProgress();
    Log("Successfully installed Cosmo.");
  }

  private static string ExecuteGitCommand(string arguments, string errorMessage)
  {
    ProcessResult result = ExecuteCommand("git", arguments.Split(' '));
    if (result.ExitCode != 0)
      ShowErrorMessageBox($"{errorMessage}: {result.StandardError}");

    return result.StandardOutput.Trim();
  }

  private static void AddToPath(string path)
  {
    string binPath = Path.Combine(path, "bin");
    string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    string profilePath = Path.Combine(homeDir, ".bashrc");
    string pathUpdateCmd = $"export PATH=\"{binPath}:$PATH\"";
    WriteProfile(profilePath, pathUpdateCmd);
    Log("Successfully added Cosmo to your PATH.");
  }

  private static ProcessResult ExecuteCommand(string command, params string[] arguments)
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
    process.BeginOutputReadLine();
    process.BeginErrorReadLine();
    process.WaitForExit();

    var result = new ProcessResult
    {
      ExitCode = process.ExitCode,
      StandardOutput = output.ToString(),
      StandardError = error.ToString()
    };

    Console.WriteLine(result.ToString());
    if (result.ExitCode != 0)
      ShowErrorMessageBox($"Error executing '{command} {string.Join(' ', arguments)}': {result.StandardError}");

    return result;
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
      ShowErrorMessageBox($"Failed to write to shell profile: {e.Message}");
    }
  }

  private static void StepProgress()
  {
    progress += _step;
    _updateProgress!((int)progress);
  }

  private static void ShowErrorMessageBox(string message)
  {
    if (_errored) return;

    _errored = true;
    _markErrored!();
    Dispatcher.UIThread.InvokeAsync(async () => {
      await MessageBoxManager
        .GetMessageBoxStandardWindow("Error", message)
        .Show();

      Environment.Exit(1);
    });
  }

  private static void Log(string msg)
  {
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