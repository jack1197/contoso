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

        //Get link to login from facebook
        public static string GetFacebookLoginLink(Activity message, string Id)
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


        //create message to allow user to login
        public static async Task<Activity> LoginHandler(Activity message)
        {
            StateClient stateClient = message.GetStateClient();
            BotData userData = await stateClient.BotState.GetUserDataAsync(message.ChannelId, message.From.Id);

            //check no login already in progress, if there is, cancel it
            string oldGUID = userData.GetProperty<string>("GUID");
            LoginEvent loginEvent = await AzureManager.AzureManagerInstance.GetLoginEventByGUID(oldGUID);
            if (loginEvent != null)
            {
                await AzureManager.AzureManagerInstance.DeleteLoginEvent(loginEvent);
            }

            //setup new login event
            string GUID = Guid.NewGuid().ToString();
            userData.SetProperty<string>("GUID", GUID);
            await stateClient.BotState.SetUserDataAsync(message.ChannelId, message.From.Id, userData);
            await StartAuth(GUID, message);

            //send login card
            Activity reply = message.CreateReply();
            reply.Attachments = new List<Attachment> { LoginCard(GetFacebookLoginLink(message, GUID), "Facebook") };
            return reply;
        }
        

        static private async Task StartAuth(string GUID, Activity activity)
        {
            LoginEvent loginEvent = new LoginEvent();
            loginEvent.GUID = GUID;

            //Properties for response message
            loginEvent.ChannelID = activity.ChannelId;
            loginEvent.ConversationID = activity.Conversation.Id;
            loginEvent.ToID = activity.Recipient.Id;
            loginEvent.FromID = activity.From.Id;
            loginEvent.ServiceUrl = activity.ServiceUrl;

            await AzureManager.AzureManagerInstance.CreateLoginEvent(loginEvent);
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


        public static async Task<Activity> LogoutHandler(Activity message)
        {
            //set state for logout confirmation
            StateClient stateClient = message.GetStateClient();
            BotData userData = await stateClient.BotState.GetUserDataAsync(message.ChannelId, message.From.Id);
            userData.SetProperty<string>("Pending", "Logout");
            await stateClient.BotState.SetUserDataAsync(message.ChannelId, message.From.Id, userData);

            //send confirmation card
            Activity response = message.CreateReply();
            response.Attachments = new List<Attachment> { LogoutConfirmationCard() };
            return response;
        }


        public static Attachment LogoutConfirmationCard()
        {
            return new HeroCard()
            {
                Title = "Please confirm logout",
                Buttons = new List<CardAction>
                {
                    new CardAction
                    {
                        Type = "imBack",
                        Title = "Confirm",
                        Value = "confirm"
                    },
                    new CardAction
                    {
                        Type = "imBack",
                        Title = "Cancel",
                        Value = "deny"
                    },
                }
            }.ToAttachment();
        }


        //handle logout confirmation response
        public static async Task<Activity> HandlePendingLogout(Activity message)
        {
            StateClient stateClient = message.GetStateClient();
            BotData userData = await stateClient.BotState.GetUserDataAsync(message.ChannelId, message.From.Id);

            if (message.Text.Trim().ToLower() == "confirm")
            {
                string GUID = userData.GetProperty<string>("GUID");
                string Name = (userData.GetProperty<string>("Name") ?? "");

                //clear login information from state
                await stateClient.BotState.DeleteStateForUserAsync(message.ChannelId, message.From.Id);

                //Try to tidy DB if applicible, should work, but no neccesary
                try
                {
                    Account account = await AzureManager.AzureManagerInstance.GetAccountByFacebookID(userData.GetProperty<string>("FacebookID"));
                    account.CurrentGUID = null;
                    await AzureManager.AzureManagerInstance.UpdateAccount(account);
                }
                catch { }//operation may not occur cleanly, and this is fine

                return message.CreateReply($"User {Name} logged out");
            }
            else
            {
                //if denied
                userData.SetProperty<string>("Pending", null);
                await stateClient.BotState.SetUserDataAsync(message.ChannelId, message.From.Id, userData);
                return message.CreateReply("Logout canceled");
            }
        }


        //setup bot state for logged-in account
        static private async Task FinishLogin(Activity message, LoginEvent loginEvent)
        {

            StateClient stateClient = message.GetStateClient();
            BotData userData = await stateClient.BotState.GetUserDataAsync(message.ChannelId, message.From.Id);
            
            Account account = await AccountFromLoginEvent(loginEvent);

            userData.SetProperty<bool>("Authorised", true);
            userData.SetProperty<string>("Name", loginEvent.Name);
            userData.SetProperty<string>("FacebookToken", loginEvent.FacebookToken);
            userData.SetProperty<string>("FacebookID", loginEvent.FacebookId);
            userData.SetProperty<string>("AccountNumber", account.AccountNumber);
            await stateClient.BotState.SetUserDataAsync(message.ChannelId, message.From.Id, userData);
            
            await AzureManager.AzureManagerInstance.DeleteLoginEvent(loginEvent);
        }


        //retrive account for certain loginevent
        private static async Task<Account> AccountFromLoginEvent(LoginEvent loginEvent)
        {
            Account account = await AzureManager.AzureManagerInstance.GetAccountByFacebookID(loginEvent.FacebookId);
            if (account == null)
            {
                //DEMONSTATION PURPOSES, production application would likely throw exception here
                return await AzureManager.AzureManagerInstance.CreateAccount(loginEvent.Name, loginEvent.FacebookId, loginEvent.GUID);
            }
            else
            {
                account.CurrentGUID = loginEvent.GUID;
                await AzureManager.AzureManagerInstance.UpdateAccount(account);
                return account;
            }
        }


        //check user in message is logged in
        static public async Task<bool> IsAuthorised(Activity message)
        {
            StateClient stateClient = message.GetStateClient();
            BotData userData = await stateClient.BotState.GetUserDataAsync(message.ChannelId, message.From.Id);

            if (userData.GetProperty<bool>("Authorised") == true)
            {
                //check no-one else has logged into account (changing its currentGUID)
                Account account = await AzureManager.AzureManagerInstance.GetAccountByGUID(userData.GetProperty<string>("GUID"));
                if (account == null)
                {
                    userData.SetProperty<bool>("Authorised", false);
                    await stateClient.BotState.SetUserDataAsync(message.ChannelId, message.From.Id, userData);
                    return false;
                }

                return true;
            }
            else
            {
                //check if login event has been created and confirmed
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