using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace contoso.DataModels
{
    public class Transaction
    {
        [JsonProperty(PropertyName = "id")]
        public string ID { get; set; }

        [JsonProperty(PropertyName = "from")]
        public string From { get; set; }

        [JsonProperty(PropertyName = "to")]
        public string To { get; set; }

        [JsonProperty(PropertyName = "transactionDate")]
        public DateTime Date { get; set; }

        [JsonProperty(PropertyName = "amount")]
        public double Amount { get; set; }
    }
}