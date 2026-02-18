using System;
using System.Diagnostics;
using System.IO;

namespace GitHubManager
{
  internal static class AppPreferencesStorage
  {
    private static readonly string PreferencesFilePath = Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
      "GitHubManager",
      "preferences.config");

    private static readonly string ShowMessagesFilePath = Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
      "GitHubManager",
      "showmessages.config");
      
    private static readonly string LogDirectory = Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
      "GitHubManager",
      "Logs");
      
    private static readonly string LogFilePath = Path.Combine(
      LogDirectory,
      $"locallog_{DateTime.Now:yyyyMMdd}.txt");

    private const int DefaultItemsPerPage = 20;
    private const bool DefaultShowMessages = true;
    private static readonly string LocalPathFilePath = Path.Combine(
      AppDomain.CurrentDomain.BaseDirectory,
      "localpath.config");
    private static readonly string MaxReposFilePath = Path.Combine(
      AppDomain.CurrentDomain.BaseDirectory,
      "maxrepos.config");

    public static void SaveItemsPerPage(int itemsPerPage)
    {
      var directory = Path.GetDirectoryName(PreferencesFilePath);
      if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
      {
        Directory.CreateDirectory(directory);
      }

      File.WriteAllText(PreferencesFilePath, itemsPerPage.ToString());
    }

    public static int LoadItemsPerPage()
    {
      if (!File.Exists(PreferencesFilePath))
      {
        return DefaultItemsPerPage;
      }

      try
      {
        var content = File.ReadAllText(PreferencesFilePath).Trim();
        if (int.TryParse(content, out var itemsPerPage) && itemsPerPage > 0)
        {
          return itemsPerPage;
        }
      }
      catch
      {
        // En cas d'erreur, retourner la valeur par défaut
      }

      return DefaultItemsPerPage;
    }

    public static void SaveLocalReposPath(string localPath)
    {
      var directory = Path.GetDirectoryName(LocalPathFilePath);
      if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
      {
        Directory.CreateDirectory(directory);
      }

      File.WriteAllText(LocalPathFilePath, localPath ?? string.Empty);
    }

    public static void SaveShowMessages(bool showMessages)
    {
      var directory = Path.GetDirectoryName(ShowMessagesFilePath);
      if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
      {
        Directory.CreateDirectory(directory);
      }

      File.WriteAllText(ShowMessagesFilePath, showMessages.ToString());
    }

    public static bool LoadShowMessages()
    {
      if (!File.Exists(ShowMessagesFilePath))
      {
        return DefaultShowMessages;
      }

      try
      {
        var content = File.ReadAllText(ShowMessagesFilePath).Trim();
        if (bool.TryParse(content, out var showMessages))
        {
          return showMessages;
        }
        return DefaultShowMessages;
      }
      catch
      {
        return DefaultShowMessages;
      }
    }
    
    public static void LogToFile(string message)
    {
      try
      {
        // Créer le répertoire s'il n'existe pas
        if (!Directory.Exists(LogDirectory))
        {
          Directory.CreateDirectory(LogDirectory);
        }
        
        // Créer un nom de fichier avec la date du jour
        string logFileName = $"log-{DateTime.Now:yyyy-MM-dd}.txt";
        string logFilePath = Path.Combine(LogDirectory, logFileName);
        
        // Ajouter le message au fichier de log avec un timestamp
        File.AppendAllText(logFilePath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}");
      }
      catch (Exception ex)
      {
        // En cas d'erreur, écrire dans le journal des événements Windows
        try
        {
          using (EventLog eventLog = new EventLog("Application"))
          {
            eventLog.Source = "GitHubManager";
            eventLog.WriteEntry($"Erreur lors de l'écriture dans le fichier de log: {ex.Message}", 
                              EventLogEntryType.Error);
          }
        }
        catch
        {
          // Si tout échoue, on ne peut rien faire de plus
        }
      }
    }

    public static string LoadLocalReposPath()
    {
      if (!File.Exists(LocalPathFilePath))
      {
        return string.Empty;
      }

      try
      {
        return File.ReadAllText(LocalPathFilePath).Trim();
      }
      catch
      {
        return string.Empty;
      }
    }

    public static void SaveMaxRepos(object maxRepos)
    {
      var directory = Path.GetDirectoryName(MaxReposFilePath);
      if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
      {
        Directory.CreateDirectory(directory);
      }

      string valueToSave;
      if (maxRepos is string strValue)
      {
        valueToSave = strValue;
      }
      else if (maxRepos is int intValue)
      {
        valueToSave = intValue.ToString();
      }
      else
      {
        valueToSave = "Tous";
      }

      File.WriteAllText(MaxReposFilePath, valueToSave);
    }

    public static object LoadMaxRepos()
    {
      if (!File.Exists(MaxReposFilePath))
      {
        return "Tous";
      }

      try
      {
        var content = File.ReadAllText(MaxReposFilePath).Trim();
        
        if (content == "Tous")
        {
          return "Tous";
        }
        
        if (int.TryParse(content, out var intValue))
        {
          return intValue;
        }
      }
      catch
      {
        // En cas d'erreur, retourner la valeur par défaut
      }

      return "Tous";
    }
  }
}
