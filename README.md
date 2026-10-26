# Banking API

A banking API in C#: accounts, transactions and transfers with a clean,
tested core.

> Status: accounts done; transactions and transfers next.

## Roadmap

- [x] Project scaffold (.NET 8 solution, CI, README)
- [x] Accounts (open, deposit, withdraw, balances)
- [ ] Transactions + transfers (history, validation, idempotency)
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

## Quick start

```sh
dotnet build
dotnet test
```

## License

MIT — see [LICENSE](LICENSE).
