using QuickSetup.Models;

namespace QuickSetup.Commands.Postgres;

public sealed record PgSetupContext(
  SetupModel SetupModel,
  PgSetupCommandSettings CommandSettings,
  SettingsFileModel SettingsFile,
  PgSetupAuditLog Log);