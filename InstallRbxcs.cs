using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Threading.Tasks;
using MessageBox.Avalonia;
using Avalonia.Threading;
using Installer.Views;
using Installer.ViewModels;
using Avalonia.Controls;
using System.Collections.Generic;

namespace Installer;

public static class Installation
{
    private const string _sourceName = "rbxcs";
    private const string _sourceURL = "https://nuget.pkg.github.com/roblox-csharp/index.json";
    private const uint _steps = 9;

    private static readonly float _step = (1f / _steps) * 100;
    private static float _progress = 0;
    private static Action<int>? _updateProgress;
    private static Action<string>? _updateTitle;
    private static Action? _markErrored;
    private static bool _errored = false;
    private static string _path = "";
    private static string _latestTag = "";

    public static async Task InstallRbxcs(
      Action<int> updateProgress,
      Action<string> updateTitle,
      Action markErrored,
      string path
    )
    {
        _updateProgress = updateProgress;
        _updateTitle = updateTitle;
        _markErrored = markErrored;
        _path = path;

        if (_errored) return;
        Display("Creating installation environment...");

        if (Directory.Exists(path))
        {
            Display("Installation directory exists, skipping creation...");
        }
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

        Display("Changing environment directory...");
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
        Display("Pulling repository...");
        try
        {
            var directoryEntries = Directory.GetFileSystemEntries(".");
            if (directoryEntries.Length != 0)
            {
                ExecuteGitCommand("-v", "Failed to run 'git' command (is git installed?)");
                ExecuteGitCommand("pull origin master --allow-unrelated-histories", "Failed to pull from the compiler repository");
            }
            else
            {
                ExecuteGitCommand("clone https://github.com/roblox-csharp/roblox-cs.git .", "Failed to clone the compiler repository");
            }
        }
        catch (Exception err)
        {
            ShowErrorMessageBox($"Failed to read the compiler repository directory (run as administrator?): {err.Message}");
        }

        StepProgress();
        Display("Fetching tags...");
        ExecuteGitCommand("fetch --tags", "Failed to fetch release tags");
        StepProgress();

        Display("Fetching latest release...");
        _latestTag = ExecuteGitCommand("describe --tags --abbrev=0", "Failed to get the latest release tag");
        StepProgress();

        Display("Checking out latest release...");
        ExecuteGitCommand($"checkout {_latestTag}", "Failed to checkout the latest release");
        StepProgress();

        Display("Building roblox-cs...");
        var sourcesResult = ExecuteCommand(null, "dotnet", $"nuget list source");
        var sources = sourcesResult.StandardOutput;
        var sourceAlreadyAdded = sources.Contains(_sourceName) && sources.Contains(_sourceURL);
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            new SourceCredentialsWindow(sourceAlreadyAdded)
            {
                DataContext = new SourceCredentialsWindowViewModel()
            };
        });
    }

    public static void OnCredentialsAcquired(SourceCredentialsWindow credentialsWindow, bool sourceAlreadyAdded)
    {
        if (!sourceAlreadyAdded)
        {
            // add the source
            var usernameTextBox = credentialsWindow.FindControl<TextBox>("UsernameBox")!;
            var tokenTextBox = credentialsWindow.FindControl<TextBox>("TokenBox")!;
            Display("Adding NuGet source for RobloxCS packages...");
            ExecuteCommand(null, "dotnet", $"nuget add source \"{_sourceURL}\" -u {usernameTextBox.Text} -p {tokenTextBox.Text} -n {_sourceName}");
            StepProgress();
        }

        Display("Compiling...");
        ExecuteCommand("Failed to compile roblox-cs", "dotnet", "build -c Release");
        StepProgress();

        Display("Successfully compiled roblox-cs.");
        if (OperatingSystem.IsWindows())
        {
            UpdateEnvironmentPath(_path);
        }
        else
        {
            UpdateShellProfilePath(_path);
        }
        Display("Successfully added roblox-cs to your PATH.");

        StepProgress();
        Display($"Successfully installed roblox-cs ({_latestTag}).");
    }

    private static void UpdateEnvironmentPath(string path)
    {
        if (_errored) return;

        Display("Adding roblox-cs to PATH in system environment...");
        var binPath = Path.GetFullPath(Path.Combine(path, "bin"));
        var currentPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine);
        if (new List<string>(currentPath?.Split(';') ?? []).Contains(binPath))
        {
            Display("Already have bin folder in PATH variable! Skipping step...");
            return;
        }

        var updatedPath = $"{currentPath};{binPath}";
        try
        {
            Environment.SetEnvironmentVariable("PATH", updatedPath, EnvironmentVariableTarget.Machine);
        } catch (Exception exception)
        {
            ShowErrorMessageBox($"Failed to set PATH variable (run as administrator?): {exception.Message}");
        }
    }

    private static void UpdateShellProfilePath(string path)
    {
        if (_errored) return;

        Display("Adding roblox-cs to PATH in shell profile...");
        var binPath = Path.Combine(path, "bin");
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var profilePath = Path.Combine(homeDir, ".bashrc");
        var pathUpdateCmd = $"export PATH=\"{binPath}:$PATH\"";

        WriteProfile(profilePath, pathUpdateCmd);
    }

    private static string ExecuteGitCommand(string arguments, string errorMessage)
    {
        var result = ExecuteCommand(errorMessage, "git", arguments.Split(' '));
        return result.StandardOutput.Trim();
    }

    private static ProcessResult ExecuteCommand(string? errorMessage, string command, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = command,
            Arguments = string.Join(' ', arguments),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var process = new Process
        {
            StartInfo = startInfo
        };

        var output = new StringBuilder();
        var error = new StringBuilder();
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
        {
            ShowErrorMessageBox($"{errorMessage}: {(!string.IsNullOrEmpty(result.StandardError) ? result.StandardError : result.StandardOutput)}");
        }
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

    private static void Display(string msg)
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