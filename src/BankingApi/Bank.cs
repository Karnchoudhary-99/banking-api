namespace BankingApi;

/// <summary>In-memory registry of accounts: the single place accounts are created and looked up.</summary>
public sealed class Bank
{
    private readonly Dictionary<Guid, Account> _accounts = new();

    /// <summary>Opens a new account and registers it.</summary>
    public Account OpenAccount(string owner, decimal initialBalance = 0)
    {
        var account = new Account(owner, initialBalance);
        _accounts.Add(account.Id, account);
        return account;
    }

    /// <summary>Returns the account with the given id.</summary>
    /// <exception cref="KeyNotFoundException">No account with that id exists.</exception>
    public Account GetAccount(Guid id)
    {
        if (!_accounts.TryGetValue(id, out var account))
        {
            throw new KeyNotFoundException($"No account with id {id}.");
        }

        return account;
    }

    /// <summary>All registered accounts, including frozen and closed ones.</summary>
    public IReadOnlyCollection<Account> ListAccounts() => _accounts.Values.ToArray();
}
