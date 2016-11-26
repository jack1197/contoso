using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace contoso.DataModels
{
    public class LoginEvent
    {
        [JsonProperty(PropertyName = "id")]
        public string ID { get; set; }

        [JsonProperty(PropertyName = "guid")]
        public string GUID { get; set; }

        [JsonProperty(PropertyName = "facebookId")]
        public string FacebookId { get; set; }

        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "facebookToken")]
        public string FacebookToken { get; set; }

        [JsonProperty(PropertyName = "toId")]
        public string ToID { get; set; }

        [JsonProperty(PropertyName = "fromId")]
        public string FromID { get; set; }

        [JsonProperty(PropertyName = "serviceUrl")]
        public string ServiceUrl { get; set; }

        [JsonProperty(PropertyName = "channelId")]
        public string ChannelID { get; set; }

        [JsonProperty(PropertyName = "conversation")]
        public string ConversationID { get; set; }

    }
}