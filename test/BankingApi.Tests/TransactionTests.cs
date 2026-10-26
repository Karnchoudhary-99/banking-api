using Xunit;

namespace BankingApi.Tests;

public class TransactionTests
{
    [Fact]
    public void DepositAndWithdrawalAreRecordedInOrderWithRunningBalances()
    {
        var bank = new Bank();
        var account = bank.OpenAccount("Asha", 100m);
        bank.Deposit(account.Id, 50m);
        bank.Withdraw(account.Id, 30m);

        var entries = bank.GetTransactions(account.Id);
        Assert.Equal(2, entries.Count);
        Assert.Equal(TransactionKind.Deposit, entries[0].Kind);
        Assert.Equal(50m, entries[0].Amount);
        Assert.Equal(150m, entries[0].BalanceAfter);
        Assert.Equal(TransactionKind.Withdrawal, entries[1].Kind);
        Assert.Equal(30m, entries[1].Amount);
        Assert.Equal(120m, entries[1].BalanceAfter);
        Assert.All(entries, e => Assert.Equal(account.Id, e.AccountId));
    }

    [Fact]
    public void NewAccountHasAnEmptyLedger()
    {
        var bank = new Bank();
        var account = bank.OpenAccount("Asha", 100m);
        Assert.Empty(bank.GetTransactions(account.Id));
    }

    [Fact]
    public void FailedOperationRecordsNothing()
    {
        var bank = new Bank();
        var account = bank.OpenAccount("Asha", 100m);
        bank.Freeze(account.Id);
        Assert.Throws<InvalidOperationException>(() => bank.Withdraw(account.Id, 10m));
        Assert.Empty(bank.GetTransactions(account.Id));
        Assert.Equal(100m, account.Balance);
    }

    [Fact]
    public void LedgerForUnknownAccountThrows()
    {
        var bank = new Bank();
        Assert.Throws<KeyNotFoundException>(() => bank.GetTransactions(Guid.NewGuid()));
    }

    [Fact]
    public void ReturnedLedgerIsACopy()
    {
        var bank = new Bank();
        var account = bank.OpenAccount("Asha", 100m);
        bank.Deposit(account.Id, 10m);
        var first = bank.GetTransactions(account.Id);
        bank.Deposit(account.Id, 10m);
        Assert.Single(first);
        Assert.Equal(2, bank.GetTransactions(account.Id).Count);
    }

    [Fact]
    public void DirectAccountOperationsAreNotInTheBankLedger()
    {
        var bank = new Bank();
        var account = bank.OpenAccount("Asha", 100m);
        account.Deposit(10m);
        Assert.Empty(bank.GetTransactions(account.Id));
    }
}
