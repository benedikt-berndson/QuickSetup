using QuickSetup.Models;
using QuickSetup.Models.Input;

namespace QuickSetup.Commands.Postgres;

public sealed record PgSetupContext(
  DatabaseSetupProcessingModel ProcessingModel,
  PgSetupCommandSettings CommandSettings,
  UserSettings UserSettings,
  PgSetupAuditLog Log
);
