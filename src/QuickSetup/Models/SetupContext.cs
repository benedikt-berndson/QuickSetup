namespace QuickSetup.Models;

public record SetupContext(string Database, string Schema, DbUser MachineUser, DbUser AppUser, DbUser ReadonlyUser);