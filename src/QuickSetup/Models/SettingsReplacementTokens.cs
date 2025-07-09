namespace QuickSetup.Models;

public static class SettingsReplacementTokens
{
  public const string Password = "{{PASSWORD}}";
  public const string Schema = "{{SCHEMA}}";
  public const string Database = "{{Database}}";

  public static string ReplaceSchema(this string input, string schema)
    => input.Replace(Schema, schema, StringComparison.CurrentCultureIgnoreCase);

  public static string ReplaceDatabase(this string input, string database)
    => input.Replace(Database, database, StringComparison.CurrentCultureIgnoreCase);

  public static string ReplaceUserTokens(this string input, string database, string schema)
    => input.ReplaceDatabase(database).ReplaceSchema(schema);
}