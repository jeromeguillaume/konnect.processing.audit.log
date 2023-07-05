# Azure Function: collect Kong Control Plane Audit logs and push them to Azure Log Analytics
The Azure function is in charge of:
1) Collecting logs sent by Kong Control Plane [Audit logs](https://docs.konghq.com/konnect/org-management/audit-logging/)
2) Pushing log to Azure Log Analytics with the [Azure HTTP Data Collector REST API](https://learn.microsoft.com/en-us/rest/api/loganalytics/create-request)

## Create the Azure Log Analytics Workspace
1) Sign in to Azure Portal, [here](https://portal.azure.com/)
2) Look for **Log Analytics workspaces** on Azure services
3) Create a new Log Analytics workspace called for instance `kong-log-analytics-ws`
4) Once the WS is created, click on `Agents` menu on the left, expand `Log Analytics agent instructions` in the middle and copy/paste the values of the `Workspace ID` and `Primary key`

![Alt text](/images/1-Azure-Log-Analytics-Workspace.png "Log Analytics Workspace")


## Create the local Azure Function App
Create the Azure Function App by following this [tutorial](https://learn.microsoft.com/en-us/azure/azure-functions/create-first-function-vs-code-csharp) and this guidance:

### Create a local project
Take these properties:
- Language: `C#`
- .NET runtime: `.NET 7.0 Isolated (LTS)`
- Template: `HTTP trigger`
- Function name: `konnect_audit_log_processing`
- Provide a namespace: `kong.konnect`
- Authorization level: `Function`

### Modify the code of the local project
Copy from the GitHub repository the following files in the local project:
- `konnect-audit-log-processing.cs`
- `azureKonnect.cs`

To do: 
1) **if you changed the Namespace** adapt the ```kong.konnect``` to your value
2) **If you changed the Function name** adapt the class name and file name to your values
3) Modify the file `azureKonnect.cs` with your Log Analytics properties taken on previous step:
```C#
static string customerId = "<Workspace ID>"
static string sharedKey = "<Primary key>"
```
### Test the local Azure Function App
In Visual Studio Code, click  Run & Debug and Start Debugging (F5). In the terminal console you see an URL which looks like:
http://localhost:7071/api/konnect_audit_log_processing

Use this command to test the Azure function app:
```shell
curl -X POST http://localhost:7071/api/konnect_audit_log_processing \
-H 'Content-Type: application/json' \
-d '{"event_product":"Konnect","event_class_id":"auditlogs"}'
```
The expected response is:
```shell
{"message From Azure Log Analytics": "OK"}
```

**Azure can take a long time creating the 1st log in the Analytics Workspace.** It took 10 minutes on my side. After the 1st creation log, other logs appear almost in real time.

### See logs in the Log Analytics Workspace
1) Open the `kong-log-analytics-ws` Azure Analytics Workspace
2) Click on `Logs` menu on the left
3) Close the popup `Queries` window
4) Access to the Query window and type:
```sql
kong_CP_CL
| order by TimeGenerated
```
5) Click on `Run`. 
Log sent by curl:
![Alt text](/images/2-Azure-Log-Analytics-run-query.png "Query on kong_CP_CL")

## Create and Deploy the public Azure Function App
See [tutorial](https://learn.microsoft.com/en-us/azure/azure-functions/create-first-function-vs-code-csharp#sign-in-to-azure) and follow this guidance:

### Create the Function App in Azure
- Select subscription (You won't see this prompt when you have -only one subscription visible under Resources)
- Globally unique name for the function app: ```konnect-audit-log-processing```
- Select a runtime stack: ```.NET 7 Isolated```
- Select a location: ```West Europe``` (for instance)

### Deploy the project to Azure
See [tutorial](https://learn.microsoft.com/en-us/azure/azure-functions/create-first-function-vs-code-csharp#deploy-the-project-to-azure)

In Visual Studio Code, choose the Azure icon in the Activity bar, then in the Workspace area, select your project folder and select the Deploy... button

### Test the public Azure Function App
The public URL of the Azure Function has this syntax:
`https://<function_app_name>.azurewebsites.net/api/<function_name>?code=<function_key>`

The function key is retrieved on Azure Portal:
1) Open the `konnect-audit-log-processing` Function App
2) Click on `Functions` menu on the left
3) Open the `konnect_audit_log_processing` function
4) Click on `Function Keys` menu on the left
5) Copy/paste the `Function Key` value

Function Key:
![Alt text](/images/3-Azure_Function_Key.png "Function Key")

Replace the `<****funtion_key****>` value and use this command to test the Azure function app:
```shell
curl -X POST "https://konnect-audit-log-processing.azurewebsites.net/api/konnect_audit_log_processing?\
code=<****funtion_key****>" \
-H 'Content-Type: application/json' \
-d '{"event_product":"Konnect","event_class_id":"auditlogs"}'
```
The expected response is:
```shell
{"message From Azure Log Analytics": "OK"}
```
See logs in the Log Analytics Workspace.

## Enable the Konnect Audit Logs
1) Log in to Konnect Portal, [here](https://cloud.konghq.com/)
2) Click on `Organizations` menu on the left
3) Click on `Audit Logs Setup` menu on the left
4) Configure the Control Plane location `US - North America` or `EU - Europe` with:
-  Endpoint: `https://konnect-audit-log-processing.azurewebsites.net/api/konnect_audit_log_processing?code=<****funtion_key****>`
- Log Format: `json`
- Enabled
5) Click on `Save`
6) Apply some load on Konnect: create a Gateway Service, apply a Plugin, create a Route, etc.

See Konnect Audit Logs in Azure log analytics:
![Alt text](/images/4-Azure-Analytics-Konnect.png "Konnect Audit Logs")

![Alt text](/images/5-Azure-Analytics-Konnect-detail.png "Konnect Audit Logs - Detail")

See Konnect [documentation](https://docs.konghq.com/konnect/org-management/audit-logging/reference/#log-formats) to have the complete list of properties.

## Troublsehoot Azure Function App
1) Open the `konnect-audit-log-processing` Function App
2) Click on `Log Stream` menu on the left
3) See the content log

![Alt text](/images/6-Azure-Log-Stream.png "Log Stream")
