using System.Text.Json.Serialization;

namespace QuickSetup.Models.Input;

public record UserSettings(
  [property: JsonPropertyName("auditLogOutputDirectory"), JsonPropertyOrder(1)] string AuditLogOutputDirectory,
  [property: JsonPropertyName("adminConnectionStrings"), JsonPropertyOrder(10)]
    Dictionary<string, string> AdminConnectionStrings,
  [property: JsonPropertyName("databaseSetupOptions"), JsonPropertyOrder(20)] DatabaseSetupOptions DatabaseSetupOptions,
  [property: JsonPropertyName("extensionsPerSchema"), JsonPropertyOrder(30)] string[] ExtensionsPerSchema,
  [property: JsonPropertyName("databaseSchemaSetupOptions"), JsonPropertyOrder(40)]
    Dictionary<string, string[]> DatabaseSchemaSetupOptions,
  [property: JsonPropertyName("userSetupOptions"), JsonPropertyOrder(50)] UserSetupOptions UserSetupOptions
);

public record DatabaseSetupOptions(
  [property: JsonPropertyName("owner"), JsonPropertyOrder(1)] string Owner,
  [property: JsonPropertyName("encoding"), JsonPropertyOrder(10)] string Encoding,
  [property: JsonPropertyName("tablespace"), JsonPropertyOrder(20)] string Tablespace,
  [property: JsonPropertyName("connectionLimit"), JsonPropertyOrder(30)] int ConnectionLimit
);

public record UserSetupOptions(
  [property: JsonPropertyName("singleUserOverride"), JsonPropertyOrder(10)] SingleUserOverride SingleUserOverride,
  [property: JsonPropertyName("owner"), JsonPropertyOrder(20)] CredentialsOptions Owner,
  [property: JsonPropertyName("_commentAppUsers"), JsonPropertyOrder(30)] string? CommentAppUsers,
  [property: JsonPropertyName("appUsers"), JsonPropertyOrder(40)] CredentialsOptions[] AppUsers,
  [property: JsonPropertyName("_commentReadOnlyUsers"), JsonPropertyOrder(50)] string? CommentReadOnlyUsers,
  [property: JsonPropertyName("readOnlyUsers"), JsonPropertyOrder(60)] CredentialsOptions[] ReadOnlyUsers
);

public record SingleUserOverride(
  [property: JsonPropertyName("_comment"), JsonPropertyOrder(0)] string? Comment,
  [property: JsonPropertyName("isActive"), JsonPropertyOrder(10)] bool IsActive,
  [property: JsonPropertyName("schemaNameReplacement"), JsonPropertyOrder(20)] string UsernameSchemaNameReplacement,
  [property: JsonPropertyName("ownerPassword"), JsonPropertyOrder(30)] string OwnerPassword,
  [property: JsonPropertyName("appUsersPassword"), JsonPropertyOrder(40)] string AppUsersPassword,
  [property: JsonPropertyName("readOnlyUsersPassword"), JsonPropertyOrder(50)] string ReadOnlyUsersPassword
);

public record CredentialsOptions(
  [property: JsonPropertyName("_commentUsername"), JsonPropertyOrder(0)] string? CommentUsername,
  [property: JsonPropertyName("username"), JsonPropertyOrder(10)] string Username,
  [property: JsonPropertyName("_commentPassword"), JsonPropertyOrder(20)] string? CommentPassword,
  [property: JsonPropertyName("password"), JsonPropertyOrder(30)] string Password
)
{
  [JsonIgnore]
  public bool GeneratePassword => string.IsNullOrEmpty(Password) || string.IsNullOrWhiteSpace(Password);
};
