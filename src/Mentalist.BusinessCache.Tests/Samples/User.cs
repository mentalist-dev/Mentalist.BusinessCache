namespace Mentalist.BusinessCache.Tests.Samples;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public IList<Account> Accounts { get; init; } = new List<Account>();
}

public class Account
{
    public Guid Id { get; set; } = Guid.NewGuid();
}