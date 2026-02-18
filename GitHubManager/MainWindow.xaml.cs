using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using WinForms = System.Windows.Forms;

namespace GitHubManager
{
  /// <summary>
  /// Logique d'interaction pour MainWindow.xaml
  /// </summary>
  public partial class MainWindow : Window
  {
    private readonly ObservableCollection<GitHubRepository> _allRepositories = new ObservableCollection<GitHubRepository>();
    private readonly ObservableCollection<GitHubRepository> _repositories = new ObservableCollection<GitHubRepository>();
    private string _token;
    private int _itemsPerPage = 20;
    private int _currentPage = 1;
    private int _totalPages = 1;
    private bool _hasStoredCredentials = false;

    public MainWindow()
    {
      InitializeComponent();
      RepositoriesDataGrid.ItemsSource = _repositories;
      LoadStoredCredentials();
      LoadStoredPreferences();

      Loaded += MainWindow_Loaded;
      Closing += MainWindow_Closing;
      
      // Initialiser l'UI de pagination après le chargement des composants
      Loaded += (s, e) =>
      {
        InitializeItemsPerPageComboBox();
        UpdatePaginationUI();
      };
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
      WindowSettingsStorage.Load(this);
      
      // Tester automatiquement l'authentification si les identifiants sont présents
      if (_hasStoredCredentials)
      {
        await TestAuthenticationAsync(silent: false);
      }
    }

    private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
      WindowSettingsStorage.Save(this);
      SaveStoredPreferences();
    }

    private void LoadStoredCredentials()
    {
      if (CredentialStorage.TryLoad(out var storedUserName, out var storedToken))
      {
        if (!string.IsNullOrWhiteSpace(storedUserName))
        {
          UserNameTextBox.Text = storedUserName;
        }

        if (!string.IsNullOrWhiteSpace(storedToken))
        {
          TokenPasswordBox.Password = storedToken;
          _token = storedToken;
        }

        // Marquer qu'on a des identifiants sauvegardés pour tester au chargement
        _hasStoredCredentials = !string.IsNullOrWhiteSpace(storedUserName) && !string.IsNullOrWhiteSpace(storedToken);
      }
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
      TestAuthButton.IsEnabled = false;
      await TestAuthenticationAsync(silent: false);
      TestAuthButton.IsEnabled = true;
    }

    private async Task TestAuthenticationAsync(bool silent = false)
    {
      if (!silent)
      {
        AuthStatusTextBlock.Text = "Test de l'authentification en cours...";
        AuthStatusTextBlock.Foreground = Brushes.Black;
      }

      try
      {
        var userName = UserNameTextBox.Text?.Trim();
        _token = TokenPasswordBox.Password;

        if (string.IsNullOrWhiteSpace(_token))
        {
          if (!silent)
          {
            AuthStatusTextBlock.Text = "Veuillez saisir un token personnel GitHub (PAT).";
            AuthStatusTextBlock.Foreground = Brushes.Red;
          }
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

              CredentialStorage.Save(userName, _token);
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
          // Charger tous les dépôts en paginant
          var allRepos = await LoadAllRepositoriesAsync(client);
          
          _allRepositories.Clear();
          foreach (var repo in allRepos)
          {
            _allRepositories.Add(repo);
          }

          _currentPage = 1;
          UpdatePagination();
          ReposStatusTextBlock.Text = $"{_allRepositories.Count} dépôts chargés.";
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

    private void UpdatePagination()
    {
      _totalPages = Math.Max(1, (int)Math.Ceiling((double)_allRepositories.Count / _itemsPerPage));
      _currentPage = Math.Max(1, Math.Min(_currentPage, _totalPages));

      var startIndex = (_currentPage - 1) * _itemsPerPage;
      var endIndex = Math.Min(startIndex + _itemsPerPage, _allRepositories.Count);

      _repositories.Clear();
      for (int i = startIndex; i < endIndex; i++)
      {
        _repositories.Add(_allRepositories[i]);
      }

      UpdatePaginationUI();
    }

    private void UpdatePaginationUI()
    {
      if (PaginationInfoTextBlock != null)
      {
        PaginationInfoTextBlock.Text = $"Page {_currentPage} sur {_totalPages} ({_allRepositories.Count} dépôts)";
      }

      if (FirstPageButton != null)
      {
        FirstPageButton.IsEnabled = _currentPage > 1;
      }

      if (PreviousPageButton != null)
      {
        PreviousPageButton.IsEnabled = _currentPage > 1;
      }

      if (NextPageButton != null)
      {
        NextPageButton.IsEnabled = _currentPage < _totalPages;
      }

      if (LastPageButton != null)
      {
        LastPageButton.IsEnabled = _currentPage < _totalPages;
      }
    }

    private void FirstPageButton_Click(object sender, RoutedEventArgs e)
    {
      _currentPage = 1;
      UpdatePagination();
    }

    private void PreviousPageButton_Click(object sender, RoutedEventArgs e)
    {
      if (_currentPage > 1)
      {
        _currentPage--;
        UpdatePagination();
      }
    }

    private void NextPageButton_Click(object sender, RoutedEventArgs e)
    {
      if (_currentPage < _totalPages)
      {
        _currentPage++;
        UpdatePagination();
      }
    }

    private void LastPageButton_Click(object sender, RoutedEventArgs e)
    {
      _currentPage = _totalPages;
      UpdatePagination();
    }

    private void LoadStoredPreferences()
    {
      _itemsPerPage = AppPreferencesStorage.LoadItemsPerPage();
      
      var localPath = AppPreferencesStorage.LoadLocalReposPath();
      if (!string.IsNullOrWhiteSpace(localPath) && LocalReposPathTextBox != null)
      {
        LocalReposPathTextBox.Text = localPath;
      }
    }

    private void SaveStoredPreferences()
    {
      AppPreferencesStorage.SaveItemsPerPage(_itemsPerPage);
      
      if (LocalReposPathTextBox != null)
      {
        AppPreferencesStorage.SaveLocalReposPath(LocalReposPathTextBox.Text);
      }
    }

    private void InitializeItemsPerPageComboBox()
    {
      if (ItemsPerPageComboBox != null)
      {
        ItemsPerPageComboBox.ItemsSource = new[] { 10, 20, 50, 100 };
        ItemsPerPageComboBox.SelectedItem = _itemsPerPage;
      }
    }

    private void ItemsPerPageComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
      if (ItemsPerPageComboBox != null && ItemsPerPageComboBox.SelectedItem is int selectedValue)
      {
        _itemsPerPage = selectedValue;
        _currentPage = 1; // Retourner à la première page lors du changement
        UpdatePagination();
        SaveStoredPreferences(); // Sauvegarder immédiatement lors du changement
      }
    }

    private async Task<List<GitHubRepository>> LoadAllRepositoriesAsync(HttpClient client)
    {
      var allRepos = new List<GitHubRepository>();
      var serializer = new DataContractJsonSerializer(typeof(List<GitHubRepository>));
      int page = 1;
      const int perPage = 100;
      bool hasMorePages = true;

      while (hasMorePages)
      {
        ReposStatusTextBlock.Text = $"Chargement des dépôts... (page {page}, {allRepos.Count} déjà chargés)";

        var url = $"https://api.github.com/user/repos?per_page={perPage}&page={page}";
        var response = await client.GetAsync(url);

        if (response.IsSuccessStatusCode)
        {
          using (var stream = await response.Content.ReadAsStreamAsync())
          {
            var repos = serializer.ReadObject(stream) as List<GitHubRepository> ?? new List<GitHubRepository>();

            if (repos.Count == 0)
            {
              hasMorePages = false;
            }
            else
            {
              allRepos.AddRange(repos);
              
              // Vérifier s'il y a une page suivante via l'en-tête Link
              if (response.Headers.Contains("Link"))
              {
                var linkHeader = response.Headers.GetValues("Link").FirstOrDefault();
                hasMorePages = HasNextPage(linkHeader);
              }
              else
              {
                // Si pas d'en-tête Link, continuer si on a reçu exactement perPage éléments
                hasMorePages = repos.Count == perPage;
              }

              page++;
            }
          }
        }
        else
        {
          throw new HttpRequestException($"Erreur HTTP {(int)response.StatusCode} lors du chargement de la page {page}.");
        }
      }

      return allRepos;
    }

    private bool HasNextPage(string linkHeader)
    {
      if (string.IsNullOrWhiteSpace(linkHeader))
      {
        return false;
      }

      // L'en-tête Link de GitHub a le format : <url>; rel="next", <url>; rel="last", etc.
      // On cherche la présence de rel="next"
      return linkHeader.Contains("rel=\"next\"");
    }

    private async void CloneOrUpdateButton_Click(object sender, RoutedEventArgs e)
    {
      if (sender is System.Windows.Controls.Button button && button.Tag is GitHubRepository repo)
      {
        var localPath = LocalReposPathTextBox?.Text?.Trim();
        
        if (string.IsNullOrWhiteSpace(localPath))
        {
          MessageBox.Show(
            "Veuillez d'abord spécifier le chemin local des dépôts dans l'onglet Authentification.",
            "Chemin manquant",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
          return;
        }

        button.IsEnabled = false;
        button.Content = "En cours...";

        try
        {
          var success = await GitOperations.CloneOrUpdateRepositoryAsync(
            repo.HtmlUrl,
            localPath,
            repo.Name);

          if (success)
          {
            MessageBox.Show(
              $"Le dépôt '{repo.Name}' a été cloné/mis à jour avec succès.",
              "Succès",
              MessageBoxButton.OK,
              MessageBoxImage.Information);
          }
          else
          {
            MessageBox.Show(
              $"Erreur lors du clonage/mise à jour du dépôt '{repo.Name}'. Vérifiez que Git est installé et que le chemin est valide.",
              "Erreur",
              MessageBoxButton.OK,
              MessageBoxImage.Error);
          }
        }
        catch (Exception ex)
        {
          MessageBox.Show(
            $"Erreur : {ex.Message}",
            "Erreur",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        }
        finally
        {
          button.IsEnabled = true;
          button.Content = "Cloner/Mettre à jour";
        }
      }
    }

    private void BrowseFolderButton_Click(object sender, RoutedEventArgs e)
    {
      using (var dialog = new WinForms.FolderBrowserDialog())
      {
        dialog.Description = "Sélectionnez le dossier où les dépôts seront clonés";
        dialog.ShowNewFolderButton = true;

        // Si un chemin est déjà saisi, l'utiliser comme point de départ
        if (!string.IsNullOrWhiteSpace(LocalReposPathTextBox?.Text))
        {
          try
          {
            dialog.SelectedPath = LocalReposPathTextBox.Text;
          }
          catch
          {
            // Ignorer si le chemin n'est pas valide
          }
        }

        if (dialog.ShowDialog() == WinForms.DialogResult.OK)
        {
          LocalReposPathTextBox.Text = dialog.SelectedPath;
        }
      }
    }
  }
}
