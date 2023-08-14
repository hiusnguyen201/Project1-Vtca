namespace GMA.App;
using GMA.BLL;
using GMA.Utility;
using Spectre.Console;
using GMA.Models;

public class OrderApp
{
    private static AccountBLL accountBLL = new AccountBLL();
    private static OrderBLL orderBLL = new OrderBLL();
    public static List<Game> cartGames = new List<Game>();

    public static void CartMenu()
    {
        if (cartGames.Count == 0 || cartGames == null)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("Your Cart is empty! ");
            Console.ResetColor();
            Console.ReadKey();
            MainMenuApp.MainMenu();
            return;
        }

        Console.Clear();
        double total = CalculateTotalPrice();
        DisplayCartTable(total);

        Console.Write("Your Choice: ");
        if (char.TryParse(Console.ReadLine(), out char choice))
        {
            switch (char.ToUpper(choice))
            {
                case 'B':
                    MainMenuApp.MainMenu();
                    break;

                case 'P':
                    if (AccountApp.accountLoggedIn != null)
                    {
                        bool gameOwned = false;
                        for (int i = 0; i < cartGames.Count; i++)
                        {
                            Game gameCart = cartGames[i];
                            gameOwned = AccountApp.accountLoggedIn.AccountOrders.Any(order => order.OrderGames.Any(game => game.GameId == gameCart.GameId));
                            if (gameOwned)
                            {
                                break;
                            }
                        }

                        if (!gameOwned)
                        {
                            CheckoutMenu(total);
                        }
                        else
                        {
                            Console.Write("Your account already owns some of the above games, so you can’t purchase them again ! ");
                            Console.ReadKey();
                        }
                    }
                    else
                    {
                        Console.Write("You need to Login to purchase! ");
                        Console.ReadKey();
                        AccountApp.LoginForm();
                    }
                    break;

                case 'S':
                    GameApp.GameStoreMenu();
                    break;

                case 'R':
                    ChoiceGameToRemoveInCart();
                    break;

                default:
                    Console.Write("Your choice is not exist! ");
                    Console.ReadKey();
                    break;
            }
        }
        else
        {
            Console.Write("Invalid choice! Try again ");
            Console.ReadKey();
        }
        CartMenu();
    }

    private static double CalculateTotalPrice()
    {
        double total = 0;
        foreach (Game game in cartGames)
        {
            total += game.Price;
        }
        return total;
    }

    private static void DisplayCartTable(double total)
    {
        var table = new Table()
            .AddColumn(new TableColumn(new Text("Game ID").Centered()))
            .AddColumn(new TableColumn(new Text("Game Name").Centered()))
            .AddColumn(new TableColumn(new Text("Price").Centered()));

        string priceString;
        foreach (Game game in cartGames)
        {
            priceString = (game.Price == 0) ? "Free" : HandlingString.FormatCurrencyVND(game.Price);
            if (AccountApp.accountLoggedIn != null && AccountApp.accountLoggedIn.AccountOrders != null)
            {
                bool gameOwned = AccountApp.accountLoggedIn.AccountOrders.Any(order => order.OrderGames.Any(gameOwned => gameOwned.GameId == game.GameId));
                if (gameOwned)
                {
                    priceString += " - (Purchased)";
                }
            }
            table.AddRow($"\n{game.GameId}\n", $"\n{game.Name}\n", $"\n{priceString}\n");
        }

        table.AddRow(" ", "[#ffffff]\n--- Total ---\n[/]", $"[#ffffff]\n{HandlingString.FormatCurrencyVND(total)}\n[/]");
        table.Width = 70;
        table.Caption("B: back | P: purchase | S: continue shopping | R: remove games");
        AnsiConsole.Write(table);
    }

    public static void CheckoutMenu(double total)
    {
        while (true)
        {
            Console.Clear();
            var table = new Table();
            table.AddColumn(new TableColumn(new Text("[Game Market Application]\nGroup 2 - PF1122 Version : 0.1\nCheckout Buy Game").Centered()));
            table.AddRow($"Username: {AccountApp.accountLoggedIn.Username}");
            table.AddRow($"Money: {HandlingString.FormatCurrencyVND(AccountApp.accountLoggedIn.Money)}");
            AnsiConsole.Write(table);

            Console.Write($"[Do you want to make payment with {HandlingString.FormatCurrencyVND(total)}] (Y/N): ");
            if (char.TryParse(Console.ReadLine(), out char choiceCheckout))
            {
                switch (char.ToUpper(choiceCheckout))
                {
                    case 'Y':
                        ProcessPayment(total);
                        break;

                    case 'N':
                        CartMenu();
                        break;

                    default:
                        Console.Write("Your choice is not exist! ");
                        Console.ReadKey();
                        break;
                }
            }
            else
            {
                Console.Write("Invalid choice! Try again ");
                Console.ReadKey();
            }
        }
    }

    public static void ProcessPayment(double total)
    {
        if (AccountApp.accountLoggedIn.Money >= total)
        {
            // Deduct money
            AccountApp.accountLoggedIn.Money -= total;

            // Create Order
            Order order = new Order(AccountApp.accountLoggedIn, total);
            order.Status = OrderStatus.PAID;
            int result = orderBLL.Save(order);

            if (result != 0)
            {
                // Create Order Details
                foreach (Game game in cartGames)
                {
                    int check = orderBLL.SaveDetails(result, game.GameId);
                    if (check == 0)
                    {
                        Console.Write($"Error! Create Order Details In OrderID: {result} - AccountID: {AccountApp.accountLoggedIn.AccountId}");
                        Console.ReadKey();
                        break;
                    }
                }

                // Update Money
                accountBLL.UpdateMoney(AccountApp.accountLoggedIn.AccountId, AccountApp.accountLoggedIn.Money);

                // Reset Cart
                cartGames = new List<Game>();

                Console.Write("Make Payment successfully! ");
                Console.ReadKey();

                //Print Invoice 
                InvoiceMenu(orderBLL.GetById(result), "CartMenu");
            }

            Console.Write("Failed to Create new Order! ");
            Console.ReadKey();
            CartMenu();
        }
        else
        {
            Console.Write("Your Balance is not enough to make payment! ");
            Console.ReadKey();
            CartMenu();
        }
    }

    public static void InvoiceMenu(Order order, string text)
    {
        Console.Clear();
        if (text == "CartMenu")
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("--- THANK YOU FOR YOUR PURCHASE! ---\n");
            Console.WriteLine("The Order below have been added to your Order history.\n");
            Console.ResetColor();
        }
        var tableOrderDetails = new Table()
        .AddColumn(new TableColumn(new Text("Game Name").Centered()))
        .AddColumn(new TableColumn(new Text("Price").Centered()));
        tableOrderDetails.Title("Order Details");
        for (int i = 0; i < order.OrderGames.Count; i++)
        {
            Game game = order.OrderGames[i];
            tableOrderDetails.AddRow($"{game.Name}", $"{HandlingString.FormatCurrencyVND(game.Price)}");
        }
        AnsiConsole.Write(tableOrderDetails);
        var tableAccountInfo = new Table()
        .AddColumn(new TableColumn(new Text("").Centered()))
        .AddColumn(new TableColumn(new Text("").Centered()));
        tableAccountInfo.HideHeaders();
        tableAccountInfo.AddRow($"Username: {AccountApp.accountLoggedIn.Username}\nOrder Id: {order.OrderId}\nDate: {order.OrderDate}", $"Total:   {HandlingString.FormatCurrencyVND(order.TotalPrice)}");
        AnsiConsole.Write(tableAccountInfo);
        Console.Write("\nPress Any key to Continue !");
        Console.ReadKey();
        if (text == "CartMenu")
        {
            MainMenuApp.MainMenu();
        }
        else if (text == "AccountMenu")
        {
            AccountApp.OrderHistory();
        }
    }

    public static void ChoiceGameToRemoveInCart()
    {
        while (true)
        {
            Console.Write("Enter Game ID you want to delete: ");
            string choice = Console.ReadLine();
            if (choice.ToUpper() == "B")
            {
                if(MainMenuApp.CheckYesNo())
                {
                    CartMenu();
                }
                else
                {
                    ChoiceGameToRemoveInCart();
                }
            }

            if (int.TryParse(choice.ToString(), out int choiceID))
            {
                Game game = cartGames.Find(game => game.GameId == choiceID);
                if (game != null)
                {
                    if(MainMenuApp.CheckYesNo())
                    {
                        cartGames.Remove(game);
                        CartMenu();
                    }
                    else
                    {
                        CartMenu();
                    }
                }
                else
                {
                    Console.Write("Game not found! ");
                    Console.ReadKey();
                }
            }
            else
            {
                Console.Write("Invalid choice! ");
                Console.ReadKey();
            }
            MainMenuApp.ClearCurrentConsoleLine();
            Console.SetCursorPosition(0, Console.CursorTop - 1);
            MainMenuApp.ClearCurrentConsoleLine();
        }
    }
}