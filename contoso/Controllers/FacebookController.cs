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
        private static string AppID = "237073960046883";
        private static string AppSecret = "d9c12221815fd2f7bcd742758b93b020";

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


        static private async Task<LoginEvent> StartLogin(string GUID, dynamic fbResult, dynamic meResult)
        {
            LoginEvent loginEvent = await AzureManager.AzureManagerInstance.GetLoginEventByGUID(GUID);
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




    }
}
