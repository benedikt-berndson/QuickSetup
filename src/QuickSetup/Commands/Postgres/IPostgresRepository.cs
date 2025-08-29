namespace QuickSetup.Commands.Postgres;

public interface IPostgresRepository
{
  void ExecuteAsRootAdmin(PgSetupContext ctx, string sql);
  void ExecuteAsRootAdmin(string adminConnectionString, string sql);
  void ExecuteAsDbScopedAdmin(PgSetupContext ctx, string sql);
  void ExecuteAsDbScopedAdmin(string adminConnectionString, string databaseName, string sql);
  void ExecuteAsSchemaScopedAdmin(PgSetupContext ctx, string sql);
  void ExecuteAsOwningUser(PgSetupContext ctx, string sql);
  HashSet<string> GetDatabaseNames(string adminConnectionString);
  HashSet<string> GetSchemaNames(PgSetupContext ctx);
  HashSet<string> GetAllUsers(string adminConnectionString);
}
