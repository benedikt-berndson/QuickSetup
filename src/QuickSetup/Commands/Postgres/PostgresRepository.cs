using Dapper;
using Npgsql;

namespace QuickSetup.Commands.Postgres;

public sealed class PostgresRepository : IPostgresRepository
{
  // key = (connectionStringName, database, username, password)
  private readonly Dictionary<string, NpgsqlDataSource> _dataSources = new();

  public void ExecuteAsRootAdmin(PgSetupContext ctx, string sql)
  {
    using var connection = GetConnection(ctx.AdminConnectionString);
    using var command = new NpgsqlCommand(sql, connection);
    command.ExecuteNonQuery();
  }

  public void ExecuteAsDbScopedAdmin(PgSetupContext ctx, string sql)
  {
    using var connection = GetConnection(ctx.AdminConnectionString, ctx.DatabaseName);
    using var command = new NpgsqlCommand(sql, connection);
    command.ExecuteNonQuery();
  }

  public void ExecuteAsSchemaScopedAdmin(PgSetupContext ctx, string sql)
  {
    using var connection = GetConnection(ctx.AdminConnectionString, ctx.DatabaseName, ctx.SchemaName);
    using var command = new NpgsqlCommand(sql, connection);
    command.ExecuteNonQuery();
  }

  public void ExecuteAsOwningUser(PgSetupContext ctx, string sql)
  {
    using var connection = GetConnection(
      ctx.AdminConnectionString,
      ctx.DatabaseName,
      ctx.SchemaName,
      ctx.Owner.Username,
      ctx.Owner.Password
    );
    using var command = new NpgsqlCommand(sql, connection);
    command.ExecuteNonQuery();
  }

  public HashSet<string> GetDatabaseNames(PgSetupContext ctx)
  {
    using var adminConnection = GetConnection(ctx.AdminConnectionString);
    return adminConnection.Query<string>("SELECT datname FROM pg_database").ToHashSet();
  }

  public HashSet<string> GetSchemaNames(PgSetupContext ctx)
  {
    const string schemaQuery = """
      SELECT schema_name
      FROM information_schema.schemata
      WHERE schema_name NOT IN ('information_schema', 'pg_catalog', 'pg_toast')
        AND schema_name NOT LIKE 'pg_temp_%'
        AND schema_name NOT LIKE 'pg_toast_temp_%'
      ORDER BY schema_name;
      """;

    using var connection = GetConnection(ctx.AdminConnectionString, ctx.DatabaseName);
    return connection.Query<string>(schemaQuery).Select(x => x.ToLower()).ToHashSet();
  }

  private NpgsqlConnection GetConnection(
    string adminConnectionString,
    string? database = null,
    string? schema = null,
    string? username = null,
    string? password = null
  )
  {
    var b = new NpgsqlConnectionStringBuilder(adminConnectionString) { Pooling = false };
    // Use defaults of admin connection string, unless overwritten
    if (database != null)
      b.Database = database;
    if (schema != null)
      b.SearchPath = schema;
    if (username != null)
      b.Username = username;
    if (password != null)
      b.Password = password;

    var connectionString = b.ToString();

    if (_dataSources.TryGetValue(connectionString, out var dataSource))
      return dataSource.OpenConnection();

    dataSource = new NpgsqlDataSourceBuilder(connectionString).Build();
    _dataSources.Add(connectionString, dataSource);
    return dataSource.OpenConnection();
  }
}
