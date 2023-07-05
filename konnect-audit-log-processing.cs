using System.Net;
using System.Text;
using System.IO.Compression;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace kong.konnect
{
    public class konnect_audit_log_processing
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;

        public konnect_audit_log_processing(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<konnect_audit_log_processing>();
        }
        
        [Function("konnect_audit_log_processing")]
        public async Task<HttpResponseData> RunAsync([HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req)
        {
            _logger.LogInformation("The function App is triggered");
        
            azureLogDataCollector azureLogDC = new azureLogDataCollector (_loggerFactory);
            
            string rcSendLogs = await azureLogDC.receiveAndSendLogsToAzure (req);

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");

            response.WriteString("{\"message From Azure Log Analytics\": \"" + rcSendLogs + "\"}");

            return response;
        }
    }
}
