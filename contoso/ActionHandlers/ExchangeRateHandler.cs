using contoso.LUIS;
using Microsoft.Bot.Connector;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;

namespace contoso.ActionHandlers
{
    public class ExchangeRateHandler
    {
        static string APIURL = "http://api.fixer.io/latest?base={0}";

        public class ExchangeResponse
        {
            public string @base { get; set; }
            public string date { get; set; }
            //want case-insensitive stings
            private Dictionary<string, double> ratesInternal;
            public Dictionary<string, double> rates
            { 
                get
                {
                    return ratesInternal;
                }
                set
                {
                    ratesInternal = new Dictionary<string, double>(value, StringComparer.OrdinalIgnoreCase);
                }
            }
        }   


        public static async Task<Activity> HandleExchangeRateMessage(Activity message, LUISHandler.LUISQueryResult LUISResult)
        {
            try
            {
                if (!LUISResult.parameters.ContainsKey("SourceRate") || LUISResult.parameters["SourceRate"] == LUISResult.parameters["DestinationRate"])
                {
                    return await ExchangeRateReply(message, LUISResult.parameters["DestinationRate"]);
                }
                else
                {
                    return await ExchangeRateReply(message, LUISResult.parameters["DestinationRate"], LUISResult.parameters["SourceRate"]);
                }
            }
            catch (KeyNotFoundException)
            {
                return message.CreateReply("Could not find exchange rate");
            }
        }


        private static async Task<Activity> ExchangeRateReply(Activity message, string destinationRate, string sourceRate = "NZD")
        {
            HttpClient client = new HttpClient();
            HttpResponseMessage response = await client.GetAsync(string.Format(APIURL, Uri.EscapeDataString(sourceRate)));
            ExchangeResponse ExchangeResponse = JsonConvert.DeserializeObject<ExchangeResponse>(await response.Content.ReadAsStringAsync());
            Activity reply = message.CreateReply(string.Format("Current exchange rate from {0} to {1} is: {2:f4}", sourceRate.ToUpper(), destinationRate.ToUpper(), ExchangeResponse.rates[destinationRate]));
            return reply;
        }
    }
}