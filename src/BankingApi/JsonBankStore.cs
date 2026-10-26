using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BankingApi;

/// <summary>
/// Persists a whole <see cref="Bank"/> — accounts, per-account ledgers and
/// idempotency keys — to a single JSON file, and rebuilds it exactly.
/// The server loads this file on start and saves after every mutation, so
/// a restart never loses balances or replays a transfer. File format
/// version 1; a snapshot with another version is rejected rather than
/// guessed at.
/// </summary>
public static class JsonBankStore
{
    /// <summary>Writes the bank's full state to <paramref name="path"/> (creating parent directories).</summary>
    public static void Save(Bank bank, string path)
    {
        var accounts = new JsonArray();
        var transactions = new JsonObject();
        foreach (var account in bank.ListAccounts().OrderBy(a => a.Id))
        {
            accounts.Add(new JsonObject
            {
                ["id"] = account.Id.ToString(),
                ["owner"] = account.Owner,
                ["balance"] = JsonValue.Create(account.Balance),
                ["status"] = account.Status.ToString(),
            });

            var entries = new JsonArray();
            foreach (var entry in bank.GetTransactions(account.Id))
            {
                var entryObject = new JsonObject
                {
                    ["id"] = entry.Id.ToString(),
                    ["accountId"] = entry.AccountId.ToString(),
                    ["kind"] = entry.Kind.ToString(),
                    ["amount"] = JsonValue.Create(entry.Amount),
                    ["balanceAfter"] = JsonValue.Create(entry.BalanceAfter),
                    ["timestamp"] = entry.Timestamp.ToString("O"),
                };
                if (entry.RelatedAccountId is not null)
                {
                    entryObject["relatedAccountId"] = entry.RelatedAccountId.Value.ToString();
                }

                if (entry.TransferId is not null)
                {
                    entryObject["transferId"] = entry.TransferId.Value.ToString();
                }

                entries.Add(entryObject);
            }

            transactions[account.Id.ToString()] = entries;
        }

        var keys = new JsonArray();
        foreach (var (key, transfer) in bank.IdempotencyTransfers().OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            keys.Add(new JsonObject
            {
                ["key"] = key,
                ["transfer"] = new JsonObject
                {
                    ["id"] = transfer.Id.ToString(),
                    ["fromAccountId"] = transfer.FromAccountId.ToString(),
                    ["toAccountId"] = transfer.ToAccountId.ToString(),
                    ["amount"] = JsonValue.Create(transfer.Amount),
                    ["timestamp"] = transfer.Timestamp.ToString("O"),
                },
            });
        }

        var root = new JsonObject
        {
            ["version"] = 1,
            ["accounts"] = accounts,
            ["transactions"] = transactions,
            ["idempotencyKeys"] = keys,
        };

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>
    /// Rebuilds a bank from a snapshot file.
    /// </summary>
    /// <exception cref="FileNotFoundException">No snapshot file exists at <paramref name="path"/>.</exception>
    /// <exception cref="InvalidDataException">The snapshot version is unsupported.</exception>
    public static Bank Load(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        if (!root.TryGetProperty("version", out var version) || version.GetInt32() != 1)
        {
            throw new InvalidDataException("Unsupported bank snapshot version.");
        }

        var bank = new Bank();
        var ledgers = root.TryGetProperty("transactions", out var transactionsElement)
            ? transactionsElement
            : default;

        foreach (var accountElement in root.GetProperty("accounts").EnumerateArray())
        {
            var id = accountElement.GetProperty("id").GetGuid();
            var account = Account.Restore(
                id,
                accountElement.GetProperty("owner").GetString()!,
                accountElement.GetProperty("balance").GetDecimal(),
                Enum.Parse<AccountStatus>(accountElement.GetProperty("status").GetString()!, ignoreCase: false));

            var entries = new List<Transaction>();
            if (ledgers.ValueKind == JsonValueKind.Object
                && ledgers.TryGetProperty(id.ToString(), out var ledger))
            {
                foreach (var entryElement in ledger.EnumerateArray())
                {
                    entries.Add(Transaction.Restore(
                        entryElement.GetProperty("id").GetGuid(),
                        entryElement.GetProperty("accountId").GetGuid(),
                        Enum.Parse<TransactionKind>(entryElement.GetProperty("kind").GetString()!, ignoreCase: false),
                        entryElement.GetProperty("amount").GetDecimal(),
                        entryElement.GetProperty("balanceAfter").GetDecimal(),
                        DateTimeOffset.Parse(entryElement.GetProperty("timestamp").GetString()!, null, DateTimeStyles.RoundtripKind),
                        entryElement.TryGetProperty("relatedAccountId", out var related) ? related.GetGuid() : null,
                        entryElement.TryGetProperty("transferId", out var transferId) ? transferId.GetGuid() : null));
                }
            }

            bank.RestoreAccount(account, entries);
        }

        if (root.TryGetProperty("idempotencyKeys", out var keys))
        {
            foreach (var keyElement in keys.EnumerateArray())
            {
                var transferElement = keyElement.GetProperty("transfer");
                bank.RestoreIdempotencyKey(
                    keyElement.GetProperty("key").GetString()!,
                    Transfer.Restore(
                        transferElement.GetProperty("id").GetGuid(),
                        transferElement.GetProperty("fromAccountId").GetGuid(),
                        transferElement.GetProperty("toAccountId").GetGuid(),
                        transferElement.GetProperty("amount").GetDecimal(),
                        DateTimeOffset.Parse(transferElement.GetProperty("timestamp").GetString()!, null, DateTimeStyles.RoundtripKind)));
            }
        }

        return bank;
    }
}
