using System.Text.Json.Serialization;

namespace QuickSetup.Models.UserSettings;

public record SingleUserOverride(
  [property: JsonPropertyName("_comment"), JsonPropertyOrder(0)] string? Comment,
  [property: JsonPropertyName("isActive"), JsonPropertyOrder(10)] bool IsActive,
  [property: JsonPropertyName("schemaNameReplacement"), JsonPropertyOrder(20)] string UsernameSchemaNameReplacement,
  [property: JsonPropertyName("ownerPassword"), JsonPropertyOrder(30)] string OwnerPassword,
  [property: JsonPropertyName("appUsersPassword"), JsonPropertyOrder(40)] string AppUsersPassword,
  [property: JsonPropertyName("readOnlyUsersPassword"), JsonPropertyOrder(50)] string ReadOnlyUsersPassword
);
