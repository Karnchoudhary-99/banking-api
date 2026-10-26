using Xunit;

namespace BankingApi.Tests;

public class TransferTests
{
    [Fact]
    public void TransferMovesMoneyAndLinksBothLedgerEntries()
    {
        var bank = new Bank();
        var from = bank.OpenAccount("Asha", 100m);
        var to = bank.OpenAccount("Rahul", 50m);

        var transfer = bank.Transfer(from.Id, to.Id, 30m);

        Assert.Equal(70m, from.Balance);
        Assert.Equal(80m, to.Balance);
        Assert.Equal(from.Id, transfer.FromAccountId);
        Assert.Equal(to.Id, transfer.ToAccountId);
        Assert.Equal(30m, transfer.Amount);

        var outEntry = Assert.Single(bank.GetTransactions(from.Id));
        var inEntry = Assert.Single(bank.GetTransactions(to.Id));
        Assert.Equal(TransactionKind.TransferOut, outEntry.Kind);
        Assert.Equal(30m, outEntry.Amount);
        Assert.Equal(70m, outEntry.BalanceAfter);
        Assert.Equal(to.Id, outEntry.RelatedAccountId);
        Assert.Equal(TransactionKind.TransferIn, inEntry.Kind);
        Assert.Equal(30m, inEntry.Amount);
        Assert.Equal(80m, inEntry.BalanceAfter);
        Assert.Equal(from.Id, inEntry.RelatedAccountId);
        Assert.Equal(transfer.Id, outEntry.TransferId);
        Assert.Equal(transfer.Id, inEntry.TransferId);
    }

    [Fact]
    public void TransferToSelfIsRejected()
    {
        var bank = new Bank();
        var account = bank.OpenAccount("Asha", 100m);
        Assert.Throws<ArgumentException>(() => bank.Transfer(account.Id, account.Id, 10m));
        Assert.Equal(100m, account.Balance);
    }

    [Fact]
    public void TransferWithUnknownAccountThrowsAndMovesNothing()
    {
        var bank = new Bank();
        var account = bank.OpenAccount("Asha", 100m);
        Assert.Throws<KeyNotFoundException>(() => bank.Transfer(account.Id, Guid.NewGuid(), 10m));
        Assert.Throws<KeyNotFoundException>(() => bank.Transfer(Guid.NewGuid(), account.Id, 10m));
        Assert.Equal(100m, account.Balance);
        Assert.Empty(bank.GetTransactions(account.Id));
    }

    [Fact]
    public void TransferRejectsNonPositiveAmounts()
    {
        var bank = new Bank();
        var from = bank.OpenAccount("Asha", 100m);
        var to = bank.OpenAccount("Rahul", 0m);
        Assert.Throws<ArgumentException>(() => bank.Transfer(from.Id, to.Id, 0m));
        Assert.Throws<ArgumentException>(() => bank.Transfer(from.Id, to.Id, -5m));
        Assert.Equal(100m, from.Balance);
        Assert.Equal(0m, to.Balance);
    }

    [Fact]
    public void TransferBeyondBalanceIsRejectedWithoutSideEffects()
    {
        var bank = new Bank();
        var from = bank.OpenAccount("Asha", 40m);
        var to = bank.OpenAccount("Rahul", 0m);
        Assert.Throws<InvalidOperationException>(() => bank.Transfer(from.Id, to.Id, 50m));
        Assert.Equal(40m, from.Balance);
        Assert.Equal(0m, to.Balance);
        Assert.Empty(bank.GetTransactions(from.Id));
        Assert.Empty(bank.GetTransactions(to.Id));
    }

    [Fact]
    public void TransferInvolvingAFrozenAccountIsRejectedWithoutSideEffects()
    {
        var bank = new Bank();
        var from = bank.OpenAccount("Asha", 100m);
        var to = bank.OpenAccount("Rahul", 0m);
        bank.Freeze(to.Id);
        Assert.Throws<InvalidOperationException>(() => bank.Transfer(from.Id, to.Id, 10m));
        bank.Unfreeze(to.Id);
        bank.Freeze(from.Id);
        Assert.Throws<InvalidOperationException>(() => bank.Transfer(from.Id, to.Id, 10m));
        Assert.Equal(100m, from.Balance);
        Assert.Equal(0m, to.Balance);
        Assert.Empty(bank.GetTransactions(from.Id));
        Assert.Empty(bank.GetTransactions(to.Id));
    }

    [Fact]
    public void RepeatingAnIdempotencyKeyReturnsTheOriginalTransferOnce()
    {
        var bank = new Bank();
        var from = bank.OpenAccount("Asha", 100m);
        var to = bank.OpenAccount("Rahul", 0m);

        var first = bank.Transfer(from.Id, to.Id, 25m, "order-42");
        var second = bank.Transfer(from.Id, to.Id, 25m, "order-42");

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(75m, from.Balance);
        Assert.Equal(25m, to.Balance);
        Assert.Single(bank.GetTransactions(from.Id));
        Assert.Single(bank.GetTransactions(to.Id));
    }

    [Fact]
    public void ReusingAnIdempotencyKeyForADifferentTransferIsRejected()
    {
        var bank = new Bank();
        var from = bank.OpenAccount("Asha", 100m);
        var to = bank.OpenAccount("Rahul", 0m);
        bank.Transfer(from.Id, to.Id, 25m, "order-42");
        Assert.Throws<InvalidOperationException>(() => bank.Transfer(from.Id, to.Id, 30m, "order-42"));
        Assert.Equal(75m, from.Balance);
        Assert.Equal(25m, to.Balance);
    }
}
