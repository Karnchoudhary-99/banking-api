namespace BankingApi;

/// <summary>
/// A single bank account. Money is held as <see cref="decimal"/> (never
/// floating point) and every operation validates its inputs before mutating
/// state, so a rejected operation never changes the balance.
/// </summary>
public sealed class Account
{
    public Account(string owner, decimal initialBalance = 0)
    {
        if (string.IsNullOrWhiteSpace(owner))
        {
            throw new ArgumentException("Owner must not be empty.", nameof(owner));
        }

        if (initialBalance < 0)
        {
            throw new ArgumentException("Initial balance must not be negative.", nameof(initialBalance));
        }

        Id = Guid.NewGuid();
        Owner = owner;
        Balance = initialBalance;
        Status = AccountStatus.Active;
    }

    /// <summary>Unique identifier for this account.</summary>
    public Guid Id { get; }

    /// <summary>Name of the account holder.</summary>
    public string Owner { get; }

    /// <summary>Current balance, in the account's currency units.</summary>
    public decimal Balance { get; private set; }

    /// <summary>Current lifecycle status.</summary>
    public AccountStatus Status { get; private set; }

    /// <summary>True only while the account is open for deposits and withdrawals.</summary>
    public bool IsActive => Status == AccountStatus.Active;
}
