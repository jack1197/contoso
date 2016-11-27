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
                //figure out conversions, if one rate given, try the other one as first nzd, then usd.
                //ignoring destination/source for simplicity, will report rate both ways
                List<string> givenRates = LUISResult.parameters.Values.Union(new List<string>{ "NZD", "USD" }, StringComparer.OrdinalIgnoreCase).Take(2).ToList();
                //exact rates arent neccesarily inverses...
                double FirstToSecond = await ExchangeRateFromTo(givenRates[0], givenRates[1]);
                double SecondToFirst = await ExchangeRateFromTo(givenRates[1], givenRates[0]);
                string reply = string.Format("Exchange rate from {0} to {1} is {2:F4}, and rate from {1} to {0} is {3:F4}", 
                    givenRates[0].ToUpper(), givenRates[1].ToUpper(), FirstToSecond, SecondToFirst);
                return message.CreateReply(reply);
            }
            catch (KeyNotFoundException)
            {
                return message.CreateReply("Could not find exchange rate");
            }
        }


        private static async Task<double> ExchangeRateFromTo(string To, string From)
        {
            HttpClient client = new HttpClient();
            HttpResponseMessage response = await client.GetAsync(string.Format(APIURL, Uri.EscapeDataString(From)));
            ExchangeResponse ExchangeResponse = JsonConvert.DeserializeObject<ExchangeResponse>(await response.Content.ReadAsStringAsync());
            return ExchangeResponse.rates[To];
        }
    }
}