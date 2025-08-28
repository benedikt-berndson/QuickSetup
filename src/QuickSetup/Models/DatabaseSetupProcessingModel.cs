using QuickSetup.Models.Processing;

namespace QuickSetup.Models;

public sealed record DatabaseSetupProcessingModel(
  string DatabaseName,
  string SchemaName,
  Credentials Owner,
  List<Credentials> AppUsers,
  List<Credentials> ReadonlyUsers
)
{
  public List<Credentials> GetUsers()
  {
    var users = new List<Credentials> { Owner };
    users.AddRange(AppUsers);
    users.AddRange(ReadonlyUsers);
    return users;
  }
};
