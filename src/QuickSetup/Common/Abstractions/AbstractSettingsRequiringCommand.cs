using DotMake.CommandLine;

namespace QuickSetup.Common.Abstractions;

public abstract class AbstractSettingsRequiringCommand
{
  protected readonly ISettingsFactory SettingsFactoryInstance;

  // ReSharper disable once MemberCanBePrivate.Global
  [CliOption(Description = "Path to settings file, defaults to current directory", Alias = "sf")]
  public string SettingsFile { get; set; } = SettingsFactory.DefaultSettingsFile;

  protected AbstractSettingsRequiringCommand(ISettingsFactory settingsFactoryInstance)
  {
    SettingsFactoryInstance = settingsFactoryInstance;
  }
}