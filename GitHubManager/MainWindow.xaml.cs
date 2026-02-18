using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
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

    // Propriété pour contrôler l'affichage des messages
    private bool ShowMessages => ShowMessagesCheckBox?.IsChecked ?? true;

    // Méthode utilitaire pour afficher des messages de manière conditionnelle
    private void ShowMessage(string message, string title, MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.None)
    {
      // Journaliser le message avec le niveau approprié
      string logMessage = $"{title}: {message}";
      if (icon == MessageBoxImage.Error)
      {
        LogError(logMessage);
      }
      else if (icon == MessageBoxImage.Warning)
      {
        LogWarning(logMessage);
      }
      else
      {
        LogInfo(logMessage);
      }

      // Afficher la boîte de message si activé
      if (ShowMessages)
      {
        Dispatcher.Invoke(() => MessageBox.Show(this, message, title, buttons, icon));
      }
    }

    // Méthodes de journalisation
    private void LogInfo(string message)
    {
      LogMessage($"[INFO] {message}");
    }

    private void LogWarning(string message)
    {
      LogMessage($"[WARNING] {message}");
    }

    private void LogError(string message)
    {
      LogMessage($"[ERROR] {message}");
    }

    private void LogMessage(string message)
    {
      string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
      string logMessage = $"[{timestamp}] {message}";

      if (LogTextBox != null)
      {
        Dispatcher.Invoke(() =>
        {
          LogTextBox.AppendText($"{logMessage}{Environment.NewLine}");
          LogTextBox.ScrollToEnd();
        });
      }

      // Écrire dans le fichier de log
      AppPreferencesStorage.LogToFile(message);
    }

    private bool _hasStoredCredentials = false;
    private CancellationTokenSource _loadCancellationTokenSource;
    private LoadingWindow _currentLoadingWindow;

    public MainWindow()
    {
      InitializeComponent();
      RepositoriesDataGrid.ItemsSource = _repositories;

      // Enregistrer le démarrage de l'application
      string startupMessage = $"=== Démarrage de l'application à {DateTime.Now:dd/MM/yyyy HH:mm:ss} ===";
      LogInfo(startupMessage);

      LoadStoredCredentials();
      LoadStoredPreferences();

      Loaded += MainWindow_Loaded;
      Closing += MainWindow_Closing;

      // Initialiser l'UI de pagination après le chargement des composants
      Loaded += (s, e) =>
      {
        InitializeItemsPerPageComboBox();
        InitializeMaxReposComboBox();
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
      try
      {
        // Sauvegarder les paramètres de la fenêtre
        WindowSettingsStorage.Save(this);

        // Sauvegarder les préférences
        SaveStoredPreferences();

        // Sauvegarder les logs dans un fichier
        if (LogTextBox != null)
        {
          string logContent = LogTextBox.Text;
          if (!string.IsNullOrEmpty(logContent))
          {
            string logFileName = $"applog-{DateTime.Now:yyyyMMdd-HHmmss}.txt";
            string logFilePath = Path.Combine(
              Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
              "GitHubManager",
              "Logs",
              logFileName);

            string logDir = Path.GetDirectoryName(logFilePath);
            if (!Directory.Exists(logDir))
            {
              Directory.CreateDirectory(logDir);
            }

            File.WriteAllText(logFilePath, logContent);
            LogInfo($"Journal de l'application sauvegardé dans: {logFilePath}");
          }
        }

        LogInfo("Application fermée");
      }
      catch (Exception exception)
      {
        LogError($"Erreur lors de la fermeture de l'application: {exception.Message}");

        // Essayer d'écrire l'erreur dans un fichier de secours
        try
        {
          string errorLogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            $"GitHubManager_Error_{DateTime.Now:yyyyMMdd-HHmmss}.txt");

          File.WriteAllText(errorLogPath, $"Erreur lors de la fermeture de l'application:\n{exception}");
        }
        catch { /* Ignorer les erreurs de sauvegarde du fichier d'erreur */ }
      }
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
        // Réinitialiser la couleur du bouton
        TestAuthButton.Background = System.Windows.Media.Brushes.Transparent;
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
            TestAuthButton.Background = Brushes.Red;
            ShowMessage(
              "Veuillez saisir un token personnel GitHub (PAT).",
              "Token manquant",
              MessageBoxButton.OK,
              MessageBoxImage.Warning);
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

              var login = serializer.ReadObject(stream) is GitHubUser user ? user.Login : "(inconnu)";
              AuthStatusTextBlock.Text = $"Authentification réussie. Connecté en tant que {login}.";
              AuthStatusTextBlock.Foreground = Brushes.Green;

              // Colorer le bouton en vert
              TestAuthButton.Background = Brushes.Green;

              CredentialStorage.Save(userName, _token);

              // Basculer vers l'onglet Dépôts
              if (MainTabControl != null && ReposTabItem != null)
              {
                MainTabControl.SelectedItem = ReposTabItem;
              }
            }
          }
          else
          {
            AuthStatusTextBlock.Text = $"Échec de l'authentification (code HTTP {(int)response.StatusCode}).";
            AuthStatusTextBlock.Foreground = Brushes.Red;
            TestAuthButton.Background = Brushes.Red;
          }
        }
      }
      catch (Exception exception)
      {
        AuthStatusTextBlock.Text = $"Erreur lors du test : {exception.Message}";
        AuthStatusTextBlock.Foreground = Brushes.Red;
        TestAuthButton.Background = Brushes.Red;
      }
    }

    private async void LoadReposButton_Click(object sender, RoutedEventArgs e)
    {
      // Annuler le chargement précédent s'il existe
      if (_loadCancellationTokenSource != null)
      {
        _loadCancellationTokenSource.Cancel();
        _loadCancellationTokenSource.Dispose();
      }

      // Créer un nouveau CancellationTokenSource
      _loadCancellationTokenSource = new CancellationTokenSource();
      var cancellationToken = _loadCancellationTokenSource.Token;

      ReposStatusTextBlock.Text = "Chargement des dépôts...";
      LoadReposButton.IsEnabled = false;
      CancelLoadButton.IsEnabled = true;

      LoadingWindow loadingWindow = null;

      try
      {
        if (string.IsNullOrWhiteSpace(_token))
        {
          ReposStatusTextBlock.Text = "Veuillez d'abord valider l'authentification (onglet Authentification).";
          return;
        }

        // Afficher la fenêtre de chargement
        loadingWindow = new LoadingWindow
        {
          Owner = this
        };
        _currentLoadingWindow = loadingWindow;
        loadingWindow.Show();

        // Récupérer le nombre maximum de repos à charger
        int? maxRepos = null;
        if (MaxReposComboBox != null && MaxReposComboBox.SelectedItem != null)
        {
          if (MaxReposComboBox.SelectedItem is string selectedText && selectedText == "Tous")
          {
            maxRepos = null; // Charger tous
          }
          else if (MaxReposComboBox.SelectedItem is int selectedValue)
          {
            maxRepos = selectedValue;
          }
        }

        using (var client = CreateHttpClient())
        {
          // Charger les dépôts en paginant
          var allRepos = await LoadAllRepositoriesAsync(client, cancellationToken, maxRepos, loadingWindow);

          if (!cancellationToken.IsCancellationRequested)
          {
            loadingWindow?.UpdateStatus("Ajout des dépôts à la liste...");

            _allRepositories.Clear();
            foreach (var repo in allRepos)
            {
              _allRepositories.Add(repo);
            }

            _currentPage = 1;
            UpdatePagination();
            ReposStatusTextBlock.Text = $"{_allRepositories.Count} dépôts chargés.";

            // Vérifier l'état local de tous les dépôts
            loadingWindow?.UpdateStatus("Vérification de l'état local des dépôts...");
            await CheckRepositoriesLocalStateAsync();
          }
          else
          {
            ReposStatusTextBlock.Text = "Chargement annulé.";
          }
        }
      }
      catch (OperationCanceledException)
      {
        ReposStatusTextBlock.Text = "Chargement annulé.";
      }
      catch (Exception exception)
      {
        ReposStatusTextBlock.Text = $"Erreur lors du chargement : {exception.Message}";
      }
      finally
      {
        // Fermer la fenêtre de chargement
        loadingWindow?.Close();
        _currentLoadingWindow = null;

        LoadReposButton.IsEnabled = true;
        CancelLoadButton.IsEnabled = false;
        if (_loadCancellationTokenSource != null)
        {
          _loadCancellationTokenSource.Dispose();
          _loadCancellationTokenSource = null;
        }
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

      // Charger l'état de la case à cocher ShowMessages
      if (ShowMessagesCheckBox != null)
      {
        ShowMessagesCheckBox.IsChecked = AppPreferencesStorage.LoadShowMessages();
      }

      // Le chargement de MaxRepos se fait dans InitializeMaxReposComboBox()
    }

    private void SaveStoredPreferences()
    {
      AppPreferencesStorage.SaveItemsPerPage(_itemsPerPage);

      if (LocalReposPathTextBox != null)
      {
        AppPreferencesStorage.SaveLocalReposPath(LocalReposPathTextBox.Text);
      }

      if (MaxReposComboBox != null && MaxReposComboBox.SelectedItem != null)
      {
        AppPreferencesStorage.SaveMaxRepos(MaxReposComboBox.SelectedItem);
      }

      if (ShowMessagesCheckBox != null)
      {
        AppPreferencesStorage.SaveShowMessages(ShowMessagesCheckBox.IsChecked ?? true);
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

    private void InitializeMaxReposComboBox()
    {
      if (MaxReposComboBox != null)
      {
        MaxReposComboBox.ItemsSource = new object[] { "Tous", 50, 100, 200, 500 };

        // Charger la valeur sauvegardée
        var savedValue = AppPreferencesStorage.LoadMaxRepos();
        MaxReposComboBox.SelectedItem = savedValue;

        // Ajouter le gestionnaire d'événement pour sauvegarder lors du changement
        MaxReposComboBox.SelectionChanged += MaxReposComboBox_SelectionChanged;
      }
    }

    private void MaxReposComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
      if (MaxReposComboBox != null && MaxReposComboBox.SelectedItem != null)
      {
        AppPreferencesStorage.SaveMaxRepos(MaxReposComboBox.SelectedItem);
      }
    }

    private void CancelLoadButton_Click(object sender, RoutedEventArgs e)
    {
      if (_loadCancellationTokenSource != null && !_loadCancellationTokenSource.IsCancellationRequested)
      {
        _loadCancellationTokenSource.Cancel();

        // Fermer la fenêtre de chargement si elle est ouverte
        if (_currentLoadingWindow != null)
        {
          _currentLoadingWindow.Close();
          _currentLoadingWindow = null;
        }
      }
    }

    private async Task CheckRepositoriesLocalStateAsync()
    {
      var localPath = LocalReposPathTextBox?.Text?.Trim();

      if (string.IsNullOrWhiteSpace(localPath))
      {
        LogInfo("Aucun chemin local spécifié, marquage de tous les dépôts comme non clonés");
        // Si pas de chemin local, tous les repos sont marqués comme non clonés
        foreach (var repo in _allRepositories)
        {
          repo.LocalState = RepositoryLocalState.NotCloned;
          repo.LocalPath = string.Empty;
        }
        return;
      }

      LogInfo($"Vérification de l'état des dépôts dans le répertoire: {localPath}");
      LogInfo($"Nombre de dépôts à vérifier: {_allRepositories.Count}");

      try
      {
        // Vérifier l'état de chaque dépôt de manière asynchrone
        await Task.Run(() =>
        {
          int processed = 0;
          foreach (var repo in _allRepositories)
          {
            try
            {
              LogInfo($"Vérification du dépôt: {repo.Name}");
              var (state, repoPath) = GitOperations.CheckRepositoryState(repo.Name, localPath);

              // Mettre à jour sur le thread UI
              Dispatcher.Invoke(() =>
              {
                repo.LocalState = state;
                repo.LocalPath = repoPath;
                LogInfo($"État du dépôt {repo.Name}: {state}, Chemin: {repoPath}");
              });
            }
            catch (Exception exception)
            {
              LogError($"Erreur lors de la vérification du dépôt {repo.Name}: {exception.Message}");
              Dispatcher.Invoke(() =>
              {
                repo.LocalState = RepositoryLocalState.NotCloned;
                repo.LocalPath = string.Empty;
              });
            }

            processed++;
            if (processed % 10 == 0) // Tous les 10 dépôts
            {
              LogInfo($"Progression: {processed}/{_allRepositories.Count} dépôts vérifiés");
            }
          }
        });

        LogInfo("Vérification des dépôts terminée");
      }
      catch (Exception exception)
      {
        LogError($"Erreur lors de la vérification des dépôts: {exception}");
      }

      // Rafraîchir l'affichage de la pagination pour mettre à jour les couleurs
      UpdatePagination();
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

    private async Task<List<GitHubRepository>> LoadAllRepositoriesAsync(HttpClient client, CancellationToken cancellationToken, int? maxRepos = null, LoadingWindow loadingWindow = null)
    {
      var allRepos = new List<GitHubRepository>();
      var serializer = new DataContractJsonSerializer(typeof(List<GitHubRepository>));
      int page = 1;
      const int perPage = 100;
      bool hasMorePages = true;

      while (hasMorePages && !cancellationToken.IsCancellationRequested)
      {
        // Vérifier si on a atteint le nombre maximum de repos
        if (maxRepos.HasValue && allRepos.Count >= maxRepos.Value)
        {
          break;
        }

        var statusMessage = $"Chargement des dépôts... (page {page}, {allRepos.Count} déjà chargés)";
        ReposStatusTextBlock.Text = statusMessage;

        loadingWindow?.Dispatcher.Invoke(() =>
          {
            loadingWindow.UpdateStatus(statusMessage);
          });

        var url = $"https://api.github.com/user/repos?per_page={perPage}&page={page}";
        var response = await client.GetAsync(url, cancellationToken);

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
              // Ajouter seulement le nombre nécessaire si maxRepos est défini
              if (maxRepos.HasValue)
              {
                var remaining = maxRepos.Value - allRepos.Count;
                if (remaining > 0)
                {
                  allRepos.AddRange(repos.Take(remaining));
                }
                if (repos.Count > remaining)
                {
                  hasMorePages = false;
                }
              }
              else
              {
                allRepos.AddRange(repos);
              }

              // Vérifier s'il y a une page suivante via l'en-tête Link
              if (response.Headers.Contains("Link"))
              {
                var linkHeader = response.Headers.GetValues("Link").FirstOrDefault();
                hasMorePages = HasNextPage(linkHeader) && (!maxRepos.HasValue || allRepos.Count < maxRepos.Value);
              }
              else
              {
                // Si pas d'en-tête Link, continuer si on a reçu exactement perPage éléments
                hasMorePages = repos.Count == perPage && (!maxRepos.HasValue || allRepos.Count < maxRepos.Value);
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

      cancellationToken.ThrowIfCancellationRequested();
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

    private void ClearLogButton_Click(object sender, RoutedEventArgs e)
    {
      if (LogTextBox != null)
      {
        LogTextBox.Clear();
        LogInfo("Journal effacé");
      }
    }

    private async void CloneButton_Click(object sender, RoutedEventArgs e)
    {
      if (sender is System.Windows.Controls.Button button && button.Tag is GitHubRepository repo)
      {
        var localPath = LocalReposPathTextBox?.Text?.Trim();

        if (string.IsNullOrWhiteSpace(localPath))
        {
          ShowMessage(
            "Veuillez d'abord spécifier le chemin local des dépôts dans l'onglet Authentification.",
            "Chemin manquant",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
          return;
        }

        button.IsEnabled = false;
        var originalContent = button.Content;
        button.Content = "Clonage en cours...";

        try
        {
          var success = await Task.Run(() => GitOperations.CloneRepositoryAsync(repo.HtmlUrl, localPath, repo.Name).Result);

          if (success)
          {
            // Mettre à jour l'état du dépôt cloné
            var (state, repoPath) = GitOperations.CheckRepositoryState(repo.Name, localPath);

            // Mettre à jour les propriétés sur le thread UI
            Dispatcher.Invoke(() =>
            {
              repo.LocalState = state;
              repo.LocalPath = repoPath;
            });

            // Rafraîchir l'interface utilisateur
            CommandManager.InvalidateRequerySuggested();

            ShowMessage(
              $"Le dépôt '{repo.Name}' a été cloné avec succès.",
              "Succès",
              MessageBoxButton.OK,
              MessageBoxImage.Information);
          }
          else
          {
            ShowMessage(
              $"Erreur lors du clonage du dépôt '{repo.Name}'. Vérifiez que Git est installé et que le chemin est valide.",
              "Erreur",
              MessageBoxButton.OK,
              MessageBoxImage.Error);
          }
        }
        catch (Exception exception)
        {
          ShowMessage(
            $"Erreur lors du clonage : {exception.Message}",
            "Erreur",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        }
        finally
        {
          button.IsEnabled = true;
          button.Content = originalContent;
        }
      }
    }

    private async void UpdateButton_Click(object sender, RoutedEventArgs e)
    {
      if (sender is System.Windows.Controls.Button button && button.Tag is GitHubRepository repo)
      {
        var localPath = LocalReposPathTextBox?.Text?.Trim();

        if (string.IsNullOrWhiteSpace(localPath))
        {
          ShowMessage(
            "Veuillez d'abord spécifier le chemin local des dépôts dans l'onglet Authentification.",
            "Chemin manquant",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
          return;
        }

        button.IsEnabled = false;
        var originalContent = button.Content;
        button.Content = "Mise à jour en cours...";

        try
        {
          var repoPath = Path.Combine(localPath, repo.Name);
          var success = await Task.Run(() => GitOperations.UpdateRepositoryAsync(repoPath).Result);

          if (success)
          {
            // Mettre à jour l'état du dépôt mis à jour
            var (state, updatedRepoPath) = GitOperations.CheckRepositoryState(repo.Name, localPath);

            // Mettre à jour les propriétés sur le thread UI
            Dispatcher.Invoke(() =>
            {
              repo.LocalState = state;
              repo.LocalPath = updatedRepoPath;
            });

            // Rafraîchir l'interface utilisateur
            CommandManager.InvalidateRequerySuggested();

            ShowMessage(
              $"Le dépôt '{repo.Name}' a été mis à jour avec succès.",
              "Succès",
              MessageBoxButton.OK,
              MessageBoxImage.Information);
          }
          else
          {
            // En cas d'échec, vérifier quand même l'état actuel
            var (currentState, currentRepoPath) = GitOperations.CheckRepositoryState(repo.Name, localPath);
            Dispatcher.Invoke(() =>
            {
              repo.LocalState = currentState;
              repo.LocalPath = currentRepoPath;
            });
            CommandManager.InvalidateRequerySuggested();

            ShowMessage(
              $"Erreur lors de la mise à jour du dépôt '{repo.Name}'.",
              "Erreur",
              MessageBoxButton.OK,
              MessageBoxImage.Error);
          }
        }
        catch (Exception exception)
        {
          ShowMessage(
            $"Erreur lors de la mise à jour : {exception.Message}",
            "Erreur",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        }
        finally
        {
          button.IsEnabled = true;
          button.Content = originalContent;
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
