using System.Text.Json;
using BankingApi;

// Banking API server: a thin ASP.NET Core adapter over ApiService.
// State lives in one JSON snapshot file (default bank.json, override
// with --data <path>): loaded on start, saved after every successful
// mutation, so a restart never loses balances or replays a transfer.
// All requests are serialized through one lock — this is a single-node
// MVP, not a concurrent banking backend, and the README says so.

var dataPath = "bank.json";
for (var i = 0; i < args.Length - 1; i++)
{
    if (args[i] == "--data")
    {
        dataPath = args[i + 1];
    }
}

var bank = File.Exists(dataPath) ? JsonBankStore.Load(dataPath) : new Bank();
var service = new ApiService(bank);
var gate = new object();

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

IResult Mutating(Func<ApiResult> action)
{
    lock (gate)
    {
        var result = action();
        if (result.StatusCode is >= 200 and < 300)
        {
            JsonBankStore.Save(bank, dataPath);
        }

        return Results.Content(result.Json, "application/json", statusCode: result.StatusCode);
    }
}

IResult Reading(Func<ApiResult> action)
{
    lock (gate)
    {
        var result = action();
        return Results.Content(result.Json, "application/json", statusCode: result.StatusCode);
    }
}

static string RequiredString(JsonElement body, string field)
{
    if (!body.TryGetProperty(field, out var value)
        || value.ValueKind != JsonValueKind.String
        || string.IsNullOrWhiteSpace(value.GetString()))
    {
        throw new ArgumentException($"Field '{field}' is required.");
    }

    return value.GetString()!;
}

static Guid RequiredGuid(JsonElement body, string field)
{
    var text = RequiredString(body, field);
    if (!Guid.TryParse(text, out var id))
    {
        throw new ArgumentException($"Field '{field}' must be a valid GUID.");
    }

    return id;
}

static decimal RequiredDecimal(JsonElement body, string field)
{
    if (!body.TryGetProperty(field, out var value)
        || value.ValueKind != JsonValueKind.Number
        || !value.TryGetDecimal(out var amount))
    {
        throw new ArgumentException($"Field '{field}' must be a number.");
    }

    return amount;
}

static decimal OptionalDecimal(JsonElement body, string field, decimal fallback) =>
    body.TryGetProperty(field, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetDecimal(out var amount)
        ? amount
        : fallback;

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/accounts", async (HttpRequest request) =>
{
    try
    {
        var body = await request.ReadFromJsonAsync<JsonElement>();
        var owner = RequiredString(body, "owner");
        var initialBalance = OptionalDecimal(body, "initialBalance", 0m);
        return Mutating(() => service.CreateAccount(owner, initialBalance));
    }
    catch (JsonException)
    {
        return Results.BadRequest(new { error = "Request body must be valid JSON." });
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapGet("/accounts", () => Reading(service.ListAccounts));

app.MapGet("/accounts/{id}", (Guid id) => Reading(() => service.GetAccount(id)));

app.MapPost("/accounts/{id}/deposit", async (Guid id, HttpRequest request) =>
{
    try
    {
        var body = await request.ReadFromJsonAsync<JsonElement>();
        var amount = RequiredDecimal(body, "amount");
        return Mutating(() => service.Deposit(id, amount));
    }
    catch (JsonException)
    {
        return Results.BadRequest(new { error = "Request body must be valid JSON." });
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/accounts/{id}/withdraw", async (Guid id, HttpRequest request) =>
{
    try
    {
        var body = await request.ReadFromJsonAsync<JsonElement>();
        var amount = RequiredDecimal(body, "amount");
        return Mutating(() => service.Withdraw(id, amount));
    }
    catch (JsonException)
    {
        return Results.BadRequest(new { error = "Request body must be valid JSON." });
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/accounts/{id}/freeze", (Guid id) => Mutating(() => service.Freeze(id)));

app.MapPost("/accounts/{id}/unfreeze", (Guid id) => Mutating(() => service.Unfreeze(id)));

app.MapPost("/accounts/{id}/close", (Guid id) => Mutating(() => service.Close(id)));

app.MapGet("/accounts/{id}/statement", (Guid id) => Reading(() => service.Statement(id)));

app.MapPost("/transfers", async (HttpRequest request) =>
{
    try
    {
        var body = await request.ReadFromJsonAsync<JsonElement>();
        var fromAccountId = RequiredGuid(body, "fromAccountId");
        var toAccountId = RequiredGuid(body, "toAccountId");
        var amount = RequiredDecimal(body, "amount");
        var idempotencyKey = request.Headers.TryGetValue("Idempotency-Key", out var header)
            && !string.IsNullOrWhiteSpace(header.ToString())
            ? header.ToString()
            : null;
        return Mutating(() => service.Transfer(fromAccountId, toAccountId, amount, idempotencyKey));
    }
    catch (JsonException)
    {
        return Results.BadRequest(new { error = "Request body must be valid JSON." });
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.Run();
