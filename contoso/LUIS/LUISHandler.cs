using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Web;
using contoso.DataModels;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace contoso.LUIS
{
    public class LUISHandler
    {
        static string APIURL = "https://api.projectoxford.ai/luis/v2.0/apps/e961442d-73e4-4ca3-91f0-b822dbbdc7b3?subscription-key=026a31cd2ad3411d8a757b6e85400c77&q={0}";

        public enum ResponseType
        {
            None,
            Logout,
            TransactionHistory,
            MakePayment,
            AccountBalance,
            ExchangeRate
        };

        public class LUISQueryResult
        {
            public ResponseType responseType { get; set; }
            public Dictionary<string, string> parameters { get; set; }
        }

        public static async Task<LUISQueryResult> HandleQuery(string query)
        {
            HttpClient client = new HttpClient();
            HttpResponseMessage response = await client.GetAsync(string.Format(APIURL, Uri.EscapeDataString(query)));
            LUISResponse LUISResponse = JsonConvert.DeserializeObject<LUISResponse>(await response.Content.ReadAsStringAsync());
            return new LUISQueryResult
            {
                responseType = TypeFromResponse(LUISResponse),
                parameters = ParamsFromResponse(LUISResponse)
            };
        }


        private static ResponseType TypeFromResponse(LUISResponse response)
        {
            switch (response.topScoringIntent.intent)
            {
                case "Logout":
                    return ResponseType.Logout;
                case "TransactionHistory":
                    return ResponseType.TransactionHistory;
                case "MakePayment":
                    return ResponseType.MakePayment;
                case "AccountBalance":
                    return ResponseType.AccountBalance;
                case "ExchangeRate":
                    return ResponseType.ExchangeRate;
                default:
                    return ResponseType.None;

            }
        }

        private static Dictionary<string, string> ParamsFromResponse(LUISResponse response)
        {
            Dictionary<string, string> paramaters = new Dictionary<string, string>();
            foreach (LUISResponse.Entity entity in response.entities)
            {
                paramaters[entity.type] = entity.entity;
            }
            return paramaters;
        }
    }
}