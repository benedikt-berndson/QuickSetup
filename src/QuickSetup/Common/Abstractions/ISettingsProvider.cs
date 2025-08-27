using QuickSetup.Models;

namespace QuickSetup.Common.Abstractions;

public interface ISettingsProvider
{
  SettingsFileModel GetSettings();

  void Init(string settingsFile);
}
