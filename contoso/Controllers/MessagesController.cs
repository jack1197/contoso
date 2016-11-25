﻿using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Description;
using Microsoft.Bot.Connector;
using Newtonsoft.Json;
using contoso.LUIS;
using contoso.ActionHandlers;

namespace contoso
{
    [BotAuthentication]
    public class MessagesController : ApiController
    {
        /// <summary>
        /// POST: api/Messages
        /// Receive a message from a user and reply to it
        /// </summary>
        public async Task<HttpResponseMessage> Post([FromBody]Activity activity)
        {
            //Create reply in seperate task, so 200 OK is sent immediately (acknowledging message),
            //but the reply can be delayed, e.g. with slow API calls
            Task.Run(() => HandleActivity(activity));

            var response = Request.CreateResponse(HttpStatusCode.OK);
            return response;
        }


        private async Task HandleActivity(Activity activity)
        {
            if (activity.Type == ActivityTypes.Message)
            {
                await ReplyToActivity(activity, await UserMessageResponse(activity));
            }
            else
            {
                await ReplyToActivity(activity, await SystemMessageResponse(activity));
            }
        }


        private async Task ReplyToActivity(Activity activity, Activity reply)
        {
            // return our reply to the user
            if (reply != null)
            {
                
                ConnectorClient connector = new ConnectorClient(new Uri(activity.ServiceUrl));
                await connector.Conversations.ReplyToActivityAsync(reply);
            }
        }


        private async Task<Activity> UserMessageResponse(Activity message)
        {
            LUISHandler.LUISQueryResult LUISResult = await LUISHandler.HandleQuery(message.Text);
            if (LUISResult.responseType == LUISHandler.ResponseType.ExchangeRate)
            {
                if (!LUISResult.parameters.ContainsKey("SourceRate"))
                    await ExchangeRateHandler.ExchangeRateReply(message, LUISResult.parameters["DestinationRate"]);
                else
                    await ExchangeRateHandler.ExchangeRateReply(message, LUISResult.parameters["DestinationRate"], LUISResult.parameters["SourceRate"]);
            }
            return message.CreateReply("Unimplemented");
        }


        private async Task<Activity> DeleteDataResponse(Activity message)
        {
            await DeleteData(message);
            return message.CreateReply("User data deleted!");
        }


        private async Task DeleteData(Activity message)
        {
            StateClient stateClient = message.GetStateClient();
            await stateClient.BotState.DeleteStateForUserAsync(message.ChannelId, message.From.Id);
        }


        private async Task<Activity> SystemMessageResponse(Activity message)
        {
            if (message.Type == ActivityTypes.DeleteUserData)
            {
                await DeleteData(message);
            }
            else if (message.Type == ActivityTypes.ConversationUpdate)
            {
                // Handle conversation state changes, like members being added and removed
                // Use Activity.MembersAdded and Activity.MembersRemoved and Activity.Action for info
                // Not available in all channels
            }
            else if (message.Type == ActivityTypes.ContactRelationUpdate)
            {
                // Handle add/remove from contact lists
                // Activity.From + Activity.Action represent what happened
            }
            else if (message.Type == ActivityTypes.Typing)
            {
                // Handle knowing tha the user is typing
            }
            else if (message.Type == ActivityTypes.Ping)
            {
            }

            return null;
        }
    }
}