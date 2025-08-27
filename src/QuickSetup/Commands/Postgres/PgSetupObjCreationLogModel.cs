namespace QuickSetup.Commands.Postgres;

public sealed class PgSetupObjCreationLogModel
{
  /// <summary>
  /// Command used to create the database or skip comment
  /// </summary>
  public string? CreateDatabase { get; set; }

  /// <summary>
  /// Commands used to create the schemas or skip comment
  /// </summary>
  public string? CreateSchema { get; set; }

  /// <summary>
  /// Commands used to grant usage privileges
  /// </summary>
  public string? GrantUsage { get; set; }
}
