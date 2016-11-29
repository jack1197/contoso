using contoso.ActionHandlers;
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

        //takes activity event and determines how to respond
        private async Task HandleActivity(Activity activity)
        {
            try
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
            catch
            {
                await ReplyToActivity(activity, activity.CreateReply("Error processing message"));
            }
        }


        //determine response to user message, i.e. text message
        private async Task<Activity> UserMessageResponse(Activity message)
        {
            if (!await FacebookHandler.IsAuthorised(message))
            {
                return await FacebookHandler.LoginHandler(message);
            }
            else if (await IsPendingConfirmation(message))
            {
                return await ConfirmationHandler(message);
            }
            else
            {
                return await NaturalInteractionHandler(message);
            }
        }


        //uses LUIS to understand user query inteligently, and respond appropriately
        private async Task<Activity> NaturalInteractionHandler(Activity message)
        {
            if ((message.Text.Trim().ToLower() ?? "") == "")
            {
                return message.CreateReply("Error: Message type unsupported");
            }
            LUISHandler.LUISQueryResult LUISResult = await LUISHandler.HandleQuery(message.Text);
            switch (LUISResult.responseType)
            {
                case LUISHandler.ResponseType.ExchangeRate:
                    return await ExchangeRateHandler.HandleExchangeRateMessage(message, LUISResult);
                case LUISHandler.ResponseType.Logout:
                    return await FacebookHandler.LogoutHandler(message);
                case LUISHandler.ResponseType.None:
                    return await UnknownHandler(message);
                case LUISHandler.ResponseType.AccountBalance:
                    return await BankHandler.HandleBalance(message);
                case LUISHandler.ResponseType.MakePayment:
                    return await BankHandler.HandleTransaction(message, LUISResult);
                case LUISHandler.ResponseType.TransactionHistory:
                    return await BankHandler.HandleHistory(message);
                default:
                    return message.CreateReply("Error: Unimplemented");
            }
        }


        //help message response
        private async Task<Activity> UnknownHandler(Activity message)
        {
            return message.CreateReply( "I'm sorry, I couldn't understand you. I can help with " +
                                        "a range of activities, these are: Finding exchange rates, " +
                                        "checking account information (e.g. Balance or transactions), and " + 
                                        "making transactions to another account");
        }


        //checks if the message is in response to a confirmation request (e.g. confirming transaction)
        private async Task<bool> IsPendingConfirmation(Activity message)
        {
            StateClient stateClient = message.GetStateClient();
            BotData userData = await stateClient.BotState.GetUserDataAsync(message.ChannelId, message.From.Id);
            return (userData.GetProperty<string>("Pending") ?? "") != "";
        }


        //Handle message as a confirmation or response to a bot query
        private async Task<Activity> ConfirmationHandler(Activity message)
        {
            StateClient stateClient = message.GetStateClient();
            BotData userData = await stateClient.BotState.GetUserDataAsync(message.ChannelId, message.From.Id);
            string pendingOn = userData.GetProperty<string>("Pending");
            switch (pendingOn)
            {
                case "Transaction":
                    return await BankHandler.CompletePendingTransaction(message);
                case "Logout":
                    return await FacebookHandler.HandlePendingLogout(message);
                default:
                    userData.SetProperty<string>("Pending", null);
                    await stateClient.BotState.SetUserDataAsync(message.ChannelId, message.From.Id, userData);
                    return message.CreateReply("Something went wrong, please try again");
            }
        }





        //send given reply activity back to orginal user
        private async Task ReplyToActivity(Activity activity, Activity reply)
        {
            // return our reply to the user
            if (reply != null)
            {
                ConnectorClient connector = new ConnectorClient(new Uri(activity.ServiceUrl));
                await connector.Conversations.ReplyToActivityAsync(reply);
            }
        }


        //handle system messages, e.g. user joined
        private async Task<Activity> SystemMessageResponse(Activity message)
        {
            if (message.Type == ActivityTypes.DeleteUserData)
            {
                await FacebookHandler.LogoutHandler(message);
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