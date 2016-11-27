using contoso.DataModels;
using Microsoft.Bot.Connector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using static contoso.LUIS.LUISHandler;

namespace contoso.ActionHandlers
{
    public class BankHandler
    {
        public static string MasterAccount
        {
            get { return "5413370000000000"; }
        }

        private static Regex TwoDigitSuffixFind = new Regex(@"(?<=[^\d])(?=\d{2})(?!\d{3})");
        private static Regex NonDecimalFind = new Regex(@"[^\d]+");
        private static Regex VerifyBankAccountNumber = new Regex(@"^\s*\d{2} *-? *\d{4} *-? *\d{7} *-? *\d{2,3}(?!\s*[^\s])");
        private static Regex FindNum = new Regex(@"(([\d, ]*\d *)(\. *\d[\d, ]*))|(([\d, ]*\d *)|(\. *\d[\d, ]*))");
        private static Regex ExtractNum = new Regex(@"[^\d.]");

        //removes all formatting from account number
        public static string AccountNumberStrip(string AccountNumber)
        {
            //ensures suffix is three digits, then removes all non-digits
            return NonDecimalFind.Replace(TwoDigitSuffixFind.Replace(AccountNumber, "0"), "");
        }


        //Formats an account number to ##-####-#######-###
        public static string AccountNumberFormat(string AccountNumber)
        {
            string Numeral = NonDecimalFind.Replace(AccountNumber, "");
            string WithDividers = $"{Numeral.Substring(0, 2)}-{Numeral.Substring(2, 4)}-{Numeral.Substring(6, 7)}-{Numeral.Substring(13)}";
            return TwoDigitSuffixFind.Replace(WithDividers, "0");
        }


        public static bool VerifyAccountNumber(string AccountNumber)
        {
            return VerifyBankAccountNumber.IsMatch(AccountNumber);
        }


        public static async Task<Activity> HandleHistory(Activity message)
        {
            string AccountNumber = (await message.GetStateClient().BotState.GetUserDataAsync(message.ChannelId, message.From.Id)).GetProperty<string>("AccountNumber");
            List<Transaction> transactions = await AzureManager.AzureManagerInstance.GetTransactionsByAccountNumber(AccountNumber);
            return null;
        }


        public class PendingTransaction
        {
            public Account From { get; set; }
            public Account To { get; set; }
            public double amount { get; set; }
        }


        public static async Task<Activity> HandleTransaction(Activity message, LUISQueryResult LUISResult)
        {
            StateClient stateClient = message.GetStateClient();
            BotData userData = await stateClient.BotState.GetUserDataAsync(message.ChannelId, message.From.Id);

            if (!(LUISResult.parameters.ContainsKey("AccountNumber") && VerifyAccountNumber(LUISResult.parameters["AccountNumber"])))
            {
                return message.CreateReply("To make a transaction, you must include a valid account number");
            }
            if (!LUISResult.parameters.ContainsKey("Money"))
            {
                return message.CreateReply("To make a transaction, you must include an amount to send");
            }

            Account From = await AzureManager.AzureManagerInstance.GetAccountByNumber(userData.GetProperty<string>("AccountNumber"));
            Account To = await AzureManager.AzureManagerInstance.GetAccountByNumber(AccountNumberStrip(LUISResult.parameters["AccountNumber"]));
            double amount = double.Parse(ExtractNum.Replace(FindNum.Match(LUISResult.parameters["Money"]).Value, ""));

            if (amount <= 0)
            {
                return message.CreateReply("Cannot send a non-positive amount of money");
            }
            if(To == null)
            {
                return message.CreateReply("Recipient account not found");
            }
            if(amount > From.Balance)
            {
                return message.CreateReply(string.Format("Insufficient funds $ {0:F2}, to pay $ {1:F2}", From.Balance, amount));
            }

            PendingTransaction transaction = new PendingTransaction
            {
                From = From,
                To = To,
                amount = amount
            };

            userData.SetProperty("Pending", "Transaction");
            userData.SetProperty("PendingTransaction", transaction);
            await stateClient.BotState.SetUserDataAsync(message.ChannelId, message.From.Id, userData);

            Activity response = message.CreateReply("Please confirm or cancel transaction:");
            response.Attachments = new List<Attachment> { TransactionConfirmationCard(transaction).ToAttachment() };
            return response;
        }


        public static async Task<Activity> CompletePendingTransaction(Activity message)
        {
            StateClient stateClient = message.GetStateClient();
            BotData userData = await stateClient.BotState.GetUserDataAsync(message.ChannelId, message.From.Id);
            PendingTransaction transaction = userData.GetProperty<PendingTransaction>("PendingTransaction");
            userData.SetProperty<PendingTransaction>("PendingTransaction", null);
            userData.SetProperty<string>("Pending", null);
            await stateClient.BotState.SetUserDataAsync(message.ChannelId, message.From.Id, userData);

            if (message.Text.ToLower().Trim() == "confirm")
            {
                //ensure nothing changed before confirmation
                if (transaction.amount > transaction.From.Balance)
                {
                    return message.CreateReply(string.Format("Insufficient funds $ {0:F2}, to pay $ {1:F2}", transaction.From.Balance, transaction.amount));
                }
                await AzureManager.AzureManagerInstance.MakeTransaction(transaction.amount, transaction.To, transaction.From);
            }
            else
            {
                return message.CreateReply("Transaction canceled");
            }

            Activity response = message.CreateReply("Transaction successful!");
            ReceiptCard receipt = TransactionConfirmationCard(transaction);
            receipt.Title = "Transaction completed!";
            receipt.Buttons = new List<CardAction>();
            response.Attachments = new List<Attachment> { receipt.ToAttachment() };
            return response;

        }


        private static ReceiptCard TransactionConfirmationCard(PendingTransaction transaction)
        {
            List<Fact> Facts = new List<Fact> {
                new Fact("From account:"),
                new Fact(AccountNumberFormat(transaction.From.AccountNumber), $"({transaction.From.Name})"),
                new Fact("To Account:"),
                new Fact(AccountNumberFormat(transaction.To.AccountNumber), $"({transaction.To.Name})"),
            };

            List<CardAction> buttons = new List<CardAction>
            {
                new CardAction
                {
                    Title = "Confirm",
                    Type = "imBack",
                    Value = "confirm"
                },
                new CardAction
                {
                    Title = "Cancel",
                    Type = "imBack",
                    Value = "deny"
                }
            };

            return new ReceiptCard
            {
                Title = "Transaction Confirmation",
                Facts = Facts,
                Items = new List<ReceiptItem>(),
                Total = string.Format("NZD {0:F2}", transaction.amount),
                Buttons = buttons,
            };
        }


        public static async Task<Activity> HandleBalance(Activity message)
        {
            string AccountNumber = (await message.GetStateClient().BotState.GetUserDataAsync(message.ChannelId, message.From.Id)).GetProperty<string>("AccountNumber");
            Account account = await AzureManager.AzureManagerInstance.GetAccountByNumber(AccountNumber);
            return message.CreateReply(string.Format("You currently have ${0:F2} in account {1}", account.Balance, AccountNumberFormat(account.AccountNumber)));
        }
    }
}