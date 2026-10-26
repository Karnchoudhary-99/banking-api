using Xunit;

namespace BankingApi.Tests;

public class AccountTests
{
    [Fact]
    public void NewAccountStartsActiveWithGivenBalance()
    {
        var account = new Account("Asha", 100m);
        Assert.Equal("Asha", account.Owner);
        Assert.Equal(100m, account.Balance);
        Assert.Equal(AccountStatus.Active, account.Status);
        Assert.True(account.IsActive);
        Assert.NotEqual(Guid.Empty, account.Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ConstructorRejectsBlankOwner(string owner)
    {
        Assert.Throws<ArgumentException>(() => new Account(owner));
    }

    [Fact]
    public void ConstructorRejectsNegativeInitialBalance()
    {
        Assert.Throws<ArgumentException>(() => new Account("Asha", -1m));
    }

    [Fact]
    public void DepositIncreasesBalance()
    {
        var account = new Account("Asha", 100m);
        account.Deposit(25.50m);
        Assert.Equal(125.50m, account.Balance);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.01)]
    [InlineData(-100)]
    public void DepositRejectsNonPositiveAmounts(decimal amount)
    {
        var account = new Account("Asha", 100m);
        Assert.Throws<ArgumentException>(() => account.Deposit(amount));
        Assert.Equal(100m, account.Balance);
    }

    [Fact]
    public void WithdrawDecreasesBalance()
    {
        var account = new Account("Asha", 100m);
        account.Withdraw(40m);
        Assert.Equal(60m, account.Balance);
    }

    [Fact]
    public void WithdrawFullBalanceLeavesZero()
    {
        var account = new Account("Asha", 100m);
        account.Withdraw(100m);
        Assert.Equal(0m, account.Balance);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void WithdrawRejectsNonPositiveAmounts(decimal amount)
    {
        var account = new Account("Asha", 100m);
        Assert.Throws<ArgumentException>(() => account.Withdraw(amount));
        Assert.Equal(100m, account.Balance);
    }

    [Fact]
    public void WithdrawBeyondBalanceIsRejectedAndBalanceUnchanged()
    {
        var account = new Account("Asha", 100m);
        Assert.Throws<InvalidOperationException>(() => account.Withdraw(100.01m));
        Assert.Equal(100m, account.Balance);
    }

    [Fact]
    public void FrozenAccountRejectsDepositsAndWithdrawals()
    {
        var account = new Account("Asha", 100m);
        account.Freeze();
        Assert.Equal(AccountStatus.Frozen, account.Status);
        Assert.False(account.IsActive);
        Assert.Throws<InvalidOperationException>(() => account.Deposit(10m));
        Assert.Throws<InvalidOperationException>(() => account.Withdraw(10m));
        Assert.Equal(100m, account.Balance);
    }

    [Fact]
    public void UnfreezeRestoresOperations()
    {
        var account = new Account("Asha", 100m);
        account.Freeze();
        account.Unfreeze();
        Assert.Equal(AccountStatus.Active, account.Status);
        account.Deposit(10m);
        Assert.Equal(110m, account.Balance);
    }

    [Fact]
    public void CloseRequiresZeroBalance()
    {
        var account = new Account("Asha", 100m);
        Assert.Throws<InvalidOperationException>(() => account.Close());
        Assert.Equal(AccountStatus.Active, account.Status);
    }

    [Fact]
    public void ClosedAccountRejectsAllOperations()
    {
        var account = new Account("Asha", 0m);
        account.Close();
        Assert.Equal(AccountStatus.Closed, account.Status);
        Assert.Throws<InvalidOperationException>(() => account.Deposit(10m));
        Assert.Throws<InvalidOperationException>(() => account.Withdraw(10m));
        Assert.Throws<InvalidOperationException>(() => account.Freeze());
        Assert.Throws<InvalidOperationException>(() => account.Unfreeze());
    }
}
