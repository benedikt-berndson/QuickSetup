using System.Text.Json;
using QuickSetup.Common.Abstractions;
using QuickSetup.Models.UserSettings;

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

    return userSettings.WithoutComments();
  }
}
