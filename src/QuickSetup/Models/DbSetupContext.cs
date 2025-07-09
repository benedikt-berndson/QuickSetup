using DotMake.CommandLine;
using QuickSetup.Commands.Postgres;

namespace QuickSetup.Models;

public sealed record DbSetupContext(
  CliContext CliContext,
  SetupModel SetupModel,
  QuickSetupSettings Settings,
  PgSetupAuditLog Log);