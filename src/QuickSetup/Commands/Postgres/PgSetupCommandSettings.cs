using System.ComponentModel;
using Spectre.Console.Cli;

namespace QuickSetup.Commands.Postgres;

public sealed class PgSetupCommandSettings : CommandSettings
{
  [Description("Path to settings file, defaults to current directory")]
  [CommandOption("-s|--settings-file")]
  [DefaultValue(Common.SettingsProvider.DefaultSettingsFile)]
  public string SettingsFile { get; set; } = null!;

  [Description("Define which connection string from settings.toml to use")]
  [CommandOption("-n|--connection-string-name")]
  [DefaultValue("default")]
  public string ConnectionStringName { get; set; } = null!;

  [Description("Drop existing databases")]
  [CommandOption("--drop-databases")]
  public bool DropDatabases { get; set; }

  [Description("Drop existing schemas")]
  [CommandOption("--drop-schemas")]
  public bool DropSchemas { get; set; }
}