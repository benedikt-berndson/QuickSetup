using Npgsql;
using OperationResult;
using QuickSetup.Models;
using Spectre.Console;
using Spectre.Console.Cli;
using Tomlyn;
using Tomlyn.Model;
using Tomlyn.Syntax;

namespace QuickSetup.Commands.Initialize;

public sealed class InitializeUserSettingsCommand : Command<InitializeUserSettingsCommandSettings>
{
  public override int Execute(CommandContext context, InitializeUserSettingsCommandSettings commandSettings)
  {
    AnsiConsole.MarkupLine("[bold yellow]Initializing user-settings template[/]");

    var pathResult = GetFilePath(commandSettings);
    if (!pathResult.IsSuccess)
      return 1;

    var builder = new NpgsqlConnectionStringBuilder
    {
      Host = "localhost",
      Port = 5432,
      Username = "postgres",
      Password = "<PASSWORD>",
      Database = "postgres",
      SearchPath = "public",
      IncludeErrorDetail = true,
    };

    var connectionStrings = new Dictionary<string, string> { ["default"] = builder.ToString() };

    var databaseSchemaDefinitions = new Dictionary<string, string> { ["db01"] = "schema01", ["db02"] = "schema01" };

    var createDbMetadata = new CreateDatabaseMetadata
    {
      Owner = "postgres",
      Encoding = "UTF8",
      Tablespace = "pg_default",
      ConnectionLimit = -1,
    };

    var owner = User.From("{{SCHEMA}}_machine_user_{{DATABASE}}", "{{PASSWORD}}");
    List<User> readWrite = [User.From("{{SCHEMA}}_app_user_{{DATABASE}}", "{{PASSWORD}}")];
    List<User> readonlyUser = [User.From("{{SCHEMA}}_readonly_user_{{DATABASE}}", "{{PASSWORD}}")];
    var tomlPropertiesMetadata = new TomlPropertiesMetadata();

    foreach (
      var property in (string[])
        [
          "connection_strings",
          "database_to_schema_map",
          "create_database_metadata",
          "owning_user_template",
          "read_write_user_templates",
          "readonly_user_templates",
        ]
    )
    {
      tomlPropertiesMetadata.SetProperty(
        property,
        new TomlPropertyMetadata
        {
          LeadingTrivia = [new TomlSyntaxTriviaMetadata { Kind = TokenKind.NewLine, Text = "\r\n" }],
          DisplayKind = TomlPropertyDisplayKind.Default,
          TrailingTrivia = null,
          TrailingTriviaAfterEndOfLine = null,
          Span = default,
        }
      );
    }

    var settings = new SettingsFileModel
    {
      AuditLogPath = "",
      ConnectionStrings = connectionStrings,
      CreateDatabaseMetadata = createDbMetadata,
      DatabaseToSchemaMap = databaseSchemaDefinitions,
      OwningUserTemplate = owner,
      ReadWriteUserTemplates = readWrite,
      ReadonlyUserTemplates = readonlyUser,
      PropertiesMetadata = tomlPropertiesMetadata,
    };

    var toml = Toml.FromModel(settings);

    var textPath = new TextPath(pathResult.Value)
    {
      SeparatorStyle = new Style(foreground: Color.Aqua),
      Justification = Justify.Left,
    };
    AnsiConsole.Write(textPath);
    AnsiConsole.WriteLine();
    File.WriteAllText(pathResult.Value, toml);

    return 0;
  }

  private static Result<string> GetFilePath(InitializeUserSettingsCommandSettings s)
  {
    if (!s.UserSettingsFilePath.EndsWith("toml"))
    {
      AnsiConsole.MarkupLine("[red]User-settings file must be TOML file[/]");
      return new ArgumentException("User-settings file must be TOML file", nameof(s.UserSettingsFilePath));
    }

    // Path.Combine actually takes the l
    var file = Path.IsPathFullyQualified(s.UserSettingsFilePath)
      ? s.UserSettingsFilePath
      : Path.Combine(Directory.GetCurrentDirectory(), Path.GetFullPath(s.UserSettingsFilePath));

    if (File.Exists(file) && !s.Replace)
    {
      AnsiConsole.MarkupLine("[red]User-settings file already exist and the replace flag is not set[/]");
      return new InvalidOperationException("User-settings file already exist and the replace flag is not set");
    }

    var dir = Path.GetDirectoryName(file);
    if (!Directory.Exists(dir))
    {
      Directory.CreateDirectory(dir!);
    }

    return file;
  }
}
