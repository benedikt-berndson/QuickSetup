using System.Text.Json;
using QuickSetup.Common.Abstractions;
using QuickSetup.Models;
using QuickSetup.Models.Input;

namespace QuickSetup.Common;

public sealed class SettingsProvider : ISettingsProvider
{
  public const string DefaultUserSettingsFile = "settings.json";

  private UserSettings? _instance;

  public UserSettings GetUserSettings() => _instance ?? throw new InvalidOperationException("Settings not initialized.");

  public void Init(string settingsFile)
  {
    var text = File.ReadAllText(settingsFile);
    _instance = JsonSerializer.Deserialize<UserSettings>(text);
  }
}
