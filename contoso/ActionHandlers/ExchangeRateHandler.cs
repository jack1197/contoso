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
            private Dictionary<string, double> ratesInternal;
            public Dictionary<string, double> rates
            { 
                get
                {
                    return ratesInternal;
                }
                set
                {
                    //want case-insensitive stings
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
                List<string> givenCodes = RatesFromLUIS(LUISResult).Union(new List<string>{ "NZD", "USD" }, StringComparer.OrdinalIgnoreCase).Take(2).ToList();
                //exact rates arent neccesarily inverses...
                double FirstToSecond = await ExchangeRateFromTo(givenCodes[0], givenCodes[1]);
                double SecondToFirst = await ExchangeRateFromTo(givenCodes[1], givenCodes[0]);
                string reply = string.Format("Exchange rate from {0} to {1} is {2:F4} (Rate from {1} to {0} is {3:F4})", 
                    givenCodes[0].ToUpper(), givenCodes[1].ToUpper(), FirstToSecond, SecondToFirst);
                return message.CreateReply(reply);
            }
            catch (KeyNotFoundException)
            {
                return message.CreateReply("Could not find exchange rate");
            }
        }


        private static List<string> RatesFromLUIS(LUISHandler.LUISQueryResult LUISResult)
        {
            List<string> Rates = new List<string>();
            if (LUISResult.parameters.ContainsKey("SourceRate"))
            {
                Rates.Add(LUISResult.parameters["SourceRate"]);
            }
            if (LUISResult.parameters.ContainsKey("DestinationRate"))
            {
                Rates.Add(LUISResult.parameters["DestinationRate"]);
            }
            return Rates;
        }


        private static async Task<double> ExchangeRateFromTo(string From, string To)
        {
            HttpClient client = new HttpClient();
            HttpResponseMessage response = await client.GetAsync(string.Format(APIURL, Uri.EscapeDataString(From)));
            ExchangeResponse ExchangeResponse = JsonConvert.DeserializeObject<ExchangeResponse>(await response.Content.ReadAsStringAsync());
            return ExchangeResponse.rates[To];
        }
    }
}