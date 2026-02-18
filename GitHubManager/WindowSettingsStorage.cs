using System;
using System.IO;
using System.Windows;

namespace GitHubManager
{
  internal static class WindowSettingsStorage
  {
    private static readonly string SettingsFilePath = Path.Combine(
      AppDomain.CurrentDomain.BaseDirectory,
      "window.config");

    public static void Save(Window window)
    {
      if (window == null)
      {
        return;
      }

      var directory = Path.GetDirectoryName(SettingsFilePath);
      if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
      {
        Directory.CreateDirectory(directory);
      }

      var state = window.WindowState;
      var width = window.Width;
      var height = window.Height;
      var left = window.Left;
      var top = window.Top;

      if (state == WindowState.Maximized)
      {
        // On sauvegarde la taille/position restaurée, pas la taille plein écran
        width = window.RestoreBounds.Width;
        height = window.RestoreBounds.Height;
        left = window.RestoreBounds.Left;
        top = window.RestoreBounds.Top;
      }

      var content = string.Format(
        System.Globalization.CultureInfo.InvariantCulture,
        "{0};{1};{2};{3};{4}",
        left,
        top,
        width,
        height,
        (int)state);

      File.WriteAllText(SettingsFilePath, content);
    }

    public static void Load(Window window)
    {
      if (window == null)
      {
        return;
      }

      if (!File.Exists(SettingsFilePath))
      {
        return;
      }

      try
      {
        var content = File.ReadAllText(SettingsFilePath);
        var parts = content.Split(';');
        if (parts.Length != 5)
        {
          return;
        }

        var culture = System.Globalization.CultureInfo.InvariantCulture;

        if (!double.TryParse(parts[0], System.Globalization.NumberStyles.Float, culture, out var left))
        {
          return;
        }

        if (!double.TryParse(parts[1], System.Globalization.NumberStyles.Float, culture, out var top))
        {
          return;
        }

        if (!double.TryParse(parts[2], System.Globalization.NumberStyles.Float, culture, out var width))
        {
          return;
        }

        if (!double.TryParse(parts[3], System.Globalization.NumberStyles.Float, culture, out var height))
        {
          return;
        }

        if (!int.TryParse(parts[4], out var stateValue))
        {
          return;
        }

        var state = (WindowState)stateValue;

        // Appliquer la taille/position avant d'éventuellement remettre en maximisé
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Left = left;
        window.Top = top;
        window.Width = width;
        window.Height = height;

        if (state == WindowState.Maximized)
        {
          window.WindowState = WindowState.Maximized;
        }
      }
      catch
      {
        // En cas de problème de parsing, on laisse simplement les valeurs par défaut.
      }
    }
  }
}

