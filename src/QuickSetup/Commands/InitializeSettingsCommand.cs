using DotMake.CommandLine;
using Npgsql;
using QuickSetup.Common;
using QuickSetup.Models;
using Tomlyn;
using Tomlyn.Model;
using Tomlyn.Syntax;

namespace QuickSetup.Commands;

[CliCommand(Description = "Initialize empty settings file", Parent = typeof(RootCommand),
  Alias = "init")]
public sealed class InitializeSettingsCommand : ICliRun
{
  [CliArgument(Description = "Name or path of the output file.")]
  public string Output { get; set; } = SettingsFactory.DefaultSettingsFile;

  [CliOption(Description = "Replace existing file", Alias = "r")]
  public bool Replace { get; set; }

  public void Run()
  {
    if (!Output.EndsWith("toml"))
      throw new ArgumentException("Command execution failed: Output file must be TOML file");

    if (File.Exists(Output) && !Replace)
      throw new InvalidOperationException(
        "Command execution failed: Output file already exist and the replace flag is not set");

    var builder = new NpgsqlConnectionStringBuilder();
    builder.Host = "localhost";
    builder.Port = 5432;
    builder.Username = "postgres";
    builder.Password = "<PASSWORD>";
    builder.Database = "postgres";
    builder.SearchPath = "public";
    builder.IncludeErrorDetail = true;

    var connectionStrings = new Dictionary<string, string>
    {
      ["connection_name"] = builder.ToString()
    };

    var databaseSchemaDefinitions = new Dictionary<string, List<string>>
    {
      ["db01"] = ["schema01", "schema02", "schema03"],
      ["db02"] = ["schema01", "schema02", "schema03"]
    };

    var owner = User.From("{{SCHEMA}}_machine_user_{{DATABASE}}", "{{PASSWORD}}");
    List<User> readWrite = [User.From("{{SCHEMA}}_app_user_{{DATABASE}}", "{{PASSWORD}}")];
    List<User> readonlyUser = [User.From("{{SCHEMA}}_readonly_user_{{DATABASE}}", "{{PASSWORD}}")];
    var tomlPropertiesMetadata = new TomlPropertiesMetadata();

    foreach (var property in (string[])
      [
        "connection_strings", "database_schema_definitions", "owning_user_template", "read_write_user_templates", "readonly_user_templates"
      ]
    )
    {
      tomlPropertiesMetadata.SetProperty(property, new TomlPropertyMetadata
      {
        LeadingTrivia =
        [
          new TomlSyntaxTriviaMetadata
          {
            Kind = TokenKind.NewLine,
            Text = "\r\n"
          }
        ],
        DisplayKind = TomlPropertyDisplayKind.Default,
        TrailingTrivia = null,
        TrailingTriviaAfterEndOfLine = null,
        Span = default
      });
    }

    var settings = new QuickSetupSettings
    {
      MarkdownOutput = "setup_log.md",
      ConnectionStrings = connectionStrings,
      DatabaseSchemaDefinitions = databaseSchemaDefinitions,
      OwningUserTemplate = owner,
      ReadWriteUserTemplates = readWrite,
      ReadonlyUserTemplates = readonlyUser,
      PropertiesMetadata = tomlPropertiesMetadata
    };

    var toml = Toml.FromModel(settings);
    File.WriteAllText(Path.GetFullPath(Output), toml);
  }
}