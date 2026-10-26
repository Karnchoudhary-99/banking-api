# Banking API

A banking API in C#: accounts, transactions and transfers with a clean,
tested core.

> Status: accounts and transactions/transfers done; HTTP API next.

## Roadmap

- [x] Project scaffold (.NET 8 solution, CI, README)
- [x] Accounts (open, deposit, withdraw, balances)
- [x] Transactions + transfers (history, validation, idempotency)
- [ ] HTTP API + persistence
- [ ] Self-review checklist, then public

## Accounts

```csharp
var bank = new Bank();
var account = bank.OpenAccount("Asha", 100m);
bank.Deposit(account.Id, 25.50m);
bank.Withdraw(account.Id, 20m);
bank.GetAccount(account.Id).Balance; // 105.50
```

Rules, all enforced by the domain model (not by callers):

- Money is `decimal`, never floating point.
- Deposits and withdrawals must be positive; a rejected operation never
  changes the balance.
- Withdrawals cannot exceed the balance (no overdraft in this core).
- Frozen accounts reject deposits and withdrawals until unfrozen.
- An account can only close with a zero balance, so money is never stranded
  in a closed account; closed accounts reject every further operation.
- Unknown account ids raise `KeyNotFoundException`.

## Transactions and transfers

Every Bank-mediated operation is recorded in a per-account ledger, oldest
first, with the running balance after each entry:

```csharp
var bank = new Bank();
var asha = bank.OpenAccount("Asha", 100m);
var rahul = bank.OpenAccount("Rahul", 50m);

var transfer = bank.Transfer(asha.Id, rahul.Id, 30m, "order-42");
// Asha: 70, Rahul: 80

foreach (var entry in bank.GetTransactions(asha.Id))
{
    Console.WriteLine($"{entry.Kind} {entry.Amount} -> {entry.BalanceAfter}");
}
// TransferOut 30 -> 70
```

Transfer rules, all checked before any balance changes, so a rejected
transfer leaves both accounts and both ledgers untouched:

- Source and destination must differ, and the amount must be positive.
- Both accounts must be active (frozen or closed accounts are rejected).
- The source balance must cover the amount (no overdraft in this core).
- A transfer records a linked pair: a `TransferOut` on the source and a
  `TransferIn` on the destination, sharing the same transfer id and naming
  each other as the related account.
- Repeating a transfer with the same idempotency key returns the original
  result instead of moving the money twice; reusing a key for a different
  transfer is rejected.
- A rejected operation records nothing. Note that the ledger only covers
  operations made through `Bank` — calling `Deposit`/`Withdraw` directly
  on an `Account` bypasses it.

## Quick start

```sh
dotnet build
dotnet test
```

## License

MIT — see [LICENSE](LICENSE).
