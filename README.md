# Banking API

A banking API in C#: accounts, transactions and transfers with a clean,
tested core.

> Status: complete — accounts, transactions/transfers, HTTP API and
> persistence all done, self-review passed.

## Roadmap

- [x] Project scaffold (.NET 8 solution, CI, README)
- [x] Accounts (open, deposit, withdraw, balances)
- [x] Transactions + transfers (history, validation, idempotency)
- [x] HTTP API + persistence
- [x] Self-review checklist, then public

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

## HTTP API

`BankingApi.Server` is a thin ASP.NET Core adapter over `ApiService`
(the same logic the unit tests exercise directly, no sockets needed):

```sh
dotnet run --project src/BankingApi.Server        # http://localhost:5000
dotnet run --project src/BankingApi.Server -- --data data/bank.json
```

| Method | Path | Body / notes |
| --- | --- | --- |
| GET | `/health` | `{"status":"ok"}` |
| POST | `/accounts` | `{"owner":"Asha","initialBalance":100}` → 201 |
| GET | `/accounts` | all accounts, by owner |
| GET | `/accounts/{id}` | one account |
| POST | `/accounts/{id}/deposit` | `{"amount":25.50}` |
| POST | `/accounts/{id}/withdraw` | `{"amount":20}` |
| POST | `/accounts/{id}/freeze` | |
| POST | `/accounts/{id}/unfreeze` | |
| POST | `/accounts/{id}/close` | zero balance required |
| GET | `/accounts/{id}/statement` | ledger, oldest first |
| POST | `/transfers` | `{"fromAccountId":"…","toAccountId":"…","amount":30}`; repeat with the same `Idempotency-Key` header to replay safely |

Status codes: 200 on reads and actions, 201 on account creation,
400 for invalid input (bad JSON, missing fields, non-positive amounts),
404 for unknown accounts, 409 for rejected state changes (insufficient
funds, frozen account, close with a balance, idempotency key reused for
a different transfer). Errors are `{"error":"…"}`.

`examples/api.sh` runs the whole tour against a live server — account
creation, deposit, an idempotent transfer sent twice (money moves once),
statement and listing.

## Persistence

State lives in one versioned JSON file (`bank.json` by default,
`--data <path>` to override): accounts with balances and statuses,
every per-account ledger entry, and idempotency keys. The server loads
it on start and saves after every successful mutation, so a restart
keeps balances and never replays a transfer. `JsonBankStore` in the core
library does the save/load; its round-trip is covered by tests,
including idempotency-key survival across a restart.

Honest limits: requests are serialized through a single lock and state
is one JSON file — this is a single-node MVP, not a concurrent banking
backend. There is no authentication on the API, so do not expose it to
an untrusted network.

## Quick start

```sh
dotnet build
dotnet test
```

## License

MIT — see [LICENSE](LICENSE).
