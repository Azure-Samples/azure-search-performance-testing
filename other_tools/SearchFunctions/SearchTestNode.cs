// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web.Http;
using System.Net;

namespace SearchFunctions
{


    public static class SearchTestNode
    {
        // Getting environment variables
        static string searchServiceName = Environment.GetEnvironmentVariable("SearchSerivceName", EnvironmentVariableTarget.Process);
        static string searchAdminKey = Environment.GetEnvironmentVariable("SearchAdminKey", EnvironmentVariableTarget.Process);
        static string searchIndexName = Environment.GetEnvironmentVariable("SearchIndexName", EnvironmentVariableTarget.Process);
        static string apiVersion = "2020-06-30";
        static List<string> queryList = new List<string>();

        private static readonly Random _random = new Random();

        [FunctionName("SearchTestNode")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log, Microsoft.Azure.WebJobs.ExecutionContext context)
        {
            try
            {

                log.LogInformation("C# HTTP trigger function processed a request.");


                log.LogInformation(searchServiceName);

                var path = System.IO.Path.Combine(context.FunctionDirectory, "..\\semantic-scholar-queries.txt");
                var reader = new StreamReader(File.OpenRead(path));
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    queryList.Add(line.Replace('"', ' ').Trim());
                }

                // Creating a HttpClient to send queries
                HttpClient client = new HttpClient();
                client.DefaultRequestHeaders.Add("api-key", searchAdminKey);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                log.LogInformation("Search client created");

                // Reading in parameters
                int qps = int.Parse(req.Query["qps"]);
                int duration = int.Parse(req.Query["duration"]);
                int totalQueryCount = qps * duration;

                // Creating a variable to hold the results
                ConcurrentBag<PerfStat> processedWork = new ConcurrentBag<PerfStat>();
                var queries = new List<string>();

                var taskList = new List<Task>();
                var testStartTime = DateTime.Now;
                for (int i = 0; i < totalQueryCount; i++)
                {
                    taskList.Add(Task.Factory.StartNew(() => SendQuery(client, processedWork, log)));

                    double testRunTime = DateTime.Now.Subtract(testStartTime).TotalSeconds;
                    double currentQPS = i / testRunTime;
                    while (currentQPS > qps)
                    {
                        Thread.Sleep(100);
                        testRunTime = DateTime.Now.Subtract(testStartTime).TotalSeconds;
                        currentQPS = i / testRunTime;
                    }
                }

                Console.WriteLine("Waiting for all tasks to complete...");
                Task.WaitAll(taskList.ToArray());

                var successfulQueries = processedWork.Where(s => s.runStatusCode == 200).Count();
                var failedQueries = processedWork.Where(s => s.runStatusCode != 200).Count();
                var percentSuccess = 100 * successfulQueries / (qps * duration);
                var averageLatency = processedWork.Select(x => x.runMS).Average();

                string responseMessage = $"Successful Queries: {successfulQueries} \n";
                responseMessage += $"Failed Queries: {failedQueries} \n";
                responseMessage += $"Percent Succesful: {percentSuccess} \n";
                responseMessage += $"Average Latency: {averageLatency} \n";

                log.LogInformation(responseMessage);

                return new OkObjectResult(processedWork);
            }
            catch (Exception e)
            {
                log.LogInformation(e.ToString());

                return new ExceptionResult(e, true);
            }
        }

        static async Task SendQuery(HttpClient client, ConcurrentBag<PerfStat> processedWork, ILogger log)
        {
            try
            {
                var startTime = DateTime.UtcNow;
                string query = GetSearchTerm();


                string url = $"https://{searchServiceName}.search.windows.net/indexes/{searchIndexName}/docs?api-version=2020-06-30&search={query}&queryType=full&$count=true&highlight=paperAbstract&facets=entities,fieldsOfStudy,year,journalName";
                HttpResponseMessage response = await client.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    int x = 5;
                }

                processedWork.Add(new PerfStat
                {
                    runStatusCode = (int)response.StatusCode,
                    runMS = Convert.ToInt32(response.Headers.GetValues("elapsed-time").FirstOrDefault().ToString()),
                    runTime = startTime
                });

            }
            catch (Exception e)
            {
                log.LogInformation(e.ToString());
            }

        }

        static string GetSearchTerm()
        {
            int randomNumber = _random.Next(0, queryList.Count - 1);

            return queryList[randomNumber];
        }
    }
}
