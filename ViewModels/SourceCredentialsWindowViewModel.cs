using System;
using ReactiveUI;

namespace Installer.ViewModels;

public class SourceCredentialsWindowViewModel : ReactiveObject
{
    public SourceCredentialsWindowViewModel()
    {
        Console.WriteLine("Initialized NuGet source credentials input window.");
    }
 }