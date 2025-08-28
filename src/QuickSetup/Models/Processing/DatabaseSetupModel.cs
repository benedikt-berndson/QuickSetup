namespace QuickSetup.Models.Processing;

public record DatabaseSetupModel(string Owner, string Encoding, string Tablespace, int ConnectionLimit);
