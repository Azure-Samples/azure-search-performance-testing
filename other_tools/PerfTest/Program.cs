// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Newtonsoft.Json;
using SearchPerfTest;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace PerfTest
{
    class Program
    {     
        static HttpClient client = new HttpClient();
        
        static ConcurrentBag<PerfStat> processedWork = new ConcurrentBag<PerfStat>();

        static string functionEndpoint = "https://{function-name}.azurewebsites.net";
        static string code = "";

        static string searchServiceName = "";
        static string resourceGroupName = "";
        static string subscriptionId = "";

        static int? currentReplicas = 1;
        static int maxReplicas = 5;
        static int partitions = 1;

        static int startQPS = 10;
        static int endQPS = 300;
        static int increment = 10;
        static int duration = 60;

        static void Main(string[] args)
        {
            var sm = new ManagementClient(resourceGroupName, searchServiceName, subscriptionId);

            Uri uri = new Uri(functionEndpoint);
            ServicePoint sp = ServicePointManager.FindServicePoint(uri);
            sp.ConnectionLimit = 25;

            var logFile = Path.Combine("../../../logs", $"{searchServiceName}-{currentReplicas}r{partitions}p-{DateTime.Now.ToString("yyyyMMddHHmm")}.csv");
            
            // Re-Create file with header 
            if (File.Exists(logFile))
                File.Delete(logFile);
            File.WriteAllText(logFile, "Service Name, Replicas, Partitions, Time (UTC), Target QPS, Target Duration, Actual Duration, Target Queries, Actual Queries, Successful Queries, Failed Queries, Avg Latency, Latency25, Latency75, Latency90, Latency95, Latency99 \r\n");


            Console.WriteLine("Starting the Azure Cognitive Search Performance Test!\n");

            string status = "";
            while (currentReplicas <= maxReplicas)
            {
                var replicaCheck = sm.GetReplicaCount();
                if (currentReplicas != replicaCheck)
                {
                    status = sm.GetStatus();
                    while (status != "Running")
                    {
                        Console.WriteLine($"Waiting for service to be ready...\n");
                        Thread.Sleep(15000);
                        status = sm.GetStatus();
                    }

                        Console.WriteLine($"Resizing service to {currentReplicas} replicas...");
                        sm.ScaleService(currentReplicas, partitions);
                        Thread.Sleep(2000);
                }

                status = sm.GetStatus();
                while (status != "Running")
                {
                    Console.WriteLine($"Waiting for service to be ready...\n");
                    Thread.Sleep(15000);
                    status = sm.GetStatus();
                }

                Console.WriteLine($"Running test for {currentReplicas} replicas!!!\n");
                int currentQPS = startQPS;
                int queriesPerThread = 10;

                while ( currentQPS <= endQPS)
                {
                    var startTime = DateTime.UtcNow;
                    Console.WriteLine($"Starting test at {currentQPS} QPS at {startTime.TimeOfDay}");
                    var taskList = new List<Task<List<PerfStat>>>();
                    var testStartTime = DateTime.Now;

                    for (int remainingQPS = currentQPS; remainingQPS > 0; remainingQPS = remainingQPS - queriesPerThread)
                    {
                        int thisQPS = Math.Min(remainingQPS, queriesPerThread);
                        taskList.Add(KickoffJobAsync(thisQPS, duration));
                        Thread.Sleep(50);
                    }

                    Console.WriteLine("Waiting for all tasks to complete...");
                    Task.WaitAll(taskList.ToArray());
                    var endTime = DateTime.UtcNow;

                    var results = new List<PerfStat>();
                    foreach (Task<List<PerfStat>> t in taskList)
                    {
                        results.AddRange(t.Result);
                    }

                    // Calculate Statistics
                    var successfulQueries = results.Where(s => s.runStatusCode == 200).Count();
                    var failedQueries = results.Where(s => s.runStatusCode != 200).Count();
                    var percentSuccess = 100 * successfulQueries / (failedQueries + successfulQueries);
                    var averageLatency = results.Select(x => x.runMS).Average();
                    var averageLatency25 = Percentile(results.Select(x => Convert.ToDouble(x.runMS)).ToArray(), 0.25);
                    var averageLatency75 = Percentile(results.Select(x => Convert.ToDouble(x.runMS)).ToArray(), 0.75);
                    var averageLatency90 = Percentile(results.Select(x => Convert.ToDouble(x.runMS)).ToArray(), 0.90);
                    var averageLatency95 = Percentile(results.Select(x => Convert.ToDouble(x.runMS)).ToArray(), 0.95);
                    var averageLatency99 = Percentile(results.Select(x => Convert.ToDouble(x.runMS)).ToArray(), 0.99);

                    string responseMessage = $"\nTarget QPS: {currentQPS} \n";
                    responseMessage += $"Target Duration: {duration} \n";
                    responseMessage += $"Actual Duration: {endTime - startTime} \n";
                    responseMessage += $"Successful Queries: {successfulQueries} \n";
                    responseMessage += $"Failed Queries: {failedQueries} \n";
                    responseMessage += $"Percent Succesful: {percentSuccess} \n";
                    responseMessage += $"Average Latency: {averageLatency} \n";
                    responseMessage += $"Latency 25th Percentile: {averageLatency25} \n";
                    responseMessage += $"Latency 75th Percentile: {averageLatency75} \n";
                    responseMessage += $"Latency 90th Percentile: {averageLatency90} \n";
                    responseMessage += $"Latency 95th Percentile: {averageLatency95} \n";
                    responseMessage += $"Latency 99th Percentile: {averageLatency99} \n";

                    File.AppendAllText(logFile, $"{searchServiceName}, {currentReplicas}, {partitions}, {startTime.ToString()}, {currentQPS}, {duration}, {endTime - startTime}, {currentQPS * duration}, {failedQueries + successfulQueries}, {successfulQueries}, {failedQueries}, {averageLatency}, {averageLatency25}, {averageLatency75}, {averageLatency90}, {averageLatency95}, {averageLatency99} \r\n");
                    Console.WriteLine(responseMessage);

                    if (percentSuccess < 97 || averageLatency > 1000)
                    {
                        break;
                    }

                    currentQPS += increment;
                }

                currentReplicas++;
            }

            Console.WriteLine("Test finished. Press any key to exit.");
            Console.ReadKey();
        }

        static async Task<List<PerfStat>> KickoffJobAsync(int qps, int duration)
        {
            Console.WriteLine($"Kicking off an Azure Function at {qps} QPS for {duration} seconds");
            
            string url = $"{functionEndpoint}/api/searchtestnode?code={code}&qps={qps}&duration={duration}";

            // To scale out to multiple azure function instances, follow this pattern
            //string url;
            //if (counter % 3 == 0)
            //{
            //    url = $"{functionEndpoint1}/api/searchtestnode?code={code1}&qps={qps}&duration={duration}";
            //}
            //else if (counter % 3 == 1)
            //{
            //    url = $"{functionEndpoint2}/api/searchtestnode?code={code2}&qps={qps}&duration={duration}";
            //}
            //else
            //{
            //    url = $"{functionEndpoint3}/api/searchtestnode?code={code3}&qps={qps}&duration={duration}";
            //}

            HttpResponseMessage response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            string responseJson = await response.Content.ReadAsStringAsync();
            List<PerfStat> performanceStats = JsonConvert.DeserializeObject<List<PerfStat>>(responseJson);


            return performanceStats;
           
        }

        // code from https://stackoverflow.com/questions/8137391/percentile-calculation
        static double Percentile(double[] sequence, double excelPercentile)
        {
            Array.Sort(sequence);
            int N = sequence.Length;
            double n = (N - 1) * excelPercentile + 1;
            // Another method: double n = (N + 1) * excelPercentile;
            if (n == 1d) return sequence[0];
            else if (n == N) return sequence[N - 1];
            else
            {
                int k = (int)n;
                double d = n - k;
                return sequence[k - 1] + d * (sequence[k] - sequence[k - 1]);
            }
        }
    }
}
