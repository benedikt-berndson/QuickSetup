using System.Text.Json;
using QuickSetup.Common.Abstractions;
using QuickSetup.Models;
using QuickSetup.Models.Input;

namespace QuickSetup.Common;

public sealed class SettingsProvider : ISettingsProvider
{
  public const string DefaultUserSettingsFile = "settings.json";

  public UserSettings GetUserSettings(string userSettingsFilePath)
  {
    var path = Path.IsPathFullyQualified(userSettingsFilePath)
      ? userSettingsFilePath
      : Path.Join(Directory.GetCurrentDirectory(), userSettingsFilePath);

    var text = File.ReadAllText(path);
    var userSettings =
      JsonSerializer.Deserialize<UserSettings>(text) ?? throw new InvalidOperationException("Settings not initialized.");
    userSettings.DatabaseSchemaSetupOptions.Remove("_");
    var clone = userSettings with
    {
      UserSetupOptions = new UserSetupOptions(
        SingleUserOverride: userSettings.UserSetupOptions.SingleUserOverride with
        {
          Comment = null,
        },
        CommentAppUsers: null,
        CommentReadOnlyUsers: null,
        Owner: userSettings.UserSetupOptions.Owner with
        {
          CommentUsername = null,
          CommentPassword = null,
        },
        AppUsers: userSettings
          .UserSetupOptions.AppUsers.Select(x => x with { CommentUsername = null, CommentPassword = null })
          .ToArray(),
        ReadOnlyUsers: userSettings
          .UserSetupOptions.ReadOnlyUsers.Select(x => x with { CommentUsername = null, CommentPassword = null })
          .ToArray()
      ),
    };

    return clone;
  }
}
