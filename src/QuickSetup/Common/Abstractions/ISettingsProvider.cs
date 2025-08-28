using QuickSetup.Models.Input;

namespace QuickSetup.Common.Abstractions;

public interface ISettingsProvider
{
  UserSettings GetUserSettings(string userSettingsFilePath);
}
