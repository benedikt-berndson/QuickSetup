using QuickSetup.Models;
using QuickSetup.Models.Input;

namespace QuickSetup.Common.Abstractions;

public interface ISettingsProvider
{
  UserSettings GetUserSettings();

  void Init(string settingsFile);
}
