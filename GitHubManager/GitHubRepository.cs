using System.Runtime.Serialization;

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
  }
}
