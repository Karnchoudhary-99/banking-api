using Xunit;

namespace BankingApi.Tests;

public class JsonBankStoreTests
{
    [Fact]
    public void RoundTripPreservesAccountsLedgersAndIdempotencyKeys()
    {
        var bank = new Bank();
        var asha = bank.OpenAccount("Asha", 100m);
        var rahul = bank.OpenAccount("Rahul", 50m);
        var frozen = bank.OpenAccount("Mira", 20m);
        bank.Freeze(frozen.Id);
        bank.Deposit(asha.Id, 25.50m);
        var transfer = bank.Transfer(asha.Id, rahul.Id, 30m, "order-42");

        var path = TempPath();
        try
        {
            JsonBankStore.Save(bank, path);
            var restored = JsonBankStore.Load(path);

            var restoredAsha = restored.GetAccount(asha.Id);
            var restoredRahul = restored.GetAccount(rahul.Id);
            var restoredFrozen = restored.GetAccount(frozen.Id);
            Assert.Equal("Asha", restoredAsha.Owner);
            Assert.Equal(95.50m, restoredAsha.Balance);
            Assert.Equal(80m, restoredRahul.Balance);
            Assert.Equal(AccountStatus.Frozen, restoredFrozen.Status);
            Assert.Equal(20m, restoredFrozen.Balance);

            var ashaEntries = restored.GetTransactions(asha.Id);
            Assert.Equal(2, ashaEntries.Count);
            Assert.Equal(TransactionKind.Deposit, ashaEntries[0].Kind);
            Assert.Equal(125.50m, ashaEntries[0].BalanceAfter);
            Assert.Equal(TransactionKind.TransferOut, ashaEntries[1].Kind);
            Assert.Equal(95.50m, ashaEntries[1].BalanceAfter);
            Assert.Equal(rahul.Id, ashaEntries[1].RelatedAccountId);
            Assert.Equal(transfer.Id, ashaEntries[1].TransferId);

            var rahulEntries = restored.GetTransactions(rahul.Id);
            Assert.Single(rahulEntries);
            Assert.Equal(TransactionKind.TransferIn, rahulEntries[0].Kind);
            Assert.Equal(transfer.Id, rahulEntries[0].TransferId);

            // The idempotency key survived the restart: repeating the
            // transfer returns the original instead of moving money twice.
            var replayed = restored.Transfer(asha.Id, rahul.Id, 30m, "order-42");
            Assert.Equal(transfer.Id, replayed.Id);
            Assert.Equal(95.50m, restoredAsha.Balance);
            Assert.Equal(80m, restoredRahul.Balance);
            Assert.Equal(2, restored.GetTransactions(asha.Id).Count);

            Assert.Throws<InvalidOperationException>(() =>
                restored.Transfer(asha.Id, rahul.Id, 31m, "order-42"));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void LoadMissingFileThrowsFileNotFound()
    {
        Assert.Throws<FileNotFoundException>(() => JsonBankStore.Load(TempPath()));
    }

    [Fact]
    public void SaveCreatesParentDirectories()
    {
        var bank = new Bank();
        bank.OpenAccount("Asha", 10m);
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var path = Path.Combine(dir, "nested", "bank.json");
        try
        {
            JsonBankStore.Save(bank, path);
            Assert.True(File.Exists(path));
            Assert.Single(JsonBankStore.Load(path).ListAccounts());
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    private static string TempPath() => Path.Combine(Path.GetTempPath(), $"bank-{Guid.NewGuid()}.json");
}
