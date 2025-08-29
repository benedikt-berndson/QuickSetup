using QuickSetup.Models.UserSettings;

namespace QuickSetup.Common.Abstractions;

public interface ISettingsProvider
{
  UserSettings GetUserSettings(string userSettingsFilePath);
}
