using Facebook;
using Microsoft.Bot.Connector;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;

namespace contoso.Controllers
{
    public class FacebookController : ApiController
    {
        private static string domain = "https://jw-contoso.azurewebsites.net";
        //private static string domain = "http://localhost:3979";
        private static string RedirectUri = domain + "/api/facebook/{0}?ChannelId={1}&FromId={2}&ServiceUrl={3}&ToId={4}&Conversation={5}";
        private static string AppID = "237073960046883";
        private static string AppSecret = "d9c12221815fd2f7bcd742758b93b020";

        public static string GetLoginLink(Activity message, string Id)
        {
            FacebookClient facebookClient = new FacebookClient();
            return facebookClient.GetLoginUrl(new
            {
                client_id = AppID,
                //client_secret = AppSecret, ---Why was this ever needed/suggested!!!???
                redirect_uri = string.Format(RedirectUri, Id, message.ChannelId, message.From.Id, message.ServiceUrl, message.Recipient.Id, message.Conversation.Id),
                response_type = "code",
                scope = "email"
            }).AbsoluteUri;
        }


        public static async Task<Activity> LoginHandler(Activity message)
        {
            StateClient stateClient = message.GetStateClient();
            BotData userData = await stateClient.BotState.GetUserDataAsync(message.ChannelId, message.From.Id);
            string guid = Guid.NewGuid().ToString();
            userData.SetProperty<string>("LoginGUID", guid);
            await stateClient.BotState.SetUserDataAsync(message.ChannelId, message.From.Id, userData);
            return message.CreateReply(GetLoginLink(message, guid));

        }


        public async Task<HttpResponseMessage> Get(string id)
        {
            Dictionary<string, string> query = Request.GetQueryNameValuePairs().ToDictionary(x => x.Key, x => x.Value);

            FacebookClient facebookClient = new FacebookClient();
            dynamic result = facebookClient.Post("oauth/access_token", new
            {
                client_id = AppID,
                client_secret = AppSecret,
                redirect_uri = Request.RequestUri.AbsoluteUri,
                code = query["code"]
            });
            try
            {
                facebookClient.AccessToken = result.access_token;
                dynamic meResponse = facebookClient.Get("me?fields=first_name,last_name,id,email");

                SendLoginMessage(query, meResponse);

                HttpResponseMessage response = Request.CreateResponse(HttpStatusCode.Accepted);
                return response;
            }
            catch
            {
                HttpResponseMessage response = Request.CreateResponse(HttpStatusCode.Accepted);
                response.Content = new StringContent("Failed to login, please try again or contact the server admin");
                return response;
            }
        }


        private async Task SendLoginMessage(Dictionary<string, string> query, dynamic meResponse)
        {
            ConnectorClient connector = new ConnectorClient(new Uri(query["ServiceUrl"]));
            IMessageActivity response = Activity.CreateMessageActivity();
            response.From = new ChannelAccount(query["ToId"]);
            response.Recipient = new ChannelAccount(query["FromId"]);
            response.ChannelId = query["ChannelId"];
            response.Conversation = new ConversationAccount(null, query["Conversation"]);
            response.Text = string.Format("You are now signed in as {0} {1}", meResponse.first_name, meResponse.last_name);
            await connector.Conversations.SendToConversationAsync((Activity)response);
        }

    }
}
