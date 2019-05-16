using Microsoft.Azure.Management.DataFactory;
using Microsoft.Azure.Management.DataFactory.Models;
using Microsoft.Azure.Management.ResourceManager;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Rest;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace AzureDataFactoryNewRelic.FetchMetrics
{
    public static class FetchADFMetrics
    {
        [FunctionName("FetchADFMetrics")]
        public static async Task Run([TimerTrigger("0 */5 * * * *")]TimerInfo myTimer, ILogger log)
        {
            log.LogInformation($"FetchADFMetrics function executed at: {DateTime.Now}");

            // Set variables

            var tenantID = Environment.GetEnvironmentVariable("TenantId", EnvironmentVariableTarget.Process);
            var applicationId = Environment.GetEnvironmentVariable("ApplicationId", EnvironmentVariableTarget.Process);
            var authenticationKey = Environment.GetEnvironmentVariable("AuthenticationKey", EnvironmentVariableTarget.Process);
            var subscriptionId = Environment.GetEnvironmentVariable("SubscriptionId", EnvironmentVariableTarget.Process);
            var resourceGroup = Environment.GetEnvironmentVariable("ResourceGroup", EnvironmentVariableTarget.Process);
            var minuteInterval = Environment.GetEnvironmentVariable("MinuteInterval", EnvironmentVariableTarget.Process);
            var newRelicInsightsInsertAPIKey = Environment.GetEnvironmentVariable("NewRelicInsightsInsertAPIKey", EnvironmentVariableTarget.Process);
            var newRelicAccountId = Environment.GetEnvironmentVariable("NewRelicAccountId", EnvironmentVariableTarget.Process);

            // Authenticate and create a data factory management client

            var context = new AuthenticationContext("https://login.windows.net/" + tenantID);
            ClientCredential cc = new ClientCredential(applicationId, authenticationKey);
            AuthenticationResult result = context.AcquireTokenAsync("https://management.azure.com/", cc).Result;
            ServiceClientCredentials cred = new TokenCredentials(result.AccessToken);
            var factoryClient = new DataFactoryManagementClient(cred) { SubscriptionId = subscriptionId };

            // Get list of factories

            List<Factory> factories = new List<Factory>();
            string nextPageLink = null;

            do
            {
                try
                {
                    Microsoft.Rest.Azure.IPage<Factory> newFactories;

                    if (string.IsNullOrEmpty(nextPageLink))
                    {
                        newFactories = await factoryClient.Factories.ListByResourceGroupAsync(resourceGroup);
                    }
                    else
                    {
                        newFactories = await factoryClient.Factories.ListByResourceGroupNextAsync(nextPageLink);
                    }

                    nextPageLink = newFactories.NextPageLink;
                    factories.AddRange(newFactories);
                }
                catch (Exception e)
                {
                    log.LogError("Error fetching Factories", e);
                    log.LogError($"Exception type = {e.GetType().Name}");
                    log.LogError($"Exception message = {e.Message}; source = {e.Source}");
                    log.LogError($"Exception stack trace = {e.StackTrace}");

                    return;
                }
            }
            while (!string.IsNullOrEmpty(nextPageLink));

            if (factories.Count == 0)
            {
                log.LogInformation($"No Factories found in resource group {resourceGroup}");
                return;
            }

            // Get list of pipeline runs in the last interval of minutes by factory

            List<PipelineRun> pipelineRuns = new List<PipelineRun>();
            nextPageLink = null;

            foreach (var f in factories)
            {
                do
                {
                    try
                    {
                        string token = null;
                        do
                        {
                            var updatedPipelineRuns = await factoryClient.PipelineRuns.QueryByFactoryAsync(
                                resourceGroup,
                                f.Name,
                                new RunFilterParameters(
                                    DateTime.UtcNow.AddMinutes(-5),
                                    DateTime.UtcNow,
                                    token
                                    )
                                );

                            pipelineRuns.AddRange(updatedPipelineRuns.Value);
                            token = updatedPipelineRuns.ContinuationToken;
                        }
                        while (!string.IsNullOrEmpty(token));
                    }
                    catch (Exception e)
                    {
                        log.LogError("Error fetching PipelineRuns", e);
                        log.LogError($"Exception type = {e.GetType().Name}");
                        log.LogError($"Exception message = {e.Message}; source = {e.Source}");
                        log.LogError($"Exception stack trace = {e.StackTrace}");

                        return;
                    }
                }
                while (!string.IsNullOrEmpty(nextPageLink));
            }

            if (pipelineRuns.Count == 0)
            {
                log.LogInformation($"No pipelineRun updates in the last {minuteInterval} minutes");
                return;
            }

            // Insert PipelineRun objects into New Relic Insights

            var handler = new HttpClientHandler();
            if (handler.SupportsAutomaticDecompression)
            {
                handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            }

            using (var httpClient = new HttpClient(handler))
            {
                httpClient.DefaultRequestHeaders.Add("X-Insert-Key", newRelicInsightsInsertAPIKey);
                httpClient.DefaultRequestHeaders.Add("Content-Type", "application/json");

                foreach (var p in pipelineRuns)
                {
                    try
                    {
                        var response = await httpClient.PostAsJsonAsync($"https://insights-collector.newrelic.com/v1/accounts/{newRelicAccountId}/events", p);
                        response.EnsureSuccessStatusCode();
                        string responseBody = await response.Content.ReadAsStringAsync();

                        log.LogInformation("Event insertion into Insights succeeeded");
                        log.LogInformation(responseBody);
                    }
                    catch (Exception e)
                    {
                        log.LogError("Error inserting PipelineRun into Insights", e);
                        log.LogError($"Exception type = {e.GetType().Name}");
                        log.LogError($"Exception message = {e.Message}; source = {e.Source}");
                        log.LogError($"Exception stack trace = {e.StackTrace}");
                    }
                }
            }
        }
    }
}
