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

    /// <summary>Adds <paramref name="amount"/> to the balance.</summary>
    /// <exception cref="InvalidOperationException">The account is not active.</exception>
    /// <exception cref="ArgumentException"><paramref name="amount"/> is not positive.</exception>
    public void Deposit(decimal amount)
    {
        RequireActive();
        RequirePositive(amount, nameof(amount));
        Balance += amount;
    }

    /// <summary>Subtracts <paramref name="amount"/> from the balance.</summary>
    /// <exception cref="InvalidOperationException">
    /// The account is not active, or the balance is too low.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="amount"/> is not positive.</exception>
    public void Withdraw(decimal amount)
    {
        RequireActive();
        RequirePositive(amount, nameof(amount));
        if (amount > Balance)
        {
            throw new InvalidOperationException("Insufficient funds.");
        }

        Balance -= amount;
    }

    /// <summary>Temporarily blocks deposits and withdrawals. Closed accounts cannot be frozen.</summary>
    public void Freeze()
    {
        if (Status == AccountStatus.Closed)
        {
            throw new InvalidOperationException("A closed account cannot be frozen.");
        }

        Status = AccountStatus.Frozen;
    }

    /// <summary>Restores a frozen account to active. Closed accounts cannot be unfrozen.</summary>
    public void Unfreeze()
    {
        if (Status == AccountStatus.Closed)
        {
            throw new InvalidOperationException("A closed account cannot be unfrozen.");
        }

        Status = AccountStatus.Active;
    }

    /// <summary>
    /// Permanently closes the account. Only possible with a zero balance, so
    /// money can never be stranded in a closed account.
    /// </summary>
    public void Close()
    {
        if (Balance != 0)
        {
            throw new InvalidOperationException("Cannot close an account with a non-zero balance.");
        }

        Status = AccountStatus.Closed;
    }

    private void RequireActive()
    {
        if (Status != AccountStatus.Active)
        {
            throw new InvalidOperationException($"Account is {Status}; only active accounts accept operations.");
        }
    }

    private static void RequirePositive(decimal amount, string name)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("Amount must be positive.", name);
        }
    }
}
