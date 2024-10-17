using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Azure.Core;
using Azure.Identity;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.VisualStudio.Services.Client;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.VisualStudio.Services.WebApi;

namespace storageadopipelinetrigger
{
    public class Function1
    {
        private readonly ILogger<Function1> _logger;

        public static string AZURE_TENANT_ID = Environment.GetEnvironmentVariable("AZURE_TENANT_ID") ?? "TenantIdNotProvided";// "";

        // ClientId for User Assigned Managed Identity. Leave null for System Assigned Managed Identity
        public static string AZURE_CLIENT_ID = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID") ?? "ManagedUserIdentityNotProvided";


        public Function1(ILogger<Function1> logger)
        {
            _logger = logger;
        }


        public static TokenCredential credential =
        new DefaultAzureCredential(
            new DefaultAzureCredentialOptions
            {
                TenantId = AZURE_TENANT_ID,
                ManagedIdentityClientId = AZURE_CLIENT_ID,
                ExcludeEnvironmentCredential = false
            });

        [Function(nameof(Function1))]
        public async Task Run([BlobTrigger("samples-workitems/{name}")] Stream stream, string name)
        {
            using var blobStreamReader = new StreamReader(stream);
            var content = await blobStreamReader.ReadToEndAsync();
            _logger.LogInformation($"C# Blob trigger function Processed blob\n Name: {name} \n Data: {content}");

            #region ADOTrigger
            try
            {
                var accessToken = (await credential.GetTokenAsync(new TokenRequestContext(new[] { "499b84ac-1321-427f-aa17-267ca6975798/.default" }), CancellationToken.None));
                var token = new VssAadToken("Bearer", accessToken.Token);
                var vssAadCredential = new VssAadCredential(token);
                var settings = VssClientHttpRequestSettings.Default.Clone();
                //settings.UserAgent = AppUserAgent;
                var organizationUrl = new Uri(new Uri("https://dev.azure.com/"), "thechef0830");
                var vssConnection = new VssConnection(organizationUrl, vssAadCredential);
                var projectClient = vssConnection.GetClient<ProjectHttpClient>();
                var project = await projectClient.GetProject("customersandbox001");

                var buildClient = await vssConnection.GetClientAsync<BuildHttpClient>();
                //var buildClient = vssConnection.GetClient<BuildHttpClient>();

                var definition = await buildClient.GetDefinitionAsync("customersandbox001", 1);
                var build = new Build
                {
                    Definition = definition,
                    Project = project,
                    Reason = BuildReason.UserCreated
                };

                var response = await buildClient.QueueBuildAsync(build, ignoreWarnings: true);
                _logger.LogError("ADO Pipeline triggered successfully: at queue position: " + response.RequestedBy.DisplayName);
            }
            catch (Exception ex)
            {
                _logger.LogInformation($">>>>>Error encountered: {ex.Message} \n Data: {ex.InnerException}");
                throw;
            }
            #endregion
        }
    }
}
