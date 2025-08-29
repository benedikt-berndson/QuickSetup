using System.Text.Json.Serialization;

namespace QuickSetup.Models.UserSettings;

public record UserSetupOptions(
  [property: JsonPropertyName("singleUserOverride"), JsonPropertyOrder(10)] SingleUserOverride SingleUserOverride,
  [property: JsonPropertyName("owner"), JsonPropertyOrder(20)] CredentialsOptions Owner,
  [property: JsonPropertyName("_commentAppUsers"), JsonPropertyOrder(30)] string? CommentAppUsers,
  [property: JsonPropertyName("appUsers"), JsonPropertyOrder(40)] CredentialsOptions[] AppUsers,
  [property: JsonPropertyName("_commentReadOnlyUsers"), JsonPropertyOrder(50)] string? CommentReadOnlyUsers,
  [property: JsonPropertyName("readOnlyUsers"), JsonPropertyOrder(60)] CredentialsOptions[] ReadOnlyUsers
);
