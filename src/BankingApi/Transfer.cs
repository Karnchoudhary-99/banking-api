namespace BankingApi;

/// <summary>
/// The result of a successful transfer: the shared identifier linking its
/// two ledger entries, plus the accounts and amount involved.
/// </summary>
public sealed class Transfer
{
    public Transfer(Guid id, Guid fromAccountId, Guid toAccountId, decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("Amount must be positive.", nameof(amount));
        }

        Id = id;
        FromAccountId = fromAccountId;
        ToAccountId = toAccountId;
        Amount = amount;
        Timestamp = DateTimeOffset.UtcNow;
    }

    /// <summary>Shared identifier, also recorded on both ledger entries.</summary>
    public Guid Id { get; }

    /// <summary>Account the money left.</summary>
    public Guid FromAccountId { get; }

    /// <summary>Account the money arrived in.</summary>
    public Guid ToAccountId { get; }

    /// <summary>Amount moved, always positive.</summary>
    public decimal Amount { get; }

    /// <summary>When the transfer was applied (UTC).</summary>
    public DateTimeOffset Timestamp { get; }
}
