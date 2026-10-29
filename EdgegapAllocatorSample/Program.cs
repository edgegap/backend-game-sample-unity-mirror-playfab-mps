using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace EdgegapAllocator
{
    // The Edgegap counterpart of MpsAllocatorSample: start a game server (RequestMultiplayerServer),
    // list versions and deployments, and stop one. Plain HttpClient, no SDK.
    // API reference: https://docs.edgegap.com/docs/api/dedicated-servers
    class Program
    {
        const string Api = "https://api.edgegap.com";
        // Every deployment started here carries this tag, so they are easy to find and clean up.
        const string Tag = "edgegap-allocator-sample";

        static readonly HttpClient Http = new HttpClient();
        static string appName;
        static string appVersion;
        static string portName;

        static async Task<int> Main(string[] args)
        {
            Console.WriteLine("Welcome to the EdgegapAllocatorSample! This sample allows you to easily call frequently used Edgegap APIs on your application");

            string token = Environment.GetEnvironmentVariable("EDGEGAP_API_TOKEN");
            if (string.IsNullOrEmpty(token))
            {
                Console.WriteLine("Enter Edgegap API token (dashboard > User Settings > Tokens)");
                token = Console.ReadLine();
            }
            // The dashboard shows the token as "token XXXX"; the header needs exactly that form.
            token = token.Trim();
            if (!token.StartsWith("token ", StringComparison.OrdinalIgnoreCase))
                token = "token " + token;
            Http.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse(token);

            appName = Environment.GetEnvironmentVariable("EDGEGAP_APP") ?? Prompt("Enter application name");
            appVersion = Environment.GetEnvironmentVariable("EDGEGAP_VERSION") ?? Prompt("Enter version name");
            portName = Environment.GetEnvironmentVariable("EDGEGAP_PORT_NAME") ?? "gameport";

            // Scripted use: EdgegapAllocatorSample deploy <player IP or lat,lon> [more players...]
            if (args.Length > 0 && args[0] == "deploy")
                return await Deploy(args.Skip(1).ToArray()) != null ? 0 : 1;

            bool exitRequested = false;
            while (!exitRequested)
            {
                int option = PrintOptions();

                switch (option)
                {
                    case 0:
                        exitRequested = true;
                        break;
                    case 1:
                        await Deploy(Prompt("Enter player public IPs or lat,lon pairs, separated by spaces (the server is placed near them)")
                            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
                        break;
                    case 2:
                        PrettyPrintJson(await Call(HttpMethod.Get, $"/v1/app/{Uri.EscapeDataString(appName)}/versions"));
                        break;
                    case 3:
                        PrettyPrintJson(await Call(HttpMethod.Get, "/v1/deployments"));
                        break;
                    case 4:
                        PrettyPrintJson(await Call(HttpMethod.Get, $"/v1/status/{Uri.EscapeDataString(Prompt("Enter request ID"))}"));
                        break;
                    case 5:
                        PrettyPrintJson(await Call(HttpMethod.Delete, $"/v1/stop/{Uri.EscapeDataString(Prompt("Enter request ID"))}"));
                        break;
                }
            }
            return 0;
        }

        static int PrintOptions()
        {
            while (true)
            {
                Console.WriteLine("----------------------------------");
                Console.WriteLine("0 to exit the app");
                Console.WriteLine("1 to deploy a game server (RequestMultiplayerServer)");
                Console.WriteLine($"2 to list versions of {appName} (ListBuildSummaries)");
                Console.WriteLine("3 to list deployments (ListMultiplayerServers)");
                Console.WriteLine("4 to get a deployment (GetMultiplayerServerDetails)");
                Console.WriteLine("5 to stop a deployment (ShutdownMultiplayerServer)");
                Console.WriteLine("----------------------------------");
                if (int.TryParse(Console.ReadLine(), out int option) && option >= 0 && option <= 5)
                    return option;
                Console.WriteLine("Please enter a valid option");
            }
        }

        /// <summary>
        /// Start one deployment near the given players and wait until it is ready. Unlike MPS, nothing is
        /// pre-provisioned: Edgegap picks the location from the players and starts the container on request.
        /// </summary>
        static async Task<string> Deploy(string[] players)
        {
            if (players.Length == 0)
            {
                Console.WriteLine("Give at least one player IP or lat,lon.");
                return null;
            }

            var users = new JsonArray();
            foreach (string player in players)
            {
                string[] latLon = player.Split(',');
                users.Add(latLon.Length == 2
                    ? new JsonObject
                    {
                        ["user_type"] = "geo_coordinates",
                        ["user_data"] = new JsonObject { ["latitude"] = double.Parse(latLon[0]), ["longitude"] = double.Parse(latLon[1]) },
                    }
                    : new JsonObject
                    {
                        ["user_type"] = "ip_address",
                        ["user_data"] = new JsonObject { ["ip_address"] = player },
                    });
            }

            var body = new JsonObject
            {
                ["application"] = appName,
                ["version"] = appVersion,
                ["users"] = users,
                ["tags"] = new JsonArray(Tag),
            };
            JsonNode created = await Call(HttpMethod.Post, "/v2/deployments", body);
            string requestId = created?["request_id"]?.GetValue<string>();
            if (requestId == null)
                return null;
            Console.WriteLine($"Deployment {requestId} requested, waiting for it to be ready...");

            for (int attempt = 0; attempt < 60; attempt++)
            {
                await Task.Delay(TimeSpan.FromSeconds(3));
                JsonNode status = await Call(HttpMethod.Get, $"/v1/status/{requestId}", quiet: true);
                if (status == null)
                    continue;
                if (status["error"]?.GetValue<bool>() == true)
                {
                    Console.WriteLine($"Deployment {requestId} failed: {status["error_detail"]}");
                    return null;
                }
                if (status["running"]?.GetValue<bool>() == true)
                {
                    JsonNode port = status["ports"]?[portName];
                    string address = $"{status["fqdn"]}:{port?["external"]}";
                    Console.WriteLine($"Ready in {status["location"]?["city"]}, {status["location"]?["country"]}: {address} ({port?["protocol"]})");
                    Console.WriteLine($"Connect a client with: -server {address}");
                    Console.WriteLine($"Request ID (to stop it): {requestId}");
                    return address;
                }
            }
            Console.WriteLine($"Deployment {requestId} is not ready after 3 minutes; check it in the dashboard.");
            return null;
        }

        static async Task<JsonNode> Call(HttpMethod method, string path, JsonNode body = null, bool quiet = false)
        {
            using var request = new HttpRequestMessage(method, Api + path);
            if (body != null)
                request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
            using HttpResponseMessage response = await Http.SendAsync(request);
            string text = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                if (!quiet)
                    Console.WriteLine($"{method} {path} failed ({(int)response.StatusCode}): {text}");
                return null;
            }
            return string.IsNullOrWhiteSpace(text) ? new JsonObject() : JsonNode.Parse(text);
        }

        static string Prompt(string message)
        {
            Console.WriteLine(message);
            return Console.ReadLine();
        }

        static void PrettyPrintJson(JsonNode node)
        {
            if (node != null)
                Console.WriteLine(node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
