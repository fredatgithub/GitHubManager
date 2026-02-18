using System.Runtime.Serialization;

namespace GitHubManager
{
  [DataContract]
  internal class GitHubUser
  {
    [DataMember(Name = "login")]
    public string Login { get; set; }

    [DataMember(Name = "name")]
    public string Name { get; set; }
  }
}
