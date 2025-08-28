using QuickSetup.Models;
using QuickSetup.Models.Input;
using QuickSetup.Models.Processing;

namespace QuickSetup.Commands.Postgres;

// public sealed record PgSetupContext(
//   DatabaseSetupProcessingModel ProcessingModel,
//   PgSetupCommandSettings CommandSettings,
//   UserSettings UserSettings,
//   PgSetupAuditLog Log
// );

public sealed record PgSetupContext(
  string AdminConnectionString,
  string DatabaseName,
  string SchemaName,
  bool DropDatabases,
  bool DropSchemas,
  DatabaseSetupModel DatabaseSetupModel,
  string[] ExtensionsPerSchema,
  CredentialsModel Owner,
  List<CredentialsModel> AppUserCredentialsModels,
  List<CredentialsModel> ReadonlyUserCredentialsModels,
  PgSetupAuditLog Log
)
{
  public List<CredentialsModel> GetAllUsers()
  {
    var users = new List<CredentialsModel> { Owner };
    users.AddRange(AppUserCredentialsModels);
    users.AddRange(ReadonlyUserCredentialsModels);
    return users;
  }
};
