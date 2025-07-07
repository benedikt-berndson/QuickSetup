using QuickSetup.Common;

namespace QuickSetup.Models;

public record SetupContext(
  string Database,
  string Schema,
  User OwningUser,
  List<User> ReadWriteUsers,
  List<User> ReadonlyUsers
)
{
  public List<User> GetUsers()
  {
    var users = new List<User> { OwningUser };
    users.AddRange(ReadWriteUsers);
    users.AddRange(ReadonlyUsers);
    return users;
  }
};