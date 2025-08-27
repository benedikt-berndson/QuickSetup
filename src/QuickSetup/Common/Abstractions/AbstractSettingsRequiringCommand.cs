// using DotMake.CommandLine;
//
// namespace QuickSetup.Common.Abstractions;
//
// public abstract class AbstractSettingsRequiringCommand
// {
//   protected readonly ISettingsProvider SettingsProvider;
//
//   // ReSharper disable once MemberCanBePrivate.Global
//   [CliOption(Description = "Path to settings file, defaults to current directory", Alias = "sf")]
//   public string SettingsFile { get; set; } = Common.SettingsProvider.DefaultSettingsFile;
//
//   protected AbstractSettingsRequiringCommand(ISettingsProvider settingsProvider)
//   {
//     SettingsProvider = settingsProvider;
//   }
// }
