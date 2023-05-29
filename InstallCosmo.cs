using System;
using System.IO;

namespace CosmoInstaller;

public static class Installation
{
  private static readonly float _step = (1f / 9f) * 100;
  private static float progress = 0;
  private static Action<int>? _updateProgress;

  public static void InstallCosmo(Action<int> updateProgress, string path)
  {
    _updateProgress = updateProgress;
    path = Path.Combine(path, ".cosmo");

    // StepProgress();
  }

  private static void StepProgress()
  {
    progress += _step;
    if (_updateProgress == null)
      throw new Exception("Attempt to call StepProgress() while _updateProgress is null");
    else
      _updateProgress((int)progress);
  }
}