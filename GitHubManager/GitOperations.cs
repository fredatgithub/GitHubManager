using System.Diagnostics;
using System.IO;
using System.Linq;
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
      Debug.WriteLine($"\n[CheckRepositoryState] Début de la vérification pour le dépôt: {repoName}");
      Debug.WriteLine($"[CheckRepositoryState] Chemin local fourni: {localPath}");

      try
      {
        if (string.IsNullOrEmpty(localPath))
        {
          Debug.WriteLine($"[CheckRepositoryState] ERREUR: Le chemin local est vide ou null pour le dépôt {repoName}");
          return (RepositoryLocalState.NotCloned, string.Empty);
        }

        // Vérifier si le chemin local existe
        if (!Directory.Exists(localPath))
        {
          Debug.WriteLine($"[CheckRepositoryState] ERREUR: Le répertoire local n'existe pas: {localPath}");
          return (RepositoryLocalState.NotCloned, string.Empty);
        }

        var repoPath = Path.Combine(localPath, repoName);
        var fullPath = Path.GetFullPath(repoPath);
        Debug.WriteLine($"[CheckRepositoryState] Chemin complet du dépôt: {fullPath}");
        Debug.WriteLine($"[CheckRepositoryState] Le répertoire existe: {Directory.Exists(repoPath)}");

        if (!Directory.Exists(repoPath))
        {
          Debug.WriteLine($"[CheckRepositoryState] Le dossier du dépôt n'existe pas: {repoPath}");
          Debug.WriteLine($"[CheckRepositoryState] Répertoire parent existe: {Directory.Exists(Path.GetDirectoryName(repoPath))}");
          Debug.WriteLine($"[CheckRepositoryState] Contenu du répertoire parent: {string.Join(", ", Directory.EnumerateFileSystemEntries(localPath).Select(Path.GetFileName))}");
          return (RepositoryLocalState.NotCloned, repoPath);
        }

        // Vérifier le contenu du dossier
        var files = Directory.EnumerateFileSystemEntries(repoPath).ToList();
        Debug.WriteLine($"[CheckRepositoryState] Contenu du dossier ({files.Count} éléments):");
        foreach (var file in files.Take(20)) // Afficher les 20 premiers fichiers max
        {
          var attr = File.GetAttributes(file);
          Debug.WriteLine($"  - {Path.GetFileName(file)} ({(attr.HasFlag(FileAttributes.Directory) ? "Dossier" : "Fichier")})");
        }
        if (files.Count > 20)
          Debug.WriteLine($"  ... et {files.Count - 20} autres éléments");

        if (!IsGitRepository(repoPath))
        {
          Debug.WriteLine($"[CheckRepositoryState] Le dossier n'est pas un dépôt Git valide: {repoPath}");
          Debug.WriteLine($"[CheckRepositoryState] Dossier .git existe: {Directory.Exists(Path.Combine(repoPath, ".git"))}");
          if (Directory.Exists(Path.Combine(repoPath, ".git")))
          {
            Debug.WriteLine($"[CheckRepositoryState] Contenu du dossier .git: {string.Join(", ", Directory.EnumerateFileSystemEntries(Path.Combine(repoPath, ".git")).Select(Path.GetFileName))}");
          }
          return (RepositoryLocalState.NotCloned, repoPath);
        }

        // Récupérer les informations distantes sans modifier le repo
        Debug.WriteLine($"[CheckRepositoryState] Exécution de 'git fetch --dry-run' dans {repoPath}");
        var fetchInfo = new ProcessStartInfo
        {
          FileName = "git",
          Arguments = "fetch --dry-run --verbose",  // Ajout de --verbose pour plus d'informations
          WorkingDirectory = repoPath,
          UseShellExecute = false,
          RedirectStandardOutput = true,
          RedirectStandardError = true,
          CreateNoWindow = true
        };

        using (var fetchProcess = new Process { StartInfo = fetchInfo })
        {
          var output = new System.Text.StringBuilder();
          var error = new System.Text.StringBuilder();

          fetchProcess.OutputDataReceived += (s, e) =>
          {
            if (!string.IsNullOrEmpty(e.Data))
            {
              output.AppendLine(e.Data);
              Debug.WriteLine($"[Git fetch] {e.Data}");
            }
          };

          fetchProcess.ErrorDataReceived += (s, e) =>
          {
            if (!string.IsNullOrEmpty(e.Data))
            {
              error.AppendLine(e.Data);
              Debug.WriteLine($"[Git fetch ERROR] {e.Data}");
            }
          };

          Debug.WriteLine("[CheckRepositoryState] Démarrage du processus git fetch...");
          if (!fetchProcess.Start())
          {
            Debug.WriteLine("[CheckRepositoryState] Échec du démarrage du processus git fetch");
            return (RepositoryLocalState.NotCloned, repoPath);
          }

          fetchProcess.BeginOutputReadLine();
          fetchProcess.BeginErrorReadLine();

          // Attendre avec un timeout de 30 secondes maximum
          if (!fetchProcess.WaitForExit(30000))
          {
            Debug.WriteLine("[CheckRepositoryState] Timeout de la commande git fetch après 30 secondes");
            try { fetchProcess.Kill(); } catch { }
            return (RepositoryLocalState.NeedsUpdate, repoPath);
          }

          var exitCode = fetchProcess.ExitCode;
          var fetchOutput = output.ToString() + error.ToString();

          Debug.WriteLine($"[CheckRepositoryState] Code de sortie git fetch: {exitCode}");
          Debug.WriteLine($"[CheckRepositoryState] Sortie complète de 'git fetch --dry-run':\n{fetchOutput}");

          if (exitCode != 0)
          {
            Debug.WriteLine($"[CheckRepositoryState] Erreur lors de l'exécution de git fetch. Code: {exitCode}");
            return (RepositoryLocalState.NeedsUpdate, repoPath);
          }

          // Si fetch --dry-run indique "Already up to date" ou est vide, vérifier avec status
          if (string.IsNullOrWhiteSpace(fetchOutput) ||
              fetchOutput.Contains("Already up to date") ||
              fetchOutput.Contains("Tout est à jour"))
          {
            Debug.WriteLine("[CheckRepositoryState] Aucun changement distant détecté, vérification de l'état local");

            // Vérifier l'état du dépôt local
            Debug.WriteLine("[CheckRepositoryState] Vérification de l'état local avec 'git status -sb'");
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
