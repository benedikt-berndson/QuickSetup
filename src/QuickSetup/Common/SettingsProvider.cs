using QuickSetup.Common.Abstractions;
using QuickSetup.Models;
using Tomlyn;

namespace QuickSetup.Common;

public sealed class SettingsProvider : ISettingsProvider
{
  public const string DefaultSettingsFile = "settings.toml";
  
  private SettingsFileModel? _instance;

  public SettingsFileModel GetSettings()
    => _instance ?? throw new InvalidOperationException("Settings not initialized.");

  public void Init(string settingsFile)
  {
    var text = File.ReadAllText(settingsFile);
    _instance = Toml.ToModel<SettingsFileModel>(text);
  }
}