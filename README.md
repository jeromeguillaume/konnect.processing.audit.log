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


## Create the Azure Function App
Create the Azure Function App by following this [tutorial](https://learn.microsoft.com/en-us/azure/azure-functions/create-first-function-vs-code-csharp) and by applying following properties

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

Note: 
- **if you changed the Namespace** adapt the ```kong.konnect``` to your value
- **If you changed the Function name** adapt the class name and file name to your values

Modify the file `azureKonnect.cs` with your Log Analytics properties taken on previous step:
```C#
static string customerId = "<Workspace ID>"
static string sharedKey = "<Primary key>"
```
### Test locally the code of the local project
In Visual Studio Code, click  Run & Debug and Start Debugging (F5). In the terminal console you see an URL which looks like:
http://localhost:7071/api/konnect_audit_log_processing

Use this command to test the Azure function app:
```shell
curl -X POST http://localhost:7071/api/konnect_audit_log_processing  
-H 'Content-Type: application/json' 
-d '{"event_product":"Konnect","event_class_id":"auditlogs"}'
```
The expected response is:
```shell
{"message From Azure Log Analytics": "OK"}
```
### Create the Function App in Azure
Take these properties:
- Select subscription (You won't see this prompt when you have -only one subscription visible under Resources)
- Globally unique name for the function app: ```konnect-audit-log-processing```
- Select a runtime stack: ```.NET 7 Isolated```
- Select a location: ```West Europe``` (for instance)

### Deploy the project to Azure
In Visual Studio Code, choose the Azure icon in the Activity bar, then in the Workspace area, select your project folder and select the Deploy... button

## Test the Azure Function App
The public URL of the Azure Function has this syntax:
```https://<function_app_name>.azurewebsites.net/api/<function_name>?code=<function_key>```
In cour case 
curl -v "https://konnect-audit-log-processing2.azurewebsites.net/
api/function_name_konnect?code=tHbmLGNUrI3HjWkkGSRYPXGdnh5fyviDMVviD2KY12UAAzFumCp2-A=="