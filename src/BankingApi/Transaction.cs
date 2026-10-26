namespace BankingApi;

/// <summary>
/// One immutable ledger entry for a single account. A transfer produces two
/// entries (a <see cref="TransactionKind.TransferOut"/> and a
/// <see cref="TransactionKind.TransferIn"/>) sharing the same
/// <see cref="TransferId"/>, so the two sides of a transfer can be matched.
/// </summary>
public sealed class Transaction
{
    public Transaction(
        Guid accountId,
        TransactionKind kind,
        decimal amount,
        decimal balanceAfter,
        Guid? relatedAccountId = null,
        Guid? transferId = null)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("Amount must be positive.", nameof(amount));
        }

        if (balanceAfter < 0)
        {
            throw new ArgumentException("Balance after must not be negative.", nameof(balanceAfter));
        }

        Id = Guid.NewGuid();
        AccountId = accountId;
        Kind = kind;
        Amount = amount;
        BalanceAfter = balanceAfter;
        Timestamp = DateTimeOffset.UtcNow;
        RelatedAccountId = relatedAccountId;
        TransferId = transferId;
    }

    /// <summary>Unique identifier for this entry.</summary>
    public Guid Id { get; }

    /// <summary>Account this entry belongs to.</summary>
    public Guid AccountId { get; }

    /// <summary>What happened.</summary>
    public TransactionKind Kind { get; }

    /// <summary>Amount moved, always positive.</summary>
    public decimal Amount { get; }

    /// <summary>Account balance immediately after this entry was applied.</summary>
    public decimal BalanceAfter { get; }

    /// <summary>When the entry was recorded (UTC).</summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>
    /// The other account involved, for transfers; null for plain
    /// deposits and withdrawals.
    /// </summary>
    public Guid? RelatedAccountId { get; }

    /// <summary>
    /// Shared identifier for the two entries of one transfer; null for
    /// plain deposits and withdrawals.
    /// </summary>
    public Guid? TransferId { get; }
}
