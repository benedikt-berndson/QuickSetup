using System.Text.Json;
using Npgsql;
using OperationResult;
using QuickSetup.Models.Input;
using Spectre.Console;
using Spectre.Console.Cli;

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
      Password = "R_E_P_L_A_C_E__M_E",
      Database = "postgres",
      SearchPath = "public",
      IncludeErrorDetail = true,
    };

    var inputModel = new UserSettings(
      "",
      new Dictionary<string, string> { ["default"] = builder.ToString() },
      new DatabaseSetupOptions("postgres", "UTF8", "pg_default", -1),
      ["btree_gist", "uuid-ossp"],
      new Dictionary<string, string[]>
      {
        ["_"] =
        [
          "Hashmap containing the key-value pairs of the form database-name: [schema-name1, maybe-schema-name2]. This comment is removed automatically.",
        ],
        ["db01"] = ["schema01"],
        ["db02"] = ["schema01"],
      },
      new UserSetupOptions(
        new SingleUserOverride(
          "If you want to use a single user for all schemas within the same database, set this to true. You have to provide a replacement for {{SCHEMA}} token. Set passwords or leave the properties empty to generate random passwords.",
          false,
          "",
          "",
          "",
          ""
        ),
        new CredentialsOptions(
          "Provide a template for the user that will be the owner of all created objects. Permitted tokens: {{DATABASE}}, {{SCHEMA}}.",
          "{{SCHEMA}}_machine_user_{{DATABASE}}",
          "Provide a custom password or leave the field empty to generate a random password.",
          ""
        ),
        "You can provide multiple AppUser entries. This might be useful if you want to track connections from different machines.",
        [
          new CredentialsOptions(
            "Provide a template for the user that will have read & write permissions on all created objects. Permitted tokens: {{DATABASE}}, {{SCHEMA}}.",
            "{{SCHEMA}}_app_user_{{DATABASE}}",
            "Provide a custom password or leave the field empty to generate a random password.",
            ""
          ),
        ],
        "You can provide multiple ReadOnlyUser entries. This might be useful if you want to track connections from different machines.",
        [
          new CredentialsOptions(
            "Provide a template for the user that will, as the name implies, only have read permissions on all created objects. Permitted tokens: {{DATABASE}}, {{SCHEMA}}.",
            "{{SCHEMA}}_readonly_user_{{DATABASE}}",
            "Provide a custom password or leave the field empty to generate a random password.",
            ""
          ),
        ]
      )
    );

    var json = JsonSerializer.Serialize(inputModel, new JsonSerializerOptions { WriteIndented = true });
    File.WriteAllText(pathResult.Value, json);

    return 0;
  }

  private static Result<string> GetFilePath(InitializeUserSettingsCommandSettings s)
  {
    if (!s.UserSettingsFilePath.EndsWith("json"))
    {
      AnsiConsole.MarkupLine("[red]User-settings file must be JSON file[/]");
      return new ArgumentException("User-settings file must be JSON file", nameof(s.UserSettingsFilePath));
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
