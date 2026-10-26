using System.Text.Json;
using Xunit;

namespace BankingApi.Tests;

public class ApiServiceTests
{
    [Fact]
    public void CreateAndGetAccountReturn201And200WithPayload()
    {
        var service = new ApiService(new Bank());

        var created = service.CreateAccount("Asha", 100m);
        Assert.Equal(201, created.StatusCode);
        using var createdDoc = JsonDocument.Parse(created.Json);
        var id = createdDoc.RootElement.GetProperty("id").GetGuid();
        Assert.Equal("Asha", createdDoc.RootElement.GetProperty("owner").GetString());
        Assert.Equal(100m, createdDoc.RootElement.GetProperty("balance").GetDecimal());
        Assert.Equal("Active", createdDoc.RootElement.GetProperty("status").GetString());

        var fetched = service.GetAccount(id);
        Assert.Equal(200, fetched.StatusCode);
        Assert.Equal(id, JsonDocument.Parse(fetched.Json).RootElement.GetProperty("id").GetGuid());
    }

    [Fact]
    public void UnknownAccountReturns404WithErrorBody()
    {
        var service = new ApiService(new Bank());
        var result = service.GetAccount(Guid.NewGuid());
        Assert.Equal(404, result.StatusCode);
        Assert.Contains("No account", JsonDocument.Parse(result.Json).RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public void InvalidInputReturns400()
    {
        var service = new ApiService(new Bank());
        Assert.Equal(400, service.CreateAccount("  ").StatusCode);

        var account = service.CreateAccount("Asha", 10m);
        var id = JsonDocument.Parse(account.Json).RootElement.GetProperty("id").GetGuid();
        Assert.Equal(400, service.Deposit(id, -5m).StatusCode);
        Assert.Equal(400, service.Deposit(id, 0m).StatusCode);
    }

    [Fact]
    public void RejectedStateChangesReturn409()
    {
        var service = new ApiService(new Bank());
        var account = service.CreateAccount("Asha", 10m);
        var id = JsonDocument.Parse(account.Json).RootElement.GetProperty("id").GetGuid();

        Assert.Equal(409, service.Withdraw(id, 50m).StatusCode);

        Assert.Equal(200, service.Freeze(id).StatusCode);
        Assert.Equal(409, service.Deposit(id, 5m).StatusCode);
        Assert.Equal(409, service.Close(id).StatusCode);

        Assert.Equal(200, service.Unfreeze(id).StatusCode);
        Assert.Equal(200, service.Withdraw(id, 10m).StatusCode);
        Assert.Equal(200, service.Close(id).StatusCode);
        Assert.Equal(409, service.Freeze(id).StatusCode);
    }

    [Fact]
    public void TransferThroughApiIsIdempotentAndStatementListsEntries()
    {
        var service = new ApiService(new Bank());
        var asha = JsonDocument.Parse(service.CreateAccount("Asha", 100m).Json).RootElement;
        var rahul = JsonDocument.Parse(service.CreateAccount("Rahul", 50m).Json).RootElement;
        var ashaId = asha.GetProperty("id").GetGuid();
        var rahulId = rahul.GetProperty("id").GetGuid();

        var first = service.Transfer(ashaId, rahulId, 30m, "order-42");
        Assert.Equal(200, first.StatusCode);
        var transferId = JsonDocument.Parse(first.Json).RootElement.GetProperty("id").GetGuid();

        var replay = service.Transfer(ashaId, rahulId, 30m, "order-42");
        Assert.Equal(transferId, JsonDocument.Parse(replay.Json).RootElement.GetProperty("id").GetGuid());
        Assert.Equal(70m, JsonDocument.Parse(service.GetAccount(ashaId).Json).RootElement.GetProperty("balance").GetDecimal());

        Assert.Equal(409, service.Transfer(ashaId, rahulId, 31m, "order-42").StatusCode);
        Assert.Equal(400, service.Transfer(ashaId, ashaId, 10m).StatusCode);

        var statement = service.Statement(ashaId);
        Assert.Equal(200, statement.StatusCode);
        var entries = JsonDocument.Parse(statement.Json).RootElement;
        Assert.Equal(1, entries.GetArrayLength());
        Assert.Equal("TransferOut", entries[0].GetProperty("kind").GetString());
        Assert.Equal(transferId, entries[0].GetProperty("transferId").GetGuid());
        Assert.Equal(rahulId, entries[0].GetProperty("relatedAccountId").GetGuid());
    }

    [Fact]
    public void ListAccountsOrdersByOwner()
    {
        var service = new ApiService(new Bank());
        service.CreateAccount("Rahul", 1m);
        service.CreateAccount("Asha", 2m);

        var listed = JsonDocument.Parse(service.ListAccounts().Json).RootElement;
        Assert.Equal(2, listed.GetArrayLength());
        Assert.Equal("Asha", listed[0].GetProperty("owner").GetString());
        Assert.Equal("Rahul", listed[1].GetProperty("owner").GetString());
    }
}
