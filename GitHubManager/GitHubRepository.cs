using System.Runtime.Serialization;
using System.Windows.Media;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GitHubManager
{
  [DataContract]
  public class GitHubRepository : INotifyPropertyChanged
  {
    private RepositoryLocalState _localState = RepositoryLocalState.NotChecked;

    [DataMember(Name = "name")]
    public string Name { get; set; }

    [DataMember(Name = "description")]
    public string Description { get; set; }

    [DataMember(Name = "private")]
    public bool IsPrivate { get; set; }

    [DataMember(Name = "html_url")]
    public string HtmlUrl { get; set; }

    private string _localPath;
    
    // Propriété pour le chemin local du dépôt
    public string LocalPath
    {
      get => _localPath;
      set
      {
        if (_localPath != value)
        {
          _localPath = value;
          OnPropertyChanged();
        }
      }
    }

    // Propriété non-sérialisée pour l'état local
    public RepositoryLocalState LocalState
    {
      get => _localState;
      set
      {
        if (_localState != value)
        {
          _localState = value;
          OnPropertyChanged();
        }
      }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
      PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
  }

  public enum RepositoryLocalState
  {
    NotChecked,
    UpToDate,
    NeedsUpdate,
    NotCloned
  }
}
