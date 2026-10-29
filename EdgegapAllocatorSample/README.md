# EdgegapAllocatorSample

The Edgegap counterpart of [MpsAllocatorSample](../MpsAllocatorSample/README.md): a small .NET console app that starts a game server on Edgegap and lists or stops deployments. It uses the [Edgegap API](https://docs.edgegap.com/docs/api/dedicated-servers) with `HttpClient`, no SDK.

| MpsAllocatorSample | EdgegapAllocatorSample | Edgegap API |
| --- | --- | --- |
| RequestMultiplayerServer | 1: deploy a game server, wait until it's ready, print `host:port` | `POST /v2/deployments`, then `GET /v1/status/{request_id}` |
| ListBuildSummaries | 2: list the application's versions | `GET /v1/app/{app}/versions` |
| ListMultiplayerServers | 3: list deployments | `GET /v1/deployments` |
| GetMultiplayerServerDetails | 4: get one deployment | `GET /v1/status/{request_id}` |
| ShutdownMultiplayerServer | 5: stop a deployment | `DELETE /v1/stop/{request_id}` |

There is no build region, standby pool or VM to choose: you give the players' public IPs (or `latitude,longitude`), and Edgegap starts the server at the best location for them. Every deployment started here is tagged `edgegap-allocator-sample`.

## Usage

Needs the [.NET SDK](https://dotnet.microsoft.com/download) 8 or newer.

```bash
export EDGEGAP_API_TOKEN="token xxxxxxxx"   # dashboard > User Settings > Tokens
export EDGEGAP_APP=mps-unity-mirror
export EDGEGAP_VERSION=20261029-1649
dotnet run
```

Missing values are asked for at startup. `EDGEGAP_PORT_NAME` (default `gameport`) is the port printed after a deploy.

For scripts and tests, deploy without the menu:

```bash
dotnet run -- deploy 203.0.113.10            # one player, by public IP
dotnet run -- deploy 45.50,-73.57 51.51,-0.13 # two players, by coordinates
```

It prints the address and the client argument, for example `-server 1a2b3c.pr.edgegap.net:31504`.

The API token can start and stop anything in your Edgegap organization: keep it on your machine or in your backend, never in a game client.
