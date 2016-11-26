using contoso.DataModels;
using Facebook;
using Microsoft.Bot.Connector;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;

namespace contoso.Controllers
{
    public class FacebookController : ApiController
    {
        //private static string domain = "https://jw-contoso.azurewebsites.net";
        private static string domain = "http://localhost:3979";
        private static string RedirectUri = domain + "/api/facebook/{0}"; //?ChannelId={1}&FromId={2}&ServiceUrl={3}&ToId={4}&Conversation={5}";
        private static string AppID = "237073960046883";
        private static string AppSecret = "d9c12221815fd2f7bcd742758b93b020";

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

            LoginEvent loginEvent = await AzureManager.AzureManagerInstance.RetrieveLoginEventFromGUID(oldGUID);
            if (loginEvent != null)
            {
                await AzureManager.AzureManagerInstance.RemoveLoginEvent(loginEvent);
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
                Account account = await AzureManager.AzureManagerInstance.RetrieveAccountFromFacebook(userData.GetProperty<string>("FacebookID"));
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


        public async Task<HttpResponseMessage> Get(string id)
        {
            string code = Request.GetQueryNameValuePairs().First().Value;

            FacebookClient facebookClient = new FacebookClient();
            dynamic result = facebookClient.Post("oauth/access_token", new
            {
                client_id = AppID,
                client_secret = AppSecret,
                redirect_uri = Request.RequestUri.AbsoluteUri,
                code = code
            });
            try
            {
                facebookClient.AccessToken = result.access_token;
                dynamic meResponse = facebookClient.Get("me?fields=first_name,last_name,id,email");
                try
                {
                    string thing = JsonConvert.SerializeObject(meResponse);
                    System.Diagnostics.Debug.WriteLine(thing);
                }
                catch { }

                LoginEvent loginEvent = await StartLogin(id, result, meResponse);

                SendLoginMessage(loginEvent, meResponse);

                HttpResponseMessage response = Request.CreateResponse(HttpStatusCode.Accepted);
                response.Content = new StringContent("Log-in successful, you can now close the window");
                return response;
            }
            catch
            {
                HttpResponseMessage response = Request.CreateResponse(HttpStatusCode.Unauthorized);
                response.Content = new StringContent("Failed to login, please try again or contact the server admin");
                return response;
            }
        }


        static private async Task SendLoginMessage(LoginEvent loginEvent, dynamic meResponse)
        {
            ConnectorClient connector = new ConnectorClient(new Uri(loginEvent.ServiceUrl));
            IMessageActivity response = Activity.CreateMessageActivity();
            response.From = new ChannelAccount(loginEvent.ToID);
            response.Recipient = new ChannelAccount(loginEvent.FromID);
            response.ChannelId = loginEvent.ChannelID;
            response.Conversation = new ConversationAccount(null, loginEvent.ConversationID);
            response.Text = string.Format("You are now signed in as {0} {1}", meResponse.first_name, meResponse.last_name);
            await connector.Conversations.SendToConversationAsync((Activity)response);
        }


        static private async Task StartAuth(string GUID, Activity activity)
        {
            LoginEvent old = await AzureManager.AzureManagerInstance.RetrieveLoginEventFromGUID(GUID);
            if (old != null)
                await AzureManager.AzureManagerInstance.RemoveLoginEvent(old);
            LoginEvent loginEvent =  new LoginEvent();
            loginEvent.ChannelID = activity.ChannelId;
            loginEvent.ConversationID = activity.Conversation.Id;
            loginEvent.GUID = GUID;
            loginEvent.ToID = activity.Recipient.Id;
            loginEvent.FromID = activity.From.Id;
            loginEvent.ServiceUrl = activity.ServiceUrl;
            await AzureManager.AzureManagerInstance.NewLoginEvent(loginEvent);
        }


        static private async Task<LoginEvent> StartLogin(string GUID, dynamic fbResult, dynamic meResult)
        {
            LoginEvent loginEvent = await AzureManager.AzureManagerInstance.RetrieveLoginEventFromGUID(GUID);
            if (loginEvent == null || (loginEvent.FacebookToken ?? "") != "")
            {
                throw new InvalidOperationException("No current loginEvent found");
            }
            loginEvent.FacebookToken = fbResult.access_token;
            loginEvent.FacebookId = meResult.id;
            loginEvent.Name = meResult.first_name + " " + meResult.last_name;
            await AzureManager.AzureManagerInstance.UpdateLoginEvent(loginEvent);
            return loginEvent;
        }


        static private async Task FinishLogin(Activity message, LoginEvent loginEvent)
        {
            StateClient stateClient = message.GetStateClient();
            BotData userData = await stateClient.BotState.GetUserDataAsync(message.ChannelId, message.From.Id);
            userData.SetProperty<bool>("Authorised", true);
            userData.SetProperty<string>("Name", loginEvent.Name);
            userData.SetProperty<string>("FacebookToken", loginEvent.FacebookToken);
            userData.SetProperty<string>("FacebookID", loginEvent.FacebookId);
            await stateClient.BotState.SetUserDataAsync(message.ChannelId, message.From.Id, userData);

            Account account = await AzureManager.AzureManagerInstance.RetrieveAccountFromFacebook(loginEvent.FacebookId);
            Random random = new Random((int)DateTime.Now.Ticks);

            //simply for demonstation purposes!!!!!!!
            if (account == null)
            {
                account = new Account
                {
                    Name = loginEvent.Name,
                    Balance = 200.00,
                    FacebookId = loginEvent.FacebookId,
                    AccountNumber = $"54-1234-{random.Next(1000000,9999999)}-00"
                };
                await AzureManager.AzureManagerInstance.MakeAccount(account);
            }

            account.CurrentGUID = loginEvent.GUID;
            await AzureManager.AzureManagerInstance.RemoveLoginEvent(loginEvent);
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
                LoginEvent loginEvent = await AzureManager.AzureManagerInstance.RetrieveLoginEventFromGUID(userData.GetProperty<string>("GUID"));
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
