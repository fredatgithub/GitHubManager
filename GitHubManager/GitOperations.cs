using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using GitHubManager;
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
        AppPreferencesStorage.LogToFile($"[CloneRepositoryAsync] Début du clonage de {repoUrl} vers {Path.Combine(localPath, repoName)}");
        
        // Vérifier si Git est accessible
        try
        {
          var gitTest = new ProcessStartInfo
          {
            FileName = "git",
            Arguments = "--version",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
          };
          
          using (var gitTestProcess = Process.Start(gitTest))
          {
            gitTestProcess.WaitForExit();
            var gitVersion = gitTestProcess.StandardOutput.ReadToEnd();
            AppPreferencesStorage.LogToFile($"[CloneRepositoryAsync] Version Git: {gitVersion.Trim()}");
          }
        }
        catch (Exception gitEx)
        {
          AppPreferencesStorage.LogToFile($"[CloneRepositoryAsync] ERREUR: Git n'est pas accessible - {gitEx.Message}");
          return false;
        }
        
        if (!Directory.Exists(localPath))
        {
          AppPreferencesStorage.LogToFile($"[CloneRepositoryAsync] Création du répertoire: {localPath}");
          Directory.CreateDirectory(localPath);
        }

        string fullPath = Path.Combine(localPath, repoName);
        
        // Vérifier si le répertoire de destination existe déjà
        if (Directory.Exists(fullPath))
        {
          AppPreferencesStorage.LogToFile($"[CloneRepositoryAsync] ERREUR: Le répertoire de destination existe déjà: {fullPath}");
          return false;
        }

        string gitCommand = $"clone \"{repoUrl}\" \"{repoName}\"";
        AppPreferencesStorage.LogToFile($"[CloneRepositoryAsync] Commande Git: git {gitCommand}");
        AppPreferencesStorage.LogToFile($"[CloneRepositoryAsync] Répertoire de travail: {localPath}");
        
        var processInfo = new ProcessStartInfo
        {
          FileName = "git",
          Arguments = gitCommand,
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
            AppPreferencesStorage.LogToFile("[CloneRepositoryAsync] ERREUR: Impossible de démarrer le processus git clone");
            return false;
          }

          var output = new System.Text.StringBuilder();
          var error = new System.Text.StringBuilder();

          process.OutputDataReceived += (s, e) =>
          {
            if (!string.IsNullOrEmpty(e.Data))
            {
              output.AppendLine(e.Data);
              AppPreferencesStorage.LogToFile($"[Git clone] {e.Data}");
            }
          };

          process.ErrorDataReceived += (s, e) =>
          {
            if (!string.IsNullOrEmpty(e.Data))
            {
              error.AppendLine(e.Data);
              AppPreferencesStorage.LogToFile($"[Git clone ERROR] {e.Data}");
            }
          };

          process.BeginOutputReadLine();
          process.BeginErrorReadLine();

          AppPreferencesStorage.LogToFile("[CloneRepositoryAsync] Attente de la fin du processus git clone...");
          await Task.Run(() => process.WaitForExit());
          
          AppPreferencesStorage.LogToFile($"[CloneRepositoryAsync] Code de sortie de git clone: {process.ExitCode}");
          AppPreferencesStorage.LogToFile($"[CloneRepositoryAsync] Sortie complète: {output.ToString()}");
          AppPreferencesStorage.LogToFile($"[CloneRepositoryAsync] Erreur complète: {error.ToString()}");
          
          // Vérifier si le répertoire a été créé correctement
          if (Directory.Exists(fullPath))
          {
            var files = Directory.EnumerateFileSystemEntries(fullPath).Take(10).ToList();
            AppPreferencesStorage.LogToFile($"[CloneRepositoryAsync] Contenu du répertoire cloné ({files.Count} éléments):");
            foreach (var file in files)
            {
              var attr = File.GetAttributes(file);
              AppPreferencesStorage.LogToFile($"  - {Path.GetFileName(file)} ({(attr.HasFlag(FileAttributes.Directory) ? "Dossier" : "Fichier")})");
            }
          }
          else
          {
            AppPreferencesStorage.LogToFile($"[CloneRepositoryAsync] ERREUR: Le répertoire n'a pas été créé: {fullPath}");
          }
          
          return process.ExitCode == 0;
        }
      }
      catch (Exception ex)
      {
        AppPreferencesStorage.LogToFile($"[CloneRepositoryAsync] Exception: {ex.Message}");
        AppPreferencesStorage.LogToFile($"[CloneRepositoryAsync] StackTrace: {ex.StackTrace}");
        return false;
      }
    }

    public static async Task<bool> UpdateRepositoryAsync(string repoPath)
    {
      try
      {
        AppPreferencesStorage.LogToFile($"[UpdateRepositoryAsync] Début de la mise à jour du dépôt: {repoPath}");
        
        // Vérifier si Git est accessible
        try
        {
          var gitTest = new ProcessStartInfo
          {
            FileName = "git",
            Arguments = "--version",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
          };
          
          using (var gitTestProcess = Process.Start(gitTest))
          {
            gitTestProcess.WaitForExit();
            var gitVersion = gitTestProcess.StandardOutput.ReadToEnd();
            AppPreferencesStorage.LogToFile($"[UpdateRepositoryAsync] Version Git: {gitVersion.Trim()}");
          }
        }
        catch (Exception gitEx)
        {
          AppPreferencesStorage.LogToFile($"[UpdateRepositoryAsync] ERREUR: Git n'est pas accessible - {gitEx.Message}");
          return false;
        }
        
        // Vérifier si le répertoire existe
        if (!Directory.Exists(repoPath))
        {
          AppPreferencesStorage.LogToFile($"[UpdateRepositoryAsync] ERREUR: Le répertoire n'existe pas: {repoPath}");
          return false;
        }
        
        AppPreferencesStorage.LogToFile($"[UpdateRepositoryAsync] Répertoire trouvé, lancement de git pull");
        
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
            AppPreferencesStorage.LogToFile("[UpdateRepositoryAsync] ERREUR: Impossible de démarrer le processus git pull");
            return false;
          }

          var output = new System.Text.StringBuilder();
          var error = new System.Text.StringBuilder();

          process.OutputDataReceived += (s, e) =>
          {
            if (!string.IsNullOrEmpty(e.Data))
            {
              output.AppendLine(e.Data);
              AppPreferencesStorage.LogToFile($"[Git pull] {e.Data}");
            }
          };

          process.ErrorDataReceived += (s, e) =>
          {
            if (!string.IsNullOrEmpty(e.Data))
            {
              error.AppendLine(e.Data);
              AppPreferencesStorage.LogToFile($"[Git pull ERROR] {e.Data}");
            }
          };

          process.BeginOutputReadLine();
          process.BeginErrorReadLine();

          await Task.Run(() => process.WaitForExit());
          
          AppPreferencesStorage.LogToFile($"[UpdateRepositoryAsync] Code de sortie de git pull: {process.ExitCode}");
          AppPreferencesStorage.LogToFile($"[UpdateRepositoryAsync] Sortie complète: {output.ToString()}");
          AppPreferencesStorage.LogToFile($"[UpdateRepositoryAsync] Erreur complète: {error.ToString()}");
          
          if (process.ExitCode == 0)
          {
            AppPreferencesStorage.LogToFile("[UpdateRepositoryAsync] Mise à jour réussie");
            return true;
          }
          else
          {
            AppPreferencesStorage.LogToFile("[UpdateRepositoryAsync] Mise à jour échouée");
            return false;
          }
        }
      }
      catch (Exception ex)
      {
        AppPreferencesStorage.LogToFile($"[UpdateRepositoryAsync] Exception: {ex.Message}");
        AppPreferencesStorage.LogToFile($"[UpdateRepositoryAsync] StackTrace: {ex.StackTrace}");
        return false;
      }
    }

    public static (RepositoryLocalState State, string Path) CheckRepositoryState(string repoName, string localPath)
    {
      AppPreferencesStorage.LogToFile($"\n[CheckRepositoryState] Début de la vérification pour le dépôt: {repoName}");
      AppPreferencesStorage.LogToFile($"[CheckRepositoryState] Chemin local fourni: {localPath}");

      try
      {
        if (string.IsNullOrEmpty(localPath))
        {
          AppPreferencesStorage.LogToFile($"[CheckRepositoryState] ERREUR: Le chemin local est vide ou null pour le dépôt {repoName}");
          return (RepositoryLocalState.NotCloned, string.Empty);
        }

        // Vérifier si le chemin local existe
        if (!Directory.Exists(localPath))
        {
          AppPreferencesStorage.LogToFile($"[CheckRepositoryState] ERREUR: Le répertoire local n'existe pas: {localPath}");
          return (RepositoryLocalState.NotCloned, string.Empty);
        }

        var repoPath = Path.Combine(localPath, repoName);
        var fullPath = Path.GetFullPath(repoPath);
        AppPreferencesStorage.LogToFile($"[CheckRepositoryState] Chemin complet du dépôt: {fullPath}");
        AppPreferencesStorage.LogToFile($"[CheckRepositoryState] Le répertoire existe: {Directory.Exists(repoPath)}");

        if (!Directory.Exists(repoPath))
        {
          AppPreferencesStorage.LogToFile($"[CheckRepositoryState] Le dossier du dépôt n'existe pas: {repoPath}");
          AppPreferencesStorage.LogToFile($"[CheckRepositoryState] Répertoire parent existe: {Directory.Exists(Path.GetDirectoryName(repoPath))}");
          AppPreferencesStorage.LogToFile($"[CheckRepositoryState] Contenu du répertoire parent: {string.Join(", ", Directory.EnumerateFileSystemEntries(localPath).Select(Path.GetFileName))}");
          return (RepositoryLocalState.NotCloned, repoPath);
        }

        // Vérifier le contenu du dossier
        var files = Directory.EnumerateFileSystemEntries(repoPath).ToList();
        AppPreferencesStorage.LogToFile($"[CheckRepositoryState] Contenu du dossier ({files.Count} éléments):");
        foreach (var file in files.Take(20)) // Afficher les 20 premiers fichiers max
        {
          var attr = File.GetAttributes(file);
          AppPreferencesStorage.LogToFile($"  - {Path.GetFileName(file)} ({(attr.HasFlag(FileAttributes.Directory) ? "Dossier" : "Fichier")})");
        }

        if (files.Count > 20)
          AppPreferencesStorage.LogToFile($"  ... et {files.Count - 20} autres éléments");

        if (!IsGitRepository(repoPath))
        {
          AppPreferencesStorage.LogToFile($"[CheckRepositoryState] Le dossier n'est pas un dépôt Git valide: {repoPath}");
          AppPreferencesStorage.LogToFile($"[CheckRepositoryState] Dossier .git existe: {Directory.Exists(Path.Combine(repoPath, ".git"))}");
          if (Directory.Exists(Path.Combine(repoPath, ".git")))
          {
            AppPreferencesStorage.LogToFile($"[CheckRepositoryState] Contenu du dossier .git: {string.Join(", ", Directory.EnumerateFileSystemEntries(Path.Combine(repoPath, ".git")).Select(Path.GetFileName))}");
          }
          return (RepositoryLocalState.NotCloned, repoPath);
        }

        // Récupérer les informations distantes sans modifier le repo
        AppPreferencesStorage.LogToFile($"[CheckRepositoryState] Exécution de 'git fetch --dry-run' dans {repoPath}");
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
              AppPreferencesStorage.LogToFile($"[Git fetch] {e.Data}");
            }
          };

          fetchProcess.ErrorDataReceived += (s, e) =>
          {
            if (!string.IsNullOrEmpty(e.Data))
            {
              error.AppendLine(e.Data);
              AppPreferencesStorage.LogToFile($"[Git fetch ERROR] {e.Data}");
            }
          };

          AppPreferencesStorage.LogToFile("[CheckRepositoryState] Démarrage du processus git fetch...");
          if (!fetchProcess.Start())
          {
            AppPreferencesStorage.LogToFile("[CheckRepositoryState] Échec du démarrage du processus git fetch");
            return (RepositoryLocalState.NotCloned, repoPath);
          }

          fetchProcess.BeginOutputReadLine();
          fetchProcess.BeginErrorReadLine();

          // Attendre avec un timeout de 30 secondes maximum
          if (!fetchProcess.WaitForExit(30000))
          {
            AppPreferencesStorage.LogToFile("[CheckRepositoryState] Timeout de la commande git fetch après 30 secondes");
            try { fetchProcess.Kill(); } catch { }
            return (RepositoryLocalState.NeedsUpdate, repoPath);
          }

          var exitCode = fetchProcess.ExitCode;
          var fetchOutput = output.ToString() + error.ToString();

          AppPreferencesStorage.LogToFile($"[CheckRepositoryState] Code de sortie git fetch: {exitCode}");
          AppPreferencesStorage.LogToFile($"[CheckRepositoryState] Sortie complète de 'git fetch --dry-run':\n{fetchOutput}");

          if (exitCode != 0)
          {
            AppPreferencesStorage.LogToFile($"[CheckRepositoryState] Erreur lors de l'exécution de git fetch. Code: {exitCode}");
            return (RepositoryLocalState.NeedsUpdate, repoPath);
          }
          
          // Si fetch --dry-run indique "Already up to date" ou est vide, vérifier avec status
          if (string.IsNullOrWhiteSpace(fetchOutput) || 
              fetchOutput.Contains("Already up to date") ||
              fetchOutput.Contains("Tout est à jour") ||
              fetchOutput.Contains("[up to date]") ||
              fetchOutput.Contains("up to date"))
          {
            AppPreferencesStorage.LogToFile("[CheckRepositoryState] Aucun changement distant détecté, vérification de l'état local");
            
            // Vérifier l'état du dépôt local
            AppPreferencesStorage.LogToFile("[CheckRepositoryState] Vérification de l'état local avec 'git status -sb'");
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
                AppPreferencesStorage.LogToFile("[CheckRepositoryState] ERREUR: Impossible de démarrer git status");
                return (RepositoryLocalState.UpToDate, repoPath);
              }

              statusProcess.WaitForExit();
              var statusOutput = statusProcess.StandardOutput.ReadToEnd();
              var statusError = statusProcess.StandardError.ReadToEnd();

              AppPreferencesStorage.LogToFile($"[CheckRepositoryState] Sortie de 'git status -sb': {statusOutput}");
              AppPreferencesStorage.LogToFile($"[CheckRepositoryState] Erreur de 'git status -sb': {statusError}");
              AppPreferencesStorage.LogToFile($"[CheckRepositoryState] Code de sortie de git status: {statusProcess.ExitCode}");

              // Si le status montre "ahead" ou "behind", le repo n'est pas à jour
              if (statusOutput.Contains("ahead") || statusOutput.Contains("behind"))
              {
                AppPreferencesStorage.LogToFile("[CheckRepositoryState] Dépôt nécessite une mise à jour (ahead/behind détecté)");
                return (RepositoryLocalState.NeedsUpdate, repoPath);
              }

              AppPreferencesStorage.LogToFile("[CheckRepositoryState] Dépôt à jour");
              return (RepositoryLocalState.UpToDate, repoPath);
            }
          }
          else
          {
            // Il y a des changements à récupérer
            AppPreferencesStorage.LogToFile("[CheckRepositoryState] Changements distants détectés, mise à jour nécessaire");
            return (RepositoryLocalState.NeedsUpdate, repoPath);
          }
        }
      }
      catch (Exception ex)
      {
        AppPreferencesStorage.LogToFile($"[CheckRepositoryState] Exception: {ex.Message}");
        return (RepositoryLocalState.NotCloned, Path.Combine(localPath, repoName));
      }
    }
  }
}
