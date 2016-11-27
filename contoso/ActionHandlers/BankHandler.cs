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
            if (!LUISResult.parameters.ContainsKey("money"))
            {
                return message.CreateReply("To make a transaction, you must include an amount to send");
            }

            Account From = await AzureManager.AzureManagerInstance.GetAccountByNumber(userData.GetProperty<string>("AccountNumber"));
            Account To = await AzureManager.AzureManagerInstance.GetAccountByNumber(LUISResult.parameters["AccountNumber"]);
            double amount = double.Parse(ExtractNum.Replace(FindNum.Match(LUISResult.parameters["money"]).Value, ""));

            if (amount <= 0)
            {
                return message.CreateReply("Cannot send a non-positive amount of money");
            }
            if(To == null)
            {
                return message.CreateReply("Recipient account not found");
            }

            PendingTransaction transaction = new PendingTransaction
            {
                From = From,
                To = To,
                amount = amount
            };

            userData.SetProperty<string>("Pending", "Transaction");
            userData.SetProperty<object>("PendingTransaction", transaction);

            Activity response = message.CreateReply("Please confirm or cancel transaction:");
            response.Attachments = new List<Attachment> { TransactionConfirmationCard(transaction) };
            return response;
        }


        private static Attachment TransactionConfirmationCard(PendingTransaction transaction)
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
                Tax = "NZD 0.00",
                Vat = "NZD 0.00",
                Total = string.Format("NZD {0:F2}", transaction.amount),
                Buttons = buttons,
            }.ToAttachment();
        }


        public static async Task<Activity> HandleBalance(Activity message)
        {
            string AccountNumber = (await message.GetStateClient().BotState.GetUserDataAsync(message.ChannelId, message.From.Id)).GetProperty<string>("AccountNumber");
            Account account = await AzureManager.AzureManagerInstance.GetAccountByNumber(AccountNumber);
            return message.CreateReply(string.Format("You currently have ${0:F2} in account {1}", account.Balance, AccountNumberFormat(account.AccountNumber)));
        }
    }
}