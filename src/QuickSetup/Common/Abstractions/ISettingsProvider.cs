using QuickSetup.Models;

namespace QuickSetup.Common.Abstractions;

public interface ISettingsProvider
{
  QuickSetupSettings GetSettings();

  void Init(string settingsFile);
}