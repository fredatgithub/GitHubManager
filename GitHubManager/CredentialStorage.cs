using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace GitHubManager
{
  internal static class CredentialStorage
  {
    private static readonly string CredentialsFilePath = Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
      "GitHubManager",
      "credentials.bin");

    public static void Save(string userName, string token)
    {
      if (string.IsNullOrWhiteSpace(userName) && string.IsNullOrWhiteSpace(token))
      {
        if (File.Exists(CredentialsFilePath))
        {
          File.Delete(CredentialsFilePath);
        }

        return;
      }

      var directory = Path.GetDirectoryName(CredentialsFilePath);
      if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
      {
        Directory.CreateDirectory(directory);
      }

      var plainText = (userName ?? string.Empty) + "\0" + (token ?? string.Empty);
      var plainBytes = Encoding.UTF8.GetBytes(plainText);

      var protectedBytes = ProtectedData.Protect(
        plainBytes,
        null,
        DataProtectionScope.CurrentUser);

      File.WriteAllBytes(CredentialsFilePath, protectedBytes);
    }

    public static bool TryLoad(out string userName, out string token)
    {
      userName = null;
      token = null;

      if (!File.Exists(CredentialsFilePath))
      {
        return false;
      }

      try
      {
        var protectedBytes = File.ReadAllBytes(CredentialsFilePath);
        var plainBytes = ProtectedData.Unprotect(
          protectedBytes,
          null,
          DataProtectionScope.CurrentUser);

        var plainText = Encoding.UTF8.GetString(plainBytes);
        var parts = plainText.Split(new[] { '\0' }, 2);

        if (parts.Length == 2)
        {
          userName = parts[0];
          token = parts[1];
        }
        else
        {
          userName = plainText;
          token = string.Empty;
        }

        return true;
      }
      catch
      {
        userName = null;
        token = null;
        return false;
      }
    }
  }
}

