namespace BankingApi;

/// <summary>
/// In-memory registry of accounts: the single place accounts are created and
/// looked up. It also owns the transaction ledger: every Bank-mediated
/// deposit, withdrawal and transfer is recorded, in order, per account, and
/// a rejected operation records nothing.
/// </summary>
public sealed class Bank
{
    private readonly Dictionary<Guid, Account> _accounts = new();
    private readonly Dictionary<Guid, List<Transaction>> _transactions = new();
    private readonly Dictionary<string, Transfer> _transfersByKey = new();

    /// <summary>Opens a new account and registers it.</summary>
    public Account OpenAccount(string owner, decimal initialBalance = 0)
    {
        var account = new Account(owner, initialBalance);
        _accounts.Add(account.Id, account);
        _transactions.Add(account.Id, new List<Transaction>());
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

    /// <summary>Deposits into the account with the given id and records the entry.</summary>
    public void Deposit(Guid id, decimal amount)
    {
        var account = GetAccount(id);
        account.Deposit(amount);
        Record(account, TransactionKind.Deposit, amount);
    }

    /// <summary>Withdraws from the account with the given id and records the entry.</summary>
    public void Withdraw(Guid id, decimal amount)
    {
        var account = GetAccount(id);
        account.Withdraw(amount);
        Record(account, TransactionKind.Withdrawal, amount);
    }

    /// <summary>Freezes the account with the given id.</summary>
    public void Freeze(Guid id) => GetAccount(id).Freeze();

    /// <summary>Unfreezes the account with the given id.</summary>
    public void Unfreeze(Guid id) => GetAccount(id).Unfreeze();

    /// <summary>Closes the account with the given id (requires a zero balance).</summary>
    public void CloseAccount(Guid id) => GetAccount(id).Close();

    /// <summary>
    /// All registered accounts, including frozen and closed ones.</summary>
    public IReadOnlyCollection<Account> ListAccounts() => _accounts.Values.ToArray();

    /// <summary>
    /// The idempotency keys recorded so far, mapped to their transfers.
    /// Used by the snapshot store so a restarted server still recognises
    /// repeated transfers instead of moving the money twice.
    /// </summary>
    public IReadOnlyDictionary<string, Transfer> IdempotencyTransfers() =>
        new Dictionary<string, Transfer>(_transfersByKey);

    /// <summary>
    /// Restores one persisted account together with its ledger, in the
    /// order given. For the JSON snapshot store only.
    /// </summary>
    public void RestoreAccount(Account account, IReadOnlyList<Transaction> transactions)
    {
        if (_accounts.ContainsKey(account.Id))
        {
            throw new InvalidOperationException($"Account {account.Id} is already registered.");
        }

        foreach (var entry in transactions)
        {
            if (entry.AccountId != account.Id)
            {
                throw new ArgumentException("Ledger entries must belong to the restored account.", nameof(transactions));
            }
        }

        _accounts.Add(account.Id, account);
        _transactions.Add(account.Id, transactions.ToList());
    }

    /// <summary>Restores one persisted idempotency key. For the snapshot store only.</summary>
    public void RestoreIdempotencyKey(string key, Transfer transfer)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Idempotency key must not be empty.", nameof(key));
        }

        _transfersByKey.Add(key, transfer);
    }

    /// <summary>
    /// The ledger for one account, oldest first. A copy, so callers cannot
    /// rewrite history.
    /// </summary>
    /// <exception cref="KeyNotFoundException">No account with that id exists.</exception>
    public IReadOnlyList<Transaction> GetTransactions(Guid id)
    {
        GetAccount(id);
        return _transactions[id].ToArray();
    }

    /// <summary>
    /// Moves <paramref name="amount"/> from one account to another, recording
    /// a linked TransferOut/TransferIn pair. Every check runs before any
    /// balance changes, so a rejected transfer leaves both accounts and
    /// both ledgers untouched.
    /// </summary>
    /// <param name="idempotencyKey">
    /// Optional caller-supplied key: repeating a transfer with the same key
    /// returns the original result instead of moving the money twice.
    /// Reusing a key for a different transfer is rejected.
    /// </param>
    /// <exception cref="KeyNotFoundException">Either account id is unknown.</exception>
    /// <exception cref="ArgumentException">
    /// The accounts are the same, or the amount is not positive.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// An account is not active, the source balance is too low, or the
    /// idempotency key was already used for a different transfer.
    /// </exception>
    public Transfer Transfer(Guid fromAccountId, Guid toAccountId, decimal amount, string? idempotencyKey = null)
    {
        if (idempotencyKey is not null && _transfersByKey.TryGetValue(idempotencyKey, out var existing))
        {
            if (existing.FromAccountId != fromAccountId
                || existing.ToAccountId != toAccountId
                || existing.Amount != amount)
            {
                throw new InvalidOperationException("Idempotency key was already used for a different transfer.");
            }

            return existing;
        }

        if (fromAccountId == toAccountId)
        {
            throw new ArgumentException("Cannot transfer to the same account.", nameof(toAccountId));
        }

        if (amount <= 0)
        {
            throw new ArgumentException("Amount must be positive.", nameof(amount));
        }

        var from = GetAccount(fromAccountId);
        var to = GetAccount(toAccountId);

        if (!from.IsActive)
        {
            throw new InvalidOperationException($"Source account is {from.Status}; only active accounts accept operations.");
        }

        if (!to.IsActive)
        {
            throw new InvalidOperationException($"Destination account is {to.Status}; only active accounts accept operations.");
        }

        if (amount > from.Balance)
        {
            throw new InvalidOperationException("Insufficient funds.");
        }

        var transferId = Guid.NewGuid();
        from.Withdraw(amount);
        Record(from, TransactionKind.TransferOut, amount, to.Id, transferId);
        to.Deposit(amount);
        Record(to, TransactionKind.TransferIn, amount, from.Id, transferId);

        var transfer = new Transfer(transferId, fromAccountId, toAccountId, amount);
        if (idempotencyKey is not null)
        {
            _transfersByKey.Add(idempotencyKey, transfer);
        }

        return transfer;
    }

    private void Record(Account account, TransactionKind kind, decimal amount, Guid? relatedAccountId = null, Guid? transferId = null)
    {
        _transactions[account.Id].Add(new Transaction(account.Id, kind, amount, account.Balance, relatedAccountId, transferId));
    }
}
