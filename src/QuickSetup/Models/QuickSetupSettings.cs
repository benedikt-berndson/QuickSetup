using System.Text.Json.Serialization;
using Tomlyn.Model;

namespace QuickSetup.Models;

public sealed class QuickSetupSettings : ITomlMetadataProvider
{
  public string? MarkdownOutput { get; set; }
  public Dictionary<string, string> ConnectionStrings { get; set; } = null!;
  public Dictionary<string, List<string>> DatabaseSchemaDefinitions { get; set; } = null!;

  public User OwningUserTemplate { get; set; } = null!;
  public List<User> ReadWriteUserTemplates { get; set; } = null!;
  public List<User> ReadonlyUserTemplates { get; set; } = null!;

  [JsonIgnore]
  public TomlPropertiesMetadata? PropertiesMetadata { get; set; }
}

public sealed class User
{
  public string Name { get; set; } = null!;
  public string Password { get; set; } = null!;

  public bool GeneratePassword => Password.Equals("{{PASSWORD}}", StringComparison.InvariantCultureIgnoreCase);

  public static User From(string name, string password)
    => new()
    {
      Name = name,
      Password = password
    };
}