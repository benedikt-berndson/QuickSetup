using System.Text.Json.Serialization;

namespace QuickSetup.Models.UserSettings;

public record UserSettings(
  [property: JsonPropertyName("auditLogOutputDirectory"), JsonPropertyOrder(1)] string AuditLogOutputDirectory,
  [property: JsonPropertyName("adminConnectionStrings"), JsonPropertyOrder(10)]
    Dictionary<string, string> AdminConnectionStrings,
  [property: JsonPropertyName("databaseSetupOptions"), JsonPropertyOrder(20)] DatabaseSetupOptions DatabaseSetupOptions,
  [property: JsonPropertyName("extensionsPerSchema"), JsonPropertyOrder(30)] string[] ExtensionsPerSchema,
  [property: JsonPropertyName("databaseSchemaSetupOptions"), JsonPropertyOrder(40)]
    Dictionary<string, string[]> DatabaseSchemaSetupOptions,
  [property: JsonPropertyName("userSetupOptions"), JsonPropertyOrder(50)] UserSetupOptions UserSetupOptions
)
{
  public UserSettings WithoutComments()
  {
    DatabaseSchemaSetupOptions.Remove("_");
    return this with
    {
      UserSetupOptions = new UserSetupOptions(
        SingleUserOverride: UserSetupOptions.SingleUserOverride with
        {
          Comment = null,
        },
        CommentAppUsers: null,
        CommentReadOnlyUsers: null,
        Owner: UserSetupOptions.Owner with
        {
          CommentUsername = null,
          CommentPassword = null,
        },
        AppUsers: UserSetupOptions.AppUsers.Select(x => x with { CommentUsername = null, CommentPassword = null }).ToArray(),
        ReadOnlyUsers: UserSetupOptions
          .ReadOnlyUsers.Select(x => x with { CommentUsername = null, CommentPassword = null })
          .ToArray()
      ),
    };
  }
};
