using System.Text.Json.Serialization;
using Tomlyn.Model;

namespace QuickSetup.Models;

public sealed class SettingsFileModel : ITomlMetadataProvider
{
  public string AuditLogPath { get; set; } = null!;
  public Dictionary<string, string> ConnectionStrings { get; set; } = null!;
  public List<string> ExtensionsPerSchema { get; set; } = null!;
  public CreateDatabaseMetadata CreateDatabaseMetadata { get; set; } = null!;
  public Dictionary<string, string> DatabaseToSchemaMap { get; set; } = null!;

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

  public bool GeneratePassword =>
    Password.Equals(SettingsReplacementTokens.Password, StringComparison.InvariantCultureIgnoreCase);

  public static User From(string name, string password) => new() { Name = name, Password = password };
}

public sealed class CreateDatabaseMetadata
{
  public string Owner { get; set; } = null!;
  public string Encoding { get; set; } = null!;
  public string Tablespace { get; set; } = null!;
  public int ConnectionLimit { get; set; }
}
