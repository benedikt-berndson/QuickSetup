using System.Text.Json.Serialization;

namespace QuickSetup.Models.UserSettings;

public record DatabaseSetupOptions(
  [property: JsonPropertyName("owner"), JsonPropertyOrder(1)] string Owner,
  [property: JsonPropertyName("encoding"), JsonPropertyOrder(10)] string Encoding,
  [property: JsonPropertyName("tablespace"), JsonPropertyOrder(20)] string Tablespace,
  [property: JsonPropertyName("connectionLimit"), JsonPropertyOrder(30)] int ConnectionLimit
);
