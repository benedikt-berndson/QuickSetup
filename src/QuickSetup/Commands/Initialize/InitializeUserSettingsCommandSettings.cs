using System.ComponentModel;
using QuickSetup.Common;
using Spectre.Console.Cli;

namespace QuickSetup.Commands.Initialize;

public sealed class InitializeUserSettingsCommandSettings : CommandSettings
{
  [CommandArgument(0, "[user settings file path]")]
  [DefaultValue(SettingsProvider.DefaultUserSettingsFile)]
  public string UserSettingsFilePath { get; set; } = null!;

  [CommandOption("-r|--replace")]
  public bool Replace { get; set; }
}
