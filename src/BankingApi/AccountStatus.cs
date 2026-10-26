namespace BankingApi;

/// <summary>Status of a bank account.</summary>
public enum AccountStatus
{
    /// <summary>Normal operation: deposits and withdrawals allowed.</summary>
    Active,

    /// <summary>Temporarily blocked: deposits and withdrawals rejected until unfrozen.</summary>
    Frozen,

    /// <summary>Permanently closed: no further operations allowed.</summary>
    Closed,
}
