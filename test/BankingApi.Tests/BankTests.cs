using Xunit;

namespace BankingApi.Tests;

public class BankTests
{
    [Fact]
    public void OpenedAccountsGetUniqueIds()
    {
        var bank = new Bank();
        var a = bank.OpenAccount("Asha");
        var b = bank.OpenAccount("Rahul");
        Assert.NotEqual(a.Id, b.Id);
    }

    [Fact]
    public void GetReturnsTheOpenedAccount()
    {
        var bank = new Bank();
        var opened = bank.OpenAccount("Asha", 50m);
        Assert.Same(opened, bank.GetAccount(opened.Id));
    }

    [Fact]
    public void GetUnknownIdThrows()
    {
        var bank = new Bank();
        Assert.Throws<KeyNotFoundException>(() => bank.GetAccount(Guid.NewGuid()));
    }

    [Fact]
    public void ListContainsAllOpenedAccounts()
    {
        var bank = new Bank();
        var a = bank.OpenAccount("Asha");
        var b = bank.OpenAccount("Rahul");
        var listed = bank.ListAccounts();
        Assert.Equal(2, listed.Count);
        Assert.Contains(a, listed);
        Assert.Contains(b, listed);
    }

    [Fact]
    public void BankOperationsFlowThroughToTheAccount()
    {
        var bank = new Bank();
        var account = bank.OpenAccount("Asha", 100m);
        bank.Deposit(account.Id, 50m);
        bank.Withdraw(account.Id, 30m);
        Assert.Equal(120m, bank.GetAccount(account.Id).Balance);
    }

    [Fact]
    public void BankFreezeBlocksOperationsUntilUnfreeze()
    {
        var bank = new Bank();
        var account = bank.OpenAccount("Asha", 100m);
        bank.Freeze(account.Id);
        Assert.Throws<InvalidOperationException>(() => bank.Withdraw(account.Id, 10m));
        bank.Unfreeze(account.Id);
        bank.Withdraw(account.Id, 10m);
        Assert.Equal(90m, account.Balance);
    }

    [Fact]
    public void BankCloseMarksAccountClosedButKeepsItListed()
    {
        var bank = new Bank();
        var account = bank.OpenAccount("Asha", 0m);
        bank.CloseAccount(account.Id);
        Assert.Equal(AccountStatus.Closed, account.Status);
        Assert.Contains(account, bank.ListAccounts());
    }

    [Fact]
    public void OperationsOnUnknownIdThrowKeyNotFound()
    {
        var bank = new Bank();
        var missing = Guid.NewGuid();
        Assert.Throws<KeyNotFoundException>(() => bank.Deposit(missing, 10m));
        Assert.Throws<KeyNotFoundException>(() => bank.CloseAccount(missing));
    }
}
