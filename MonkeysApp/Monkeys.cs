using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
//using Newtonsoft.Json;
using Carter;
using Carter.ModelBinding;
using Carter.Request;
using Carter.Response;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Linq;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;

namespace MonkeysApp
{
    public class Monkeys
    {
        [FunctionName("try")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            string requestBody = String.Empty;
            using (StreamReader streamReader = new StreamReader(req.Body))
            {
                requestBody = await streamReader.ReadToEndAsync();
            }

            TryRequest t = JsonSerializer.Deserialize<TryRequest>(requestBody);
            Monkeys monkey = new Monkeys();
            monkey.GeneticAlgorithm(t);

            return new OkObjectResult("Try request recieved");
        }
        async Task<AssessResponse> PostFitnessAssess(AssessRequest areq)
        {
            var client = new HttpClient();
            client.BaseAddress = new Uri("https://fitnessapp20210205105606.azurewebsites.net/api/assess?code=hA6/LM3aog0L0T0agyBpoP7SWGo63ylrOtlkIlNaOFqG1Vq5NdpiZQ==");
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Add("x-functions-key", "4qiPl9le1Qi6mEEONh6vkhAtKdGpa4tnmcIwFnseVWH9aHlRDrKrkw==");
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
            var hrm = await client.PostAsJsonAsync("", areq);
            hrm.EnsureSuccessStatusCode();
            var ares = await hrm.Content.ReadAsAsync<AssessResponse>();
            return ares;
        }

        async Task PostClientTop(TopRequest treq)
        {
            var client = new HttpClient();
            client.BaseAddress = new Uri($"https://blazorrealtimeserverapi.azure-api.net/api/notifications");
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", "1c7c670dd005438bbe6606b7c59e477b");
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
            var hrm = await client.PostAsJsonAsync("", treq);
            hrm.EnsureSuccessStatusCode();
        }


        private Random _random = new Random(1);

        private double NextDouble()
        {
            lock (this)
            {
                return _random.NextDouble();
            }
        }

        private int NextInt(int a, int b)
        {
            lock (this)
            {
                return _random.Next(a, b);
            }
        }

        int ProportionalRandom(int[] weights, int sum)
        {
            var val = NextDouble() * sum;

            for (var i = 0; i < weights.Length; i++)
            {
                if (val < weights[i]) return i;

                val -= weights[i];
            }
            return 0;
        }

        async void GeneticAlgorithm(TryRequest treq)
        {
            
            await Task.Delay(0);
            var id = treq.id;
            var parallel = treq.parallel;
            var monkeys = treq.monkeys;
            if (monkeys % 2 != 0) monkeys += 1;
            var length = treq.length;
            var crossover = treq.crossover / 100.0;
            var mutation = treq.mutation / 100.0;
            var limit = treq.limit;
            if (limit == 0) limit = 1000;
            Console.WriteLine(treq.ToString());
            if (length == 0)
            {
                var areq = new AssessRequest { id = id, genomes = new List<string> { "" } };
                var ares = await PostFitnessAssess(areq);
                length = ares.scores[0];
            }

            var genomes = Enumerable.Range(1, monkeys)
                .Select(m =>
                {
                    var s = new char[length];
                    for (int i = 0; i < length; i++)
                        s[i] = (char)NextInt(32, 127);
                    return new String(s);
                }).ToList();

            var topscore = int.MaxValue;

            for (int loop = 0; loop < limit; loop++)
            {
                //WriteLine ($"[{tid()}] ... genomes \"{string.Join("\", \"", genomes)}\"");
                var areq = new AssessRequest { id = id, genomes = genomes };
                var ares = await PostFitnessAssess(areq);

                var scores = ares.scores;
                int topscore2 = 0;
                int maxscore = -1;
                int[] weights = null;
                int sumweights = 0;

                if (parallel)
                {
                    var pscores = scores.AsParallel();
                    topscore2 = pscores.Min();
                    maxscore = pscores.Max();
                    weights = pscores.Select(s => maxscore - s + 1).ToArray();
                    sumweights = weights.AsParallel().Sum();

                }
                else
                {
                    topscore2 = scores.Min();
                    maxscore = scores.Max();
                    weights = scores.Select(s => maxscore - s + 1).ToArray();
                    sumweights = weights.Sum();
                }

                //WriteLine ($"[{tid()}] ... {loop} score ranges {topscore2}..{maxscore}");
                //WriteLine ($"[{tid()}] ... scores {string.Join(", ", scores)}");
                //WriteLine ($"[{tid()}] ... sumweights {sumweights} weights {string.Join(", ", weights)}");

                if (topscore2 < topscore)
                {
                    topscore = topscore2;
                    var i = scores.FindIndex(s => s == topscore2);
                    var top = new TopRequest { id = id, loop = loop, score = topscore, genome = genomes[i] };
                    await PostClientTop(top);
                    if (topscore == 0) break;
                }

                Func<int, string[]> evolve = i =>
                {
                    string p1 = genomes[ProportionalRandom(weights, sumweights)];
                    string p2 = genomes[ProportionalRandom(weights, sumweights)];

                    if (NextDouble() < crossover)
                    {
                        var x = NextInt(0, length);  // 1
                        var c11 = p1.Substring(0, x);
                        var c21 = p2.Substring(0, x);
                        var c12 = p1.Substring(x);
                        var c22 = p2.Substring(x);
                        p1 = c11 + c22;
                        p2 = c21 + c12;
                    }

                    if (NextDouble() < mutation)
                    {
                        var c1 = p1.ToCharArray();
                        var x2 = NextInt(0, length);
                        c1[x2] = (char)NextInt(32, 127);
                        p1 = new String(c1);
                    }

                    if (NextDouble() < mutation)
                    {
                        var c2 = p2.ToCharArray();
                        var x2 = NextInt(0, length);
                        c2[x2] = (char)NextInt(32, 127);
                        p2 = new String(c2);
                    }

                    return new[] { p1, p2 };
                };

                if (parallel)
                {
                    genomes = ParallelEnumerable.Range(1, monkeys / 2)
                        .SelectMany<int, string>(evolve).ToList();
                }
                else
                {
                    genomes = Enumerable.Range(1, monkeys / 2)
                        .SelectMany<int, string>(evolve).ToList();
                }
            }
        }
        public class TryRequest
        {
            public int id { get; set; }
            public bool parallel { get; set; }
            public int monkeys { get; set; }
            public int length { get; set; }
            public int crossover { get; set; }
            public int mutation { get; set; }
            public int limit { get; set; }
            public override string ToString()
            {
                return $"{{{id}, {parallel}, {monkeys}, {length}, {crossover}, {mutation}, {limit}}}";
            }
        }

        public class TopRequest
        {
            public int id { get; set; }
            public int loop { get; set; }
            public int score { get; set; }
            public string genome { get; set; }
            public override string ToString()
            {
                return $"{{{id}, {loop}, {score}, {genome}}}";
            }
        }

        public class AssessRequest
        {
            public int id { get; set; }
            public List<string> genomes { get; set; }
            public override string ToString()
            {
                return $"{{{id}, #{genomes.Count}}}";
            }
        }

        public class AssessResponse
        {
            public int id { get; set; }
            public List<int> scores { get; set; }
            public override string ToString()
            {
                return $"{{{id}, #{scores.Count}}}";
            }
        }

    }

}
