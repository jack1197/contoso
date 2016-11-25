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
            public Dictionary<string, double> rates { get; set; }
        }   

        static async Task<Activity> ExchangeRateReply(Activity message, string destinationRate, string sourceRate = "NZD")
        {
            HttpClient client = new HttpClient();
            HttpResponseMessage response = await client.GetAsync(string.Format(APIURL, Uri.EscapeDataString(sourceRate)));
            ExchangeResponse ExchangeResponse = JsonConvert.DeserializeObject<ExchangeResponse>(await response.Content.ReadAsStringAsync());
            Activity reply = message.CreateReply(string.Format("Current exchange rate from {0} to {1} is: {2:f4}", sourceRate, destinationRate, ExchangeResponse.rates[destinationRate]));
            return reply;
        }


        
    }
}