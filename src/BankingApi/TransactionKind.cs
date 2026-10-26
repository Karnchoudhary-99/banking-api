namespace BankingApi;

/// <summary>What a ledger entry represents.</summary>
public enum TransactionKind
{
    /// <summary>Money paid into an account.</summary>
    Deposit,

    /// <summary>Money taken out of an account.</summary>
    Withdrawal,

    /// <summary>Money sent out of an account as part of a transfer.</summary>
    TransferOut,

    /// <summary>Money received into an account as part of a transfer.</summary>
    TransferIn,
}
