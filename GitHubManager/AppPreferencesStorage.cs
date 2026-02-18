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
  }
}
