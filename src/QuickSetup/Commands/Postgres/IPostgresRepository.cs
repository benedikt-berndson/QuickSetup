namespace QuickSetup.Commands.Postgres;

public interface IPostgresRepository
{
  void ExecuteAsRootAdmin(PgSetupContext ctx, string sql);
  void ExecuteAsDbScopedAdmin(PgSetupContext ctx, string sql);
  void ExecuteAsSchemaScopedAdmin(PgSetupContext ctx, string sql);
  void ExecuteAsOwningUser(PgSetupContext ctx, string sql);
  HashSet<string> GetDatabaseNames(PgSetupContext ctx);
  HashSet<string> GetSchemaNames(PgSetupContext ctx);
}
