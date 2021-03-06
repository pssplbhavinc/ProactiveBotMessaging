//MIT License

//Copyright(c) 2017 Richard Custance

//Permission is hereby granted, free of charge, to any person obtaining a copy
//of this software and associated documentation files (the "Software"), to deal
//in the Software without restriction, including without limitation the rights
//to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//copies of the Software, and to permit persons to whom the Software is
//furnished to do so, subject to the following conditions:

//The above copyright notice and this permission notice shall be included in all
//copies or substantial portions of the Software.

//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//SOFTWARE.

// listens for new events happening on the queue (posted by ERP)
// matches the event to subscription items in storage
// posts a notification to the users subscribing to the event

#r "Microsoft.WindowsAzure.Storage"
#r "Newtonsoft.Json"
using Microsoft.WindowsAzure.Storage.Table;
using System.Configuration;
using Microsoft.Bot.Connector;
using Newtonsoft.Json;
using System;

public async static Task Run(string myQueueItem, CloudTable subscriptionTable, TraceWriter log)
{
    var _dialogName = @"[AzureFunction] ";

    log.Info($"C# Queue trigger function processed: {myQueueItem}");

    // query which subscriptions need to be notified that the event has happened
    var query = new TableQuery<SubscriptionTableItem>().Where(TableQuery.GenerateFilterCondition("SubscriptionId", QueryComparisons.Equal, myQueueItem));
    var results = subscriptionTable.ExecuteQuery(query).Select(s => (SubscriptionTableItem)s).ToList();

    // create a list so that we can keep track of what notifications are posted so that the 
    // subscriptions can be deleted from the storage table
    var postedNotifications = new List<SubscriptionTableItem>();

    // retrieve the Microsoft AppId and AppPassword from AppSettings 
    var appId = ConfigurationManager.AppSettings["MicrosoftAppId"];
    var appPassword = ConfigurationManager.AppSettings["MicrosoftAppPassword"];

    // for each subscription use the original conversation info to post back to the channel 
    foreach (SubscriptionTableItem subscription in results)
    {
        var conversationReference = JsonConvert.DeserializeObject<ConversationReference>(subscription.ConversationReference);
        
        MicrosoftAppCredentials.TrustServiceUrl(conversationReference.ServiceUrl);
        var client = new ConnectorClient(new Uri(conversationReference.ServiceUrl), new MicrosoftAppCredentials(appId, appPassword));
        var result = conversationReference.GetPostToBotMessage().CreateReply($@"{_dialogName}The order {subscription.SubscriptionId} has now been despatched! For more actions ask me to 'show actions'");
        client.Conversations.ReplyToActivity((Activity)result);

        // logging purposes only
        log.Info($"Notification posted to: {conversationReference.ChannelId}");

        // track that the notification has been posted
        postedNotifications.Add(subscription);
    }

    // delete each notification that has been posted correctly
    foreach(var subscription in postedNotifications)
    {
        TableOperation operation = TableOperation.Delete(subscription);
        await subscriptionTable.ExecuteAsync(operation);

        // logging purposes only
        log.Info($"Subscription: {subscription.SubscriptionId} deleted");
    }
}

public class SubscriptionTableItem : TableEntity
{
    public string Time {get; set;}
    public string ConversationReference {get; set;}
    public string SubscriptionId {get; set;}
}
