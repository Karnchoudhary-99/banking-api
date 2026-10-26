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

Console.WriteLine("Transactions and transfers coming next.");
