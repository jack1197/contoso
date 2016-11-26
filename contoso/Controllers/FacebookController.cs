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
        private static string RedirectUri = "https://jw-contoso.azurewebsites.net/api/facebook/";
        private static string AppID = "237073960046883";
        private static string AppSecret = "d9c12221815fd2f7bcd742758b93b020";

        public static string GetLoginLink(string Id)
        {
            FacebookClient facebookClient = new FacebookClient();
            return facebookClient.GetLoginUrl(new
            {
                client_id = AppID,
               // client_secret = AppSecret,
                redirect_uri = RedirectUri + Id,
                response_type = "code",
                scope = "email"
            }).AbsoluteUri;
        }


        public static async Task<Activity> LoginHandler(Activity message)
        {
            return message.CreateReply(GetLoginLink(Guid.NewGuid().ToString()));
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
            facebookClient.AccessToken = result.access_token;
            dynamic me = facebookClient.Get("me?fields=first_name,last_name,id,email");
            HttpResponseMessage response = Request.CreateResponse(HttpStatusCode.Accepted);
            response.Content = new StringContent(JsonConvert.SerializeObject(new { result, me }));
            return response;
        }

    }
}
