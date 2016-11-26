﻿using contoso.ActionHandlers;
using contoso.Controllers;
using contoso.LUIS;
using Microsoft.Bot.Connector;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;

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


        private async Task<Activity> UserMessageResponse(Activity message)
        {
            if (!await FacebookController.CheckAuth(message))
            {
                return await FacebookController.LoginHandler(message);
            }
            LUISHandler.LUISQueryResult LUISResult = await LUISHandler.HandleQuery(message.Text);
            switch (LUISResult.responseType)
            {
                case LUISHandler.ResponseType.ExchangeRate:
                    return await ExchangeRateHandler.HandleExchangeRateMessage(message, LUISResult);
                case LUISHandler.ResponseType.Logout:
                    return await FacebookController.LogoutHandler(message);
                default:
                    return message.CreateReply("Unimplemented");
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


        private async Task<Activity> SystemMessageResponse(Activity message)
        {
            if (message.Type == ActivityTypes.DeleteUserData)
            {
                await FacebookController.LogoutHandler(message);
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