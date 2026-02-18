using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace GitHubManager
{
  internal static class GitOperations
  {
    public static async Task<bool> CloneOrUpdateRepositoryAsync(string repoUrl, string localPath, string repoName)
    {
      try
      {
        var repoPath = Path.Combine(localPath, repoName);

        if (Directory.Exists(repoPath) && IsGitRepository(repoPath))
        {
          // Mise à jour du dépôt existant
          return await UpdateRepositoryAsync(repoPath);
        }
        else
        {
          // Clonage d'un nouveau dépôt
          return await CloneRepositoryAsync(repoUrl, localPath, repoName);
        }
      }
      catch
      {
        return false;
      }
    }

    private static bool IsGitRepository(string path)
    {
      return Directory.Exists(Path.Combine(path, ".git"));
    }

    private static async Task<bool> CloneRepositoryAsync(string repoUrl, string localPath, string repoName)
    {
      try
      {
        if (!Directory.Exists(localPath))
        {
          Directory.CreateDirectory(localPath);
        }

        var processInfo = new ProcessStartInfo
        {
          FileName = "git",
          Arguments = $"clone \"{repoUrl}\" \"{repoName}\"",
          WorkingDirectory = localPath,
          UseShellExecute = false,
          RedirectStandardOutput = true,
          RedirectStandardError = true,
          CreateNoWindow = true
        };

        using (var process = Process.Start(processInfo))
        {
          if (process == null)
          {
            return false;
          }

          await Task.Run(() => process.WaitForExit());
          return process.ExitCode == 0;
        }
      }
      catch
      {
        return false;
      }
    }

    private static async Task<bool> UpdateRepositoryAsync(string repoPath)
    {
      try
      {
        var processInfo = new ProcessStartInfo
        {
          FileName = "git",
          Arguments = "pull",
          WorkingDirectory = repoPath,
          UseShellExecute = false,
          RedirectStandardOutput = true,
          RedirectStandardError = true,
          CreateNoWindow = true
        };

        using (var process = Process.Start(processInfo))
        {
          if (process == null)
          {
            return false;
          }

          await Task.Run(() => process.WaitForExit());
          return process.ExitCode == 0;
        }
      }
      catch
      {
        return false;
      }
    }
  }
}
