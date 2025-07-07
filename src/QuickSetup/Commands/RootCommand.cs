using DotMake.CommandLine;

namespace QuickSetup.Commands;

[CliCommand(Description = "Root level")]
public class RootCommand
{
  [CliArgument(Description = "Path to settings file")]
  public string SettingsFile { get; set; } = "settings.toml";
  // public async Task RunAsync()
  // {
  //   Cli.Ext.ConfigureServices(services =>
  //   {
  //     
  //   });
  // }
}

