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

    public static async Task<bool> CloneRepositoryAsync(string repoUrl, string localPath, string repoName)
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

    public static async Task<bool> UpdateRepositoryAsync(string repoPath)
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

    public static (RepositoryLocalState State, string Path) CheckRepositoryState(string repoName, string localPath)
    {
      try
      {
        var repoPath = Path.Combine(localPath, repoName);
        Debug.WriteLine($"[CheckRepositoryState] Vérification de l'état pour {repoPath}");

        if (!Directory.Exists(repoPath))
        {
          Debug.WriteLine($"[CheckRepositoryState] Le dossier n'existe pas: {repoPath}");
          return (RepositoryLocalState.NotCloned, repoPath);
        }

        if (!IsGitRepository(repoPath))
        {
          Debug.WriteLine($"[CheckRepositoryState] Le dossier n'est pas un dépôt Git: {repoPath}");
          return (RepositoryLocalState.NotCloned, repoPath);
        }

        // Récupérer les informations distantes sans modifier le repo
        var fetchInfo = new ProcessStartInfo
        {
          FileName = "git",
          Arguments = "fetch --dry-run",
          WorkingDirectory = repoPath,
          UseShellExecute = false,
          RedirectStandardOutput = true,
          RedirectStandardError = true,
          CreateNoWindow = true
        };

        using (var fetchProcess = Process.Start(fetchInfo))
        {
          if (fetchProcess == null)
          {
            return (RepositoryLocalState.NotCloned, repoPath);
          }

          fetchProcess.WaitForExit();
          var fetchOutput = fetchProcess.StandardOutput.ReadToEnd() + fetchProcess.StandardError.ReadToEnd();

          // Si fetch --dry-run indique "Already up to date" ou est vide, vérifier avec status
          Debug.WriteLine($"[CheckRepositoryState] Sortie de 'git fetch --dry-run': {fetchOutput}");
          
          if (string.IsNullOrWhiteSpace(fetchOutput) || fetchOutput.Contains("Already up to date"))
          {
            Debug.WriteLine("[CheckRepositoryState] Aucun changement distant détecté, vérification de l'état local");
            // Vérifier s'il y a des commits en avance sur le remote
            var statusInfo = new ProcessStartInfo
            {
              FileName = "git",
              Arguments = "status -sb",
              WorkingDirectory = repoPath,
              UseShellExecute = false,
              RedirectStandardOutput = true,
              RedirectStandardError = true,
              CreateNoWindow = true
            };

            using (var statusProcess = Process.Start(statusInfo))
            {
              if (statusProcess == null)
              {
                return (RepositoryLocalState.UpToDate, repoPath);
              }

              statusProcess.WaitForExit();
              var statusOutput = statusProcess.StandardOutput.ReadToEnd();

              Debug.WriteLine($"[CheckRepositoryState] Sortie de 'git status -sb': {statusOutput}");
              
              // Si le status montre "ahead" ou "behind", le repo n'est pas à jour
              if (statusOutput.Contains("ahead") || statusOutput.Contains("behind"))
              {
                Debug.WriteLine("[CheckRepositoryState] Dépôt nécessite une mise à jour (ahead/behind)");
                return (RepositoryLocalState.NeedsUpdate, repoPath);
              }

              Debug.WriteLine("[CheckRepositoryState] Dépôt à jour");
              return (RepositoryLocalState.UpToDate, repoPath);
            }
          }
          else
          {
            // Il y a des changements à récupérer
            Debug.WriteLine("[CheckRepositoryState] Changements distants détectés, mise à jour nécessaire");
            return (RepositoryLocalState.NeedsUpdate, repoPath);
          }
        }
      }
      catch
      {
        return (RepositoryLocalState.NotCloned, Path.Combine(localPath, repoName));
      }
    }
  }
}
