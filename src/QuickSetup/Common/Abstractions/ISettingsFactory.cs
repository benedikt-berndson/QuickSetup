using QuickSetup.Models;

namespace QuickSetup.Common.Abstractions;

public interface ISettingsFactory
{
  QuickSetupSettings GetInstance();

  void Init(string settingsFile);
}