using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Azure.Functions.Worker.Http;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace kong.konnect
{
	class azureLogDataCollector
	{
		private readonly ILogger _logger;

		// Update customerId to your Log Analytics workspace ID
		//static string customerId = "<****CHANGE_ME****>";
		static string customerId = "7c59c6c9-7817-4e37-9cbd-191da9521769";

		// For sharedKey, use either the primary or the secondary Connected Sources client authentication key   
		//static string sharedKey = "<****CHANGE_ME****>";
		static string sharedKey = "i3FT0oybRYBwCkkfaWykV3Xj5ZiTSBzfJQatHInzF0iApxJ98zH2+r6FHldO6/rMqvigOBDNKDIbIMm0UToSAA==";

		// LogName is name of the event type that is being submitted to Azure Monitor
		static string LogName = "kong_CP_CL";

		// You can use an optional field to specify the timestamp from the data. If the time field is not specified, Azure Monitor assumes the time is the message ingestion time
		static string TimeStampField = "";

		public azureLogDataCollector(ILoggerFactory loggerFactory)
		{
    		_logger = loggerFactory.CreateLogger<azureLogDataCollector>();
		}
		
		public async Task<string> receiveAndSendLogsToAzure (HttpRequestData req)
		{
			string requestBody = "";
			var valueContentEnconding = "";
			//----------------------------------------------------------------------
			// Decompress the traffic if needed by checking Content-Encoding header
			//----------------------------------------------------------------------
			try
			{
				valueContentEnconding = req.Headers.GetValues("Content-Encoding").First();
			}
			catch {}
			_logger.LogInformation("Konnect Audit Logs | Content-Encoding: " + valueContentEnconding);

			if (valueContentEnconding == "deflate")
			{
				using (DeflateStream zippedStream = new DeflateStream(req.Body, CompressionMode.Decompress))
				{
					using (MemoryStream reader = new MemoryStream())
					{
						zippedStream.CopyTo(reader);
						byte[] result = reader.ToArray();
						requestBody = Encoding.Default.GetString(result);
					}
				}
			}
			else if (valueContentEnconding == "gzip")
			{
				using (GZipStream zippedStream = new GZipStream(req.Body, CompressionMode.Decompress))
				{
					using (MemoryStream reader = new MemoryStream())
					{
						zippedStream.CopyTo(reader);
						byte[] result = reader.ToArray();
						requestBody = Encoding.Default.GetString(result);
					}
				}
			}
			else
			{   
				req.Body.Position = 0;
				requestBody = await new StreamReader(req.Body).ReadToEndAsync();
			}
			
			_logger.LogInformation("Konnect Audit Logs | Original | data: " + requestBody);

			// Re-format the String sent by Konnect for adding a comma ',' between each entry
			requestBody = "[" + requestBody + "]";
			requestBody = requestBody.Replace ("}\n{", "},\n{");
			
			// Re-format the String sent by Konnect for changing the trace_id from a Double to a String
			// Azure doesn't ingest correctly the Double there is a limit to 10000000000000000 and 
			// Konnect sends for example => "trace_id":6187117426119443960
			// So we re-format it to => "trace_id":"6187117426119443960"	
			int start = 0;
			int next = 0;
			String requestBody2 = "";
			String traceId = "\"trace_id\":";
			while (next != -1){
				next = requestBody.IndexOf(traceId, start);
				if (next != -1){
					// Copy the text "trace_id":
					requestBody2 += requestBody.Substring(start, next - start + traceId.Length);
					// Find the end of the value of trace_id
					var endTraceId = requestBody.IndexOf(",", next + 1);
					if (endTraceId == -1) {
						endTraceId = requestBody.IndexOf("}", next + 1);
					}
					// Copy the value of trace_id (example: 6187117426119443960)
					requestBody2 += "\"" + requestBody.Substring(next + traceId.Length, endTraceId - next - traceId.Length) + "\"";
					start = next + traceId.Length + endTraceId - next - traceId.Length;
				}
				// There is no 'trace_id' anymore and copy the rest of the string
				else{
					var mylength = requestBody.Length;
					mylength = requestBody.Length - start;
					requestBody2 += requestBody.Substring(start, requestBody.Length - start);
				}
			}
			
			_logger.LogInformation("Konnect Audit Logs | Re-formated | data: " + requestBody2);

			//-----------------------------------------------------------
			// Sign the request (with HMAC) AND
			// Send the logs (received from Konnect CP) to the Azure Log 
			//-----------------------------------------------------------
			return sendKonnectLogToAzureAnalytics( requestBody2);
		}

		//----------------------------------------------------------------------------
		// Sign and Send the logs (received from Konnect CP) to the Azure Log 
		// Analytics Workspace.
		// The request is signed with HMAC-SHA256 and the Authorization signature has
		// a this format, for instance:
		// 		POST
  		//  	100
		//  	application/json
		//  	x-ms-date:Thu, 29 Jun 2023 10:15:22 GMT
		//  	/api/logs
		//----------------------------------------------------------------------------
		public  string sendKonnectLogToAzureAnalytics( string jsonKonnect)
		{
			// Create a hash for the API signature
			var datestring = DateTime.UtcNow.ToString("r");
			var jsonBytes = Encoding.UTF8.GetBytes(jsonKonnect);
			string stringToHash = "POST\n" + jsonBytes.Length + "\napplication/json\n" + "x-ms-date:" + datestring + "\n/api/logs";
			string hashedString = BuildSignature(stringToHash, sharedKey);
			string signature = "SharedKey " + customerId + ":" + hashedString;
			
			_logger.LogInformation ("signature: " + signature);
			return PostData(signature, datestring, jsonKonnect);
		}

		//---------------------------------------------------
		// Build the API signature: use HMAC-SHA256
		// Signature=Base64(HMAC-SHA256(UTF8(StringToSign)))
		//---------------------------------------------------
		public static string BuildSignature(string message, string secret)
		{
			var encoding = new System.Text.ASCIIEncoding();
			byte[] keyByte = Convert.FromBase64String(secret);
			byte[] messageBytes = encoding.GetBytes(message);
			using (var hmacsha256 = new HMACSHA256(keyByte))
			{
				byte[] hash = hmacsha256.ComputeHash(messageBytes);
				return Convert.ToBase64String(hash);
			}
		}

		//-------------------------------------------------
		// Send the request to the Azure POST API endpoint
		//-------------------------------------------------
		public string PostData(string signature, string date, string json)
		{
			string returnCode;
			try
			{
				string url = "https://" + customerId + ".ods.opinsights.azure.com/api/logs?api-version=2016-04-01";

				System.Net.Http.HttpClient client = new System.Net.Http.HttpClient();
				client.DefaultRequestHeaders.Add("Accept", "application/json");
				client.DefaultRequestHeaders.Add("Log-Type", LogName);
				client.DefaultRequestHeaders.Add("Authorization", signature);
				client.DefaultRequestHeaders.Add("x-ms-date", date);
				client.DefaultRequestHeaders.Add("time-generated-field", TimeStampField);

				// If charset=utf-8 is part of the content-type header, the API call may return forbidden.
				System.Net.Http.HttpContent httpContent = new StringContent(json, Encoding.UTF8);
				httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
				_logger.LogInformation ("Azure Analytics Workspace | Request | URL: " + url);
				_logger.LogInformation ("Azure Analytics Workspace | Request | Content: " + httpContent.ReadAsStringAsync().Result);
				Task<System.Net.Http.HttpResponseMessage> response = client.PostAsync(new Uri(url), httpContent);
				
				System.Net.Http.HttpContent responseContent = response.Result.Content;
				string result = responseContent.ReadAsStringAsync().Result;
				_logger.LogInformation ("Azure Analytics Workspace | Response | HTTP Code: " + response.Result.StatusCode + " | result: '" + result + "'");
				returnCode = response.Result.StatusCode.ToString();
			}
			catch (Exception excep)
			{
				_logger.LogError ("API Post Exception: " + excep.Message);
				returnCode = excep.Message;
			}
			return  returnCode;
		}
	}
}