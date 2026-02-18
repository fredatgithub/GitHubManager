using System.Runtime.Serialization;
using System.Windows.Media;

namespace GitHubManager
{
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

    // Propriété non-sérialisée pour l'état local
    public RepositoryLocalState LocalState { get; set; } = RepositoryLocalState.NotChecked;
  }

  public enum RepositoryLocalState
  {
    NotChecked,
    UpToDate,
    NeedsUpdate,
    NotCloned
  }
}
