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


        private AzureManager()
        {
            this.client = new MobileServiceClient("http://jw-contosodb.azurewebsites.net/", "78c8faa0-aac1-4e50-a296-c42cf484ca78");
            this.accountTable = this.client.GetTable<Account>();
            this.transactionTable = this.client.GetTable<Transaction>();
            this.loginEventTable = this.client.GetTable<LoginEvent>();
        }


        public async Task MakeAccount(Account account)
        {
            await this.accountTable.InsertAsync(account);
        }


        public async Task<Account> RetrieveAccountFromNumber(string AccountNumber)
        {
            return (await accountTable
                .Where(AccountItem => AccountItem.AccountNumber == AccountNumber)
                .ToListAsync())
                .FirstOrDefault();
        }


        public async Task<Account> RetrieveAccountFromFacebook(string FacebookId)
        {
            try
            {
                return (await accountTable
                    .Where(AccountItem => AccountItem.FacebookId == FacebookId)
                    .ToListAsync())
                    .FirstOrDefault();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
                System.Diagnostics.Debug.WriteLine(ex.Message);
                return null;
            }
        }


        public async Task<LoginEvent> RetrieveLoginEventFromGUID(string GUID)
        {
            try
            {
                return (await loginEventTable
                .Where(LoginEventItem => LoginEventItem.GUID == GUID)
                .ToListAsync())
                .FirstOrDefault();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
                System.Diagnostics.Debug.WriteLine(ex.Message);
                return null;
            }
        }


        public async Task RemoveLoginEvent(LoginEvent loginEvent)
        {
            await this.loginEventTable.DeleteAsync(loginEvent);
        }


        public async Task UpdateLoginEvent(LoginEvent loginEvent)
        {
            await this.loginEventTable.UpdateAsync(loginEvent);
        }


        public async Task NewLoginEvent(LoginEvent loginEvent)
        {
            await this.loginEventTable.InsertAsync(loginEvent);
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
