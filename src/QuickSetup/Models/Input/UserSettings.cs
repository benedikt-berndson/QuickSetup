using System.Text.Json.Serialization;

namespace QuickSetup.Models.Input;

public record UserSettings(
  [property: JsonPropertyOrder(0)] AuditLogOptions AuditLogOptions,
  [property: JsonPropertyOrder(10)] Dictionary<string, string> AdminConnectionStrings,
  [property: JsonPropertyOrder(20)] DatabaseSetupOptions DatabaseSetupOptions,
  [property: JsonPropertyOrder(30)] string[] ExtensionsPerSchema,
  [property: JsonPropertyOrder(40)] Dictionary<string, string> DatabaseSchemaSetupOptions,
  [property: JsonPropertyOrder(50)] UserSetupOptions UserSetupOptions
);

public record AuditLogOptions(
  [property: JsonPropertyName("_comment"), JsonPropertyOrder(0)] string? Comment,
  [property: JsonPropertyOrder(10)] string Path
);

public record DatabaseSetupOptions(
  [property: JsonPropertyName("_comment"), JsonPropertyOrder(0)] string? Comment,
  [property: JsonPropertyOrder(1)] string Owner,
  [property: JsonPropertyOrder(10)] string Encoding,
  [property: JsonPropertyOrder(20)] string Tablespace,
  [property: JsonPropertyOrder(30)] int ConnectionLimit
);

public record UserSetupOptions(
  [property: JsonPropertyName("_useSingleUserComment"), JsonPropertyOrder(0)]
    string? CommentUseSingleUserForAllSchemasWithinSameDatabase,
  [property: JsonPropertyOrder(10)] bool UseSingleUserForAllSchemasWithinSameDatabase,
  [property: JsonPropertyOrder(20)] CredentialsOptions Owner,
  [property: JsonPropertyName("_appUserComment"), JsonPropertyOrder(30)] string? CommentAppUsers,
  [property: JsonPropertyOrder(40)] CredentialsOptions[] AppUsers,
  [property: JsonPropertyName("_readonlyUserComment"), JsonPropertyOrder(50)] string? CommentReadOnlyUsers,
  [property: JsonPropertyOrder(60)] CredentialsOptions[] ReadOnlyUsers
);

public record CredentialsOptions(
  [property: JsonPropertyName("_usernameComment"), JsonPropertyOrder(0)] string? CommentUsername,
  [property: JsonPropertyOrder(10)] string Username,
  [property: JsonPropertyName("_passwordComment"), JsonPropertyOrder(20)] string? CommentPassword,
  [property: JsonPropertyOrder(30)] string Password
)
{
  [JsonIgnore]
  public bool GeneratePassword => string.IsNullOrEmpty(Password) || string.IsNullOrWhiteSpace(Password);
};
