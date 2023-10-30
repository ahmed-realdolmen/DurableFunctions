using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace FunctionApp1
{
    public static class Function1
    {
        [FunctionName("Function1")]
        public static async Task<DataWithIsAdult> RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context)
        {
            var inputData = context.GetInput<string>();
            var obj1 = await context.CallActivityAsync<DataWithAge>("Function1_Parser", inputData);
            var obj2 = await context.CallActivityAsync<DataWithIsAdult>("Function1_CalculateAdult", obj1);
            await context.CallActivityAsync<object>("Function1_InsertInCosmosDb", obj2);

            return obj2;
        }

        public class DataWithAge
        {
            public string Name { get; set; }
            public int Age { get; set; }
        }
        public class DataWithIsAdult
        {
            public string Name { get; set; }
            public bool IsAdult { get; set; }
        }

        public class DataWithId
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public bool IsAdult { get; set; }
        }

        [FunctionName("Function1_Parser")]
        public static DataWithAge SayHello([ActivityTrigger] string obj, ILogger log)
        {
            var sep = obj.Split(",");
            return new DataWithAge
            {
                Name = sep[0],
                Age = int.Parse(sep[1])
            };
        }

        [FunctionName("Function1_CalculateAdult")]
        public static DataWithIsAdult CalculateAdult([ActivityTrigger] DataWithAge obj, ILogger log)
        {
            return new DataWithIsAdult
            {
                Name = obj.Name,
                IsAdult = obj.Age >= 18
            };
        }

        [FunctionName("Function1_InsertInCosmosDb")]
        public static object InsertInCosmosDb(
            [ActivityTrigger] DataWithIsAdult obj,
            [CosmosDB("lab3", "People", Connection = "CosmosDbConnectionString")] IAsyncCollector<DataWithId> document,
            ILogger log)
        {
            document.AddAsync(new DataWithId
            {
                Id = Guid.NewGuid().ToString(),
                Name = obj.Name,
                IsAdult = obj.IsAdult,
            });
            return obj;
        }

        [FunctionName("Function1_HttpStart")]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestMessage req,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            var data = await req.Content.ReadAsStringAsync();

            // Function input comes from the request content.
            // Important!! to have <string>
            string instanceId = await starter.StartNewAsync<string>("Function1", data);


            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }
    }
}