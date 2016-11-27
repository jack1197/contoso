using contoso.DataModels;
using Facebook;
using Microsoft.Bot.Connector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace contoso.ActionHandlers
{
    public class FacebookHandler
    {

#if DEBUG
        private static string domain = "http://localhost:3979";
#else
        private static string domain = "https://jw-contoso.azurewebsites.net";
#endif

        private static string RedirectUri = domain + "/api/facebook/{0}"; //?ChannelId={1}&FromId={2}&ServiceUrl={3}&ToId={4}&Conversation={5}";
        private static string AppID = "237073960046883";

        public static string GetLoginLink(Activity message, string Id)
        {
            FacebookClient facebookClient = new FacebookClient();
            return facebookClient.GetLoginUrl(new
            {
                client_id = AppID,
                redirect_uri = string.Format(RedirectUri, Id), //, message.ChannelId, message.From.Id, message.ServiceUrl, message.Recipient.Id, message.Conversation.Id),
                response_type = "code",
                scope = "email"
            }).AbsoluteUri;
        }


        public static async Task<Activity> LoginHandler(Activity message)
        {
            StateClient stateClient = message.GetStateClient();
            BotData userData = await stateClient.BotState.GetUserDataAsync(message.ChannelId, message.From.Id);
            string oldGUID = userData.GetProperty<string>("GUID");

            LoginEvent loginEvent = await AzureManager.AzureManagerInstance.GetLoginEventByGUID(oldGUID);
            if (loginEvent != null)
            {
                await AzureManager.AzureManagerInstance.DeleteLoginEvent(loginEvent);
            }

            string GUID = Guid.NewGuid().ToString();
            userData.SetProperty<string>("GUID", GUID);
            await stateClient.BotState.SetUserDataAsync(message.ChannelId, message.From.Id, userData);

            await StartAuth(GUID, message);

            Activity reply = message.CreateReply();
            reply.Attachments = new List<Attachment> { LoginCard(GetLoginLink(message, GUID), "Facebook") };
            return reply;
        }


        public static async Task<Activity> LogoutHandler(Activity message)
        {
            StateClient stateClient = message.GetStateClient();
            BotData userData = await stateClient.BotState.GetUserDataAsync(message.ChannelId, message.From.Id);
            string GUID = userData.GetProperty<string>("GUID");
            string Name = (userData.GetProperty<string>("Name") ?? "");
            await stateClient.BotState.DeleteStateForUserAsync(message.ChannelId, message.From.Id);

            //Try to tidy DB if applicible
            try
            {
                Account account = await AzureManager.AzureManagerInstance.GetAccountByFacebook(userData.GetProperty<string>("FacebookID"));
                account.CurrentGUID = null;
                await AzureManager.AzureManagerInstance.UpdateAccount(account);
            }
            catch { }

            return message.CreateReply($"User {Name} logged out");
        }


        private static Attachment LoginCard(string URL, string provider)
        {
            return new SigninCard
            {
                Buttons = new List<CardAction>
                {
                    new CardAction
                    {
                        Value = URL,
                        Type = "signin",
                        Title = $"Sign in with {provider}"
                    }
                },
                Text = "Hello, please sign in to continue:"
            }.ToAttachment();
        }

        static private async Task StartAuth(string GUID, Activity activity)
        {
            LoginEvent old = await AzureManager.AzureManagerInstance.GetLoginEventByGUID(GUID);
            if (old != null)
                await AzureManager.AzureManagerInstance.DeleteLoginEvent(old);
            LoginEvent loginEvent = new LoginEvent();
            loginEvent.ChannelID = activity.ChannelId;
            loginEvent.ConversationID = activity.Conversation.Id;
            loginEvent.GUID = GUID;
            loginEvent.ToID = activity.Recipient.Id;
            loginEvent.FromID = activity.From.Id;
            loginEvent.ServiceUrl = activity.ServiceUrl;
            await AzureManager.AzureManagerInstance.CreateLoginEvent(loginEvent);
        }


        static private async Task FinishLogin(Activity message, LoginEvent loginEvent)
        {

            Account account = await AzureManager.AzureManagerInstance.GetAccountByFacebook(loginEvent.FacebookId);
            Random random = new Random((int)DateTime.Now.Ticks);

            //simply for demonstation purposes!!!!!!!
            if (account == null)
            {
                account = new Account
                {
                    Name = loginEvent.Name,
                    Balance = 0,
                    FacebookId = loginEvent.FacebookId,
                    AccountNumber = $"541234{random.Next(1000000, 9999999)}000"
                };
                await AzureManager.AzureManagerInstance.MakeAccount(account);
                //update
                account = await AzureManager.AzureManagerInstance.GetAccountByNumber(account.AccountNumber);
                //give money
                Account master = await AzureManager.AzureManagerInstance.GetAccountByNumber(BankHandler.MasterAccount);
                await AzureManager.AzureManagerInstance.MakeTransaction(200, account, master);
            }

            StateClient stateClient = message.GetStateClient();
            BotData userData = await stateClient.BotState.GetUserDataAsync(message.ChannelId, message.From.Id);
            userData.SetProperty<bool>("Authorised", true);
            userData.SetProperty<string>("Name", loginEvent.Name);
            userData.SetProperty<string>("FacebookToken", loginEvent.FacebookToken);
            userData.SetProperty<string>("FacebookID", loginEvent.FacebookId);
            userData.SetProperty<string>("AccountNumber", account.AccountNumber);
            await stateClient.BotState.SetUserDataAsync(message.ChannelId, message.From.Id, userData);

            account.CurrentGUID = loginEvent.GUID;
            await AzureManager.AzureManagerInstance.DeleteLoginEvent(loginEvent);
        }


        static public async Task<bool> CheckAuth(Activity message)
        {
            StateClient stateClient = message.GetStateClient();
            BotData userData = await stateClient.BotState.GetUserDataAsync(message.ChannelId, message.From.Id);
            if (userData.GetProperty<bool>("Authorised") == true)
            {
                return true;
            }
            else
            {
                LoginEvent loginEvent = await AzureManager.AzureManagerInstance.GetLoginEventByGUID(userData.GetProperty<string>("GUID"));
                if (loginEvent?.FacebookId != null)
                {
                    await FinishLogin(message, loginEvent);
                    return true;
                }
            }
            return false;
        }
    }
}