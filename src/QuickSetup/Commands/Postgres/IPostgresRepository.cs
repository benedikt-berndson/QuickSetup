namespace QuickSetup.Commands.Postgres;

public interface IPostgresRepository
{
  void ExecuteAsRootAdmin(string connectionName, string sql);
  void ExecuteAsDbScopedAdmin(string connectionName, string database, string sql);
  void ExecuteAsSchemaScopedAdmin(string connectionName, string database, string schema, string sql);

  void ExecuteAsOwningUser(
    string connectionName,
    string database,
    string schema,
    string username,
    string password,
    string sql
  );

  HashSet<string> GetDatabaseNames(string connectionName);
  HashSet<string> GetSchemaNames(string connectionName, string database);
}
