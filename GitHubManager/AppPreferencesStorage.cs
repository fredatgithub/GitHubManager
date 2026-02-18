using System;
using System.IO;

namespace GitHubManager
{
  internal static class AppPreferencesStorage
  {
    private static readonly string PreferencesFilePath = Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
      "GitHubManager",
      "preferences.config");

    private const int DefaultItemsPerPage = 20;
    private static readonly string LocalPathFilePath = Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
      "GitHubManager",
      "localpath.config");
    private static readonly string MaxReposFilePath = Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
      "GitHubManager",
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
