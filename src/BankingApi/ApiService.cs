using System.Text.Json;
using System.Text.Json.Serialization;

namespace BankingApi;

/// <summary>One HTTP-style result: a status code plus a JSON body.</summary>
public sealed record ApiResult(int StatusCode, string Json);

/// <summary>
/// The HTTP API as pure logic: every operation on a <see cref="Bank"/>
/// mapped to a status code and a JSON payload, with domain exceptions
/// translated to HTTP semantics (unknown account 404, invalid input 400,
/// rejected state change such as insufficient funds or a reused
/// idempotency key 409). The ASP.NET server is a thin adapter over this
/// class, so the whole API is unit-testable without sockets — the same
/// pattern the recommender API used for its pure handle() routing.
/// </summary>
public sealed class ApiService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly Bank _bank;

    public ApiService(Bank bank)
    {
        _bank = bank;
    }

    /// <summary>Opens an account. 201 with the account on success.</summary>
    public ApiResult CreateAccount(string owner, decimal initialBalance = 0) =>
        Run(201, () => AccountJson(_bank.OpenAccount(owner, initialBalance)));

    /// <summary>All accounts, ordered by owner then id. 200.</summary>
    public ApiResult ListAccounts() =>
        Run(200, () => JsonSerializer.Serialize(
            _bank.ListAccounts()
                .OrderBy(a => a.Owner, StringComparer.Ordinal)
                .ThenBy(a => a.Id)
                .Select(ToDto),
            JsonOptions));

    /// <summary>One account by id. 200, or 404 when unknown.</summary>
    public ApiResult GetAccount(Guid id) =>
        Run(200, () => AccountJson(_bank.GetAccount(id)));

    /// <summary>Deposits and returns the updated account. 200.</summary>
    public ApiResult Deposit(Guid id, decimal amount) =>
        Run(200, () =>
        {
            _bank.Deposit(id, amount);
            return AccountJson(_bank.GetAccount(id));
        });

    /// <summary>Withdraws and returns the updated account. 200.</summary>
    public ApiResult Withdraw(Guid id, decimal amount) =>
        Run(200, () =>
        {
            _bank.Withdraw(id, amount);
            return AccountJson(_bank.GetAccount(id));
        });

    /// <summary>Freezes an account and returns it. 200.</summary>
    public ApiResult Freeze(Guid id) =>
        Run(200, () =>
        {
            _bank.Freeze(id);
            return AccountJson(_bank.GetAccount(id));
        });

    /// <summary>Unfreezes an account and returns it. 200.</summary>
    public ApiResult Unfreeze(Guid id) =>
        Run(200, () =>
        {
            _bank.Unfreeze(id);
            return AccountJson(_bank.GetAccount(id));
        });

    /// <summary>Closes an account and returns it. 200.</summary>
    public ApiResult Close(Guid id) =>
        Run(200, () =>
        {
            _bank.CloseAccount(id);
            return AccountJson(_bank.GetAccount(id));
        });

    /// <summary>
    /// Transfers between accounts. 200 with the transfer; repeating a
    /// transfer with the same idempotency key returns the original
    /// transfer (still 200), never a second movement of money.
    /// </summary>
    public ApiResult Transfer(Guid fromAccountId, Guid toAccountId, decimal amount, string? idempotencyKey = null) =>
        Run(200, () => TransferJson(_bank.Transfer(fromAccountId, toAccountId, amount, idempotencyKey)));

    /// <summary>The account's ledger, oldest first. 200, or 404 when unknown.</summary>
    public ApiResult Statement(Guid id) =>
        Run(200, () => JsonSerializer.Serialize(_bank.GetTransactions(id).Select(ToDto), JsonOptions));

    private ApiResult Run(int successStatus, Func<string> produce)
    {
        try
        {
            return new ApiResult(successStatus, produce());
        }
        catch (KeyNotFoundException ex)
        {
            return Error(404, ex.Message);
        }
        catch (ArgumentException ex)
        {
            return Error(400, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Error(409, ex.Message);
        }
    }

    private static ApiResult Error(int status, string message) =>
        new(status, JsonSerializer.Serialize(new { error = message }, JsonOptions));

    private static string AccountJson(Account account) =>
        JsonSerializer.Serialize(ToDto(account), JsonOptions);

    private static string TransferJson(Transfer transfer) =>
        JsonSerializer.Serialize(ToDto(transfer), JsonOptions);

    private static AccountDto ToDto(Account account) =>
        new(account.Id, account.Owner, account.Balance, account.Status);

    private static TransactionDto ToDto(Transaction entry) =>
        new(entry.Id, entry.AccountId, entry.Kind, entry.Amount, entry.BalanceAfter,
            entry.Timestamp, entry.RelatedAccountId, entry.TransferId);

    private static TransferDto ToDto(Transfer transfer) =>
        new(transfer.Id, transfer.FromAccountId, transfer.ToAccountId, transfer.Amount, transfer.Timestamp);

    private sealed record AccountDto(Guid Id, string Owner, decimal Balance, AccountStatus Status);

    private sealed record TransactionDto(
        Guid Id,
        Guid AccountId,
        TransactionKind Kind,
        decimal Amount,
        decimal BalanceAfter,
        DateTimeOffset Timestamp,
        Guid? RelatedAccountId,
        Guid? TransferId);

    private sealed record TransferDto(Guid Id, Guid FromAccountId, Guid ToAccountId, decimal Amount, DateTimeOffset Timestamp);
}
