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
using LibGit2Sharp;
using System.Net;
using System.Linq;

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
    private static Tag? _latestTag;

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

        const string repoURL = "https://github.com/roblox-csharp/roblox-cs.git";
        const string remoteName = "origin";
        var clonePath = Path.Combine(path, ".");
        var gitFolder = Path.Combine(clonePath, ".git");

        Repository? repo = null;
        if (Directory.Exists(gitFolder))
        {
            try
            {
                repo = new Repository(gitFolder);
            }
            catch (Exception err)
            {
                ShowErrorMessageBox($"Failed to create repository: {err.Message}\n{string.Join('\n', err.StackTrace)}");
                return;
            }
        }
        StepProgress();

        Remote origin = null!;
        IEnumerable<string> refspecs = null!;
        Display("Pulling repository...");
        try
        {
            var directoryEntries = Directory.GetFileSystemEntries(clonePath);
            if (directoryEntries.Length == 0)
            {
                try
                {
                    Repository.Clone(repoURL, clonePath);
                }
                catch (Exception err)
                {
                    ShowErrorMessageBox($"Failed to clone the compiler repository: {err.Message}");
                    return;
                }
            }
            if (repo == null)
            {
                try
                {
                    repo = new Repository(gitFolder);
                }
                catch (Exception err)
                {
                    ShowErrorMessageBox($"Failed to create repository: {err.Message}\n{string.Join('\n', err.StackTrace)}");
                    return;
                }
            }
            origin = repo.Network.Remotes[remoteName];
            refspecs = origin.FetchRefSpecs.Select(refspec => refspec.ToString()).OfType<string>();
            GitPull(repo, origin, refspecs);
        }
        catch (Exception err)
        {
            ShowErrorMessageBox($"Failed to read the compiler repository directory (run as administrator?): {err.Message}");
            return;
        }

        StepProgress();
        Display("Fetching tags...");
        if (repo == null) return;
        try
        {
            repo.Network.Fetch(origin.Name, refspecs, new FetchOptions()
            {
                TagFetchMode = TagFetchMode.All
            });
        }
        catch (Exception err)
        {
            ShowErrorMessageBox($"Failed to fetch release tags from repository: {err.Message}");
        }
        StepProgress();

        Display("Fetching latest release...");
        var tags = repo.Tags;
        _latestTag = tags.OrderByDescending(tag => tag.FriendlyName).FirstOrDefault()!;
        if (_latestTag == null)
        {
            ShowErrorMessageBox($"Failed to get the latest release tag, tags found: {repo.Tags.Count()}");
            return;
        }
        StepProgress();

        Display("Checking out latest release...");
        try
        {
            var commit = repo.Commits.FirstOrDefault(commit => commit == (Commit)_latestTag.Target);
            Commands.Checkout(repo, commit);
        }
        catch (Exception err)
        {
            ShowErrorMessageBox($"Failed to checkout the latest release: {err.Message}");
        }
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
        Display($"Successfully installed roblox-cs ({_latestTag?.FriendlyName ?? "???"}).");
    }

    private static void GitPull(Repository repo, Remote remote, IEnumerable<string> refspecs)
    {
        try
        {
            repo.Network.Fetch(remote.Name, refspecs);
        }
        catch (Exception err)
        {
            ShowErrorMessageBox($"Failed to fetch origin/master from repository: {err.Message}");
        }
        try
        {
            var branchToMerge = repo.Branches[$"{remote.Name}/master"];
            var signature = new Signature("rbxcs-installer", "rbxcs-installer@roblox-cs.com", DateTimeOffset.Now);
            var mergeResult = repo.Merge(branchToMerge, signature, new MergeOptions
            {
                FileConflictStrategy = CheckoutFileConflictStrategy.Theirs
            });
        }
        catch (Exception err)
        {
            ShowErrorMessageBox($"Failed to merge: {err.Message}");
        }
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