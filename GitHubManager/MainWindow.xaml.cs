using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace GitHubManager
{
  /// <summary>
  /// Logique d'interaction pour MainWindow.xaml
  /// </summary>
  public partial class MainWindow: Window
  {
    private readonly ObservableCollection<GitHubRepository> _repositories = new ObservableCollection<GitHubRepository>();
    private string _token;

    public MainWindow()
    {
      InitializeComponent();
      RepositoriesDataGrid.ItemsSource = _repositories;
    }

    private HttpClient CreateHttpClient()
    {
      var client = new HttpClient();
      client.DefaultRequestHeaders.UserAgent.ParseAdd("GitHubManagerApp/1.0");

      if (!string.IsNullOrWhiteSpace(_token))
      {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
      }

      return client;
    }

    private async void TestAuthButton_Click(object sender, RoutedEventArgs e)
    {
      AuthStatusTextBlock.Text = "Test de l'authentification en cours...";
      AuthStatusTextBlock.Foreground = Brushes.Black;
      TestAuthButton.IsEnabled = false;

      try
      {
        var userName = UserNameTextBox.Text?.Trim();
        _token = TokenPasswordBox.Password;

        if (string.IsNullOrWhiteSpace(_token))
        {
          AuthStatusTextBlock.Text = "Veuillez saisir un token personnel GitHub (PAT).";
          AuthStatusTextBlock.Foreground = Brushes.Red;
          return;
        }

        using (var client = CreateHttpClient())
        {
          var response = await client.GetAsync("https://api.github.com/user");

          if (response.IsSuccessStatusCode)
          {
            using (var stream = await response.Content.ReadAsStreamAsync())
            {
              var serializer = new DataContractJsonSerializer(typeof(GitHubUser));
              var user = serializer.ReadObject(stream) as GitHubUser;

              var login = user != null ? user.Login : "(inconnu)";
              AuthStatusTextBlock.Text = $"Authentification réussie. Connecté en tant que {login}.";
              AuthStatusTextBlock.Foreground = Brushes.Green;
            }
          }
          else
          {
            AuthStatusTextBlock.Text = $"Échec de l'authentification (code HTTP {(int)response.StatusCode}).";
            AuthStatusTextBlock.Foreground = Brushes.Red;
          }
        }
      }
      catch (Exception ex)
      {
        AuthStatusTextBlock.Text = $"Erreur lors du test : {ex.Message}";
        AuthStatusTextBlock.Foreground = Brushes.Red;
      }
      finally
      {
        TestAuthButton.IsEnabled = true;
      }
    }

    private async void LoadReposButton_Click(object sender, RoutedEventArgs e)
    {
      ReposStatusTextBlock.Text = "Chargement des dépôts...";
      LoadReposButton.IsEnabled = false;

      try
      {
        if (string.IsNullOrWhiteSpace(_token))
        {
          ReposStatusTextBlock.Text = "Veuillez d'abord valider l'authentification (onglet Authentification).";
          return;
        }

        using (var client = CreateHttpClient())
        {
          // Liste des dépôts de l'utilisateur authentifié
          var response = await client.GetAsync("https://api.github.com/user/repos?per_page=100");

          if (response.IsSuccessStatusCode)
          {
            using (var stream = await response.Content.ReadAsStreamAsync())
            {
              var serializer = new DataContractJsonSerializer(typeof(List<GitHubRepository>));
              var repos = serializer.ReadObject(stream) as List<GitHubRepository> ?? new List<GitHubRepository>();

              _repositories.Clear();
              foreach (var repo in repos)
              {
                _repositories.Add(repo);
              }

              ReposStatusTextBlock.Text = $"{_repositories.Count} dépôts chargés.";
            }
          }
          else
          {
            ReposStatusTextBlock.Text = $"Impossible de charger les dépôts (code HTTP {(int)response.StatusCode}).";
          }
        }
      }
      catch (Exception ex)
      {
        ReposStatusTextBlock.Text = $"Erreur lors du chargement : {ex.Message}";
      }
      finally
      {
        LoadReposButton.IsEnabled = true;
      }
    }
  }

  [DataContract]
  public class GitHubRepository
  {
    [DataMember(Name = "name")]
    public string Name { get; set; }

    [DataMember(Name = "description")]
    public string Description { get; set; }

    [DataMember(Name = "private")]
    public bool IsPrivate { get; set; }

    [DataMember(Name = "html_url")]
    public string HtmlUrl { get; set; }
  }

  [DataContract]
  internal class GitHubUser
  {
    [DataMember(Name = "login")]
    public string Login { get; set; }

    [DataMember(Name = "name")]
    public string Name { get; set; }
  }
}
