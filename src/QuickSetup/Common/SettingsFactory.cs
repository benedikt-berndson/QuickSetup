using QuickSetup.Common.Abstractions;
using Tomlyn;

namespace QuickSetup.Common;

public sealed class SettingsFactory : ISettingsFactory
{
  public const string DefaultSettingsFile = "settings.toml";
  
  private QuickSetupSettings? _instance;

  public QuickSetupSettings GetInstance()
    => _instance ?? throw new InvalidOperationException("Settings not initialized.");

  public void Init(string settingsFile)
  {
    var text = File.ReadAllText(settingsFile);
    _instance = Toml.ToModel<QuickSetupSettings>(text);
  }
}