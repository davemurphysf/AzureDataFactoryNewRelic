# AzureDataFactoryNewRelic
Azure Function for periodically pulling in Azure Data Factory metrics into New Relic Insights.

## Setup Requirements

### Create an Azure Active Directory application.
 1. Create an application in Azure Active Directory that represents the application you are creating. For the sign-on URL, you can provide a dummy URL as shown in the article (https://contoso.org/exampleapp).
 2. Get the **application ID, authentication key,** and **tenant ID**.
 4. Assign the application to the Reader role at the subscription level.

### Create a New Relic Insights Events API insert key
 1. Go to [insights.newrelic.com](https://insights.newrelic.com) > Manage data > API keys.
 2. Next to the Insert keys heading, select the + symbol.
 3. Enter a short description (owner, team, purpose, data source, etc.) for the key.
 4. Select Save your notes.

### (Optional) Create an Azure Functions app
 - Either [create a new functions app](https://docs.microsoft.com/en-us/azure/azure-functions/functions-create-scheduled-function) or use an existing one

### Add Environment Variables
 - TenantId
   + Azure AD tenantId
 - ApplicationId
    + Azure AD applicationId
 - AuthenticationKey
    + Azure AD applicationKey
 - SubscriptionId
    + Azure subscriptionId (use `az account show ` from cli)
 - ResourceGroup
    + Resource Group that holds the Azure Data Factories you want to monitor
 - MinuteInterval
    + Interval for timer function (i.e. `5` for a function that runs every 5 minutes)
 - NewRelicInsightsInsertAPIKey
    + New Relic Insights API Insert key (note this is **NOT** the same a normal account API key, [see here](https://docs.newrelic.com/docs/insights/insights-data-sources/custom-data/send-custom-events-event-api#register))
 - NewRelicAccountId
    + If you login to New Relic APM, your accountId is the first number in the URL `https://rpm.newrelic.com/accounts/{ACCOUNT_ID}/applications`


