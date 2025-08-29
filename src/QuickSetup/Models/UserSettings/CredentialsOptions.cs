using System.Text.Json.Serialization;

namespace QuickSetup.Models.UserSettings;

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
