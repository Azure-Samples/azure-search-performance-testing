# Azure Cognitive Search benchmarking tool

The following code was used to produce the benchmarks for Cognitive Search. The code is optimized to run tests, scale the service, and collect the results.

For performance testing, we generally recommend using the [JMeter solution](https://github.com/Azure-Samples/azure-search-performance-testing) that's outlined in the main README rather than this tool.

## Prerequisites

+ An Azure Cognitive Search Service
+ An Azure Function resource (preferably in the same region as your search service). To scale up to higher QPS, additional Azure Functions will be needed.

## Run the solution

1. Open **SearchFunctions.sln** in Visual Studio

1. Update **SearchFunctions/local.settings.json**. It should look like this:

    ```json
    {
    "IsEncrypted": false,
    "Values": {
        "AzureWebJobsStorage": "UseDevelopmentStorage=true",
        "FUNCTIONS_WORKER_RUNTIME": "dotnet",

        "SearchSerivceName": "",
        "SearchAdminKey": "",
        "SearchIndexName": ""
    }
    }
    ```

1. Deploy the SearchFunctions to your Azure Functions resource. 
    - In Visual Studio, right click the project name -> select publish
    - Under **Actions** select **Manage Azure App Service settings** and make sure `SearchServiceName`, `SearchAdminKey`, and `SearchIndexName` all have the correct values under remote. This ensures the Azure Function has the proper environment variables.

1. Select **SearchPerfTest** as the startup project

1. Update the settings for **SearchPerfTest**

    * First, update the function endpoint and code to connect to your Azure Function project:

        ```csharp
        static string functionEndpoint = "https://delegenz-perf.azurewebsites.net";
        static string code = "";

        static string searchServiceName = "";
        static string resourceGroupName = "";
        static string subscriptionId = "";
        ```

    * Next, update the values that control the QPS for the tests (or leave the defaults):

        ```csharp
        int startQPS = 10;
        int endQPS = 550;
        int increment = 10;
        int duration = 60;
        ```
        
    * To scale the search service, you'll also need a Service Principal with access to your search service. Once you [create the service principal](https://docs.microsoft.com/azure/active-directory/develop/howto-create-service-principal-portal), update the following values in ManagementClient.cs:

        ```csharp
        private string clientId = "";
        private string clientSecret = "";
        private string tenantId = "";
        ```

1. At this point, you're ready to run the solution. Run the project and then monitor the results. Results will be added to the `logs` folder.
