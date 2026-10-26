using BankingApi;

var bank = new Bank();
var asha = bank.OpenAccount("Asha", 100m);
var rahul = bank.OpenAccount("Rahul", 50m);

bank.Deposit(asha.Id, 25.50m);
bank.Withdraw(rahul.Id, 20m);

foreach (var account in bank.ListAccounts())
{
    Console.WriteLine($"{account.Owner}: {account.Balance} ({account.Status})");
}

var transfer = bank.Transfer(asha.Id, rahul.Id, 30m, "demo-transfer-1");
Console.WriteLine($"Transferred {transfer.Amount} from Asha to Rahul (id {transfer.Id}).");
Console.WriteLine("Asha's statement:");
foreach (var entry in bank.GetTransactions(asha.Id))
{
    Console.WriteLine($"  {entry.Kind} {entry.Amount} -> balance {entry.BalanceAfter}");
}
