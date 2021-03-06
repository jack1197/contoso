﻿using contoso.ActionHandlers;
using contoso.DataModels;
using Microsoft.WindowsAzure.MobileServices;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace contoso
{
    public class AzureManager
    {
        private static AzureManager instance;
        private MobileServiceClient client;
        private IMobileServiceTable<Account> accountTable;
        private IMobileServiceTable<Transaction> transactionTable;
        private IMobileServiceTable<LoginEvent> loginEventTable;

        private static double LoginExpiryTime = 10; //minutes


        private AzureManager()
        {
            this.client = new MobileServiceClient("http://jw-contosodb.azurewebsites.net/", "78c8faa0-aac1-4e50-a296-c42cf484ca78");
            this.accountTable = this.client.GetTable<Account>();
            this.transactionTable = this.client.GetTable<Transaction>();
            this.loginEventTable = this.client.GetTable<LoginEvent>();
        }


        //make new account
        public async Task<Account> CreateAccount(string Name, string FacebookID, string GUID)
        {
            Random random = new Random((int)DateTime.Now.Ticks);
            Account account = new Account
            {
                Name = Name,
                Balance = 0,
                FacebookId = FacebookID,
                AccountNumber = $"541234{random.Next(1000000, 9999999)}000",
                CurrentGUID = GUID
            };
            await this.accountTable.InsertAsync(account);

            //DEMONSTATION PURPOSES: give money
            account = await GetAccountByNumber(account.AccountNumber);//must refresh
            Account master = await GetAccountByNumber(BankHandler.MasterAccount);
            await AzureManager.AzureManagerInstance.MakeTransaction(200, account, master);

            return account;
        }


        public async Task<Account> GetAccountByNumber(string AccountNumber)
        {
            return (await accountTable
                .Where(AccountItem => AccountItem.AccountNumber == AccountNumber)
                .ToListAsync())
                .FirstOrDefault();
        }


        public async Task<Account> GetAccountByGUID(string GUID)
        {
            return (await accountTable
                .Where(AccountItem => AccountItem.CurrentGUID == GUID)
                .ToListAsync())
                .FirstOrDefault();
        }


        public async Task<Account> GetAccountByFacebookID(string FacebookId)
        {
            return (await accountTable
                .Where(AccountItem => AccountItem.FacebookId == FacebookId)
                .ToListAsync())
                .FirstOrDefault();
        }

        public async Task UpdateAccount(Account account)
        {
            await this.accountTable.UpdateAsync(account);
        }


        public async Task<LoginEvent> GetLoginEventByGUID(string GUID)
        {
            LoginEvent loginEvent = (await loginEventTable
                .Where(LoginEventItem => LoginEventItem.GUID == GUID)
                .ToListAsync())
                .FirstOrDefault();

            //check timeout
            if (loginEvent != null && loginEvent.Expiry < DateTime.Now)
            {
                await DeleteLoginEvent(loginEvent);
                loginEvent = null;
            }

            return loginEvent;
        }


        public async Task DeleteLoginEvent(LoginEvent loginEvent)
        {
            await this.loginEventTable.DeleteAsync(loginEvent);
        }


        public async Task UpdateLoginEvent(LoginEvent loginEvent)
        {
            await this.loginEventTable.UpdateAsync(loginEvent);
        }


        public async Task CreateLoginEvent(LoginEvent loginEvent)
        {
            loginEvent.Expiry = DateTime.Now.AddMinutes(LoginExpiryTime);//set timeout
            await this.loginEventTable.InsertAsync(loginEvent);
        }


        public async Task<List<Transaction>> GetTransactionsByAccountNumber(string AccountNumber)
        {
            return (await transactionTable
                .Where(TransactionItem => (TransactionItem.From == AccountNumber || TransactionItem.To == AccountNumber) 
                                            && TransactionItem.Date > DateTime.Now.AddMonths(-1))//from last month
                .OrderByDescending(TransactionItem => TransactionItem.Date)
                .ToListAsync());
        }


        public async Task MakeTransaction(double amount,  Account To, Account From)
        {
            try
            {
                Transaction transaction = new Transaction
                {
                    From = From.AccountNumber,
                    To = To.AccountNumber,
                    Amount = amount,
                    Date = DateTime.Now,
                };
           
                //dont need to update balances if sending money to self
                if (From.AccountNumber != To.AccountNumber)
                {
                    From.Balance -= amount;
                    To.Balance += amount;
                }

                await this.transactionTable.InsertAsync(transaction);
                await this.accountTable.UpdateAsync(From);
                await this.accountTable.UpdateAsync(To);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }


        public MobileServiceClient AzureClient
        {
            get { return client;  }
        }

        
        public static AzureManager AzureManagerInstance
        {
            get
            {
                if(instance == null)
                {
                    instance = new AzureManager();
                }
                return instance;
            }
        }
    }
}
