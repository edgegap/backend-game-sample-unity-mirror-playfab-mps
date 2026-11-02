# PlayFab Multiplayer Unity Mirror Game Server Sample

## Run it on Edgegap (this fork)

This fork runs the same Unity Mirror server on [Edgegap](https://edgegap.com) instead of PlayFab Multiplayer Servers (MPS). No SDK is needed on the server, and nothing is pre-provisioned: each server starts on request, near its players, and stops itself when they leave. There is no matchmaker yet: you start a server with [EdgegapAllocatorSample](../EdgegapAllocatorSample/README.md) (or the dashboard) and connect clients to the address it prints.

| On MPS (GSDK) | On Edgegap |
| --- | --- |
| `AgentListener.cs`: `Start`, port from `GetGameServerConnectionInfo`, `ReadyForPlayers`, listen on `OnServerActive` | `EdgegapServer.cs`: listen on the fixed port 7777 at startup |
| `UpdateConnectedPlayers` | Not needed. The server logs its player count. |
| `OnShutDownCallback` | Edgegap sends SIGTERM; players get the same `ShutdownMessage` when Unity quits |
| `OnMaintenanceCallback` | No equivalent: Edgegap doesn't recycle VMs under a running match |
| Build, VM size, servers per VM, standby servers | Application version: image, 0.5 vCPU / 1 GB per server, port `gameport` TCP 7777 |
| RequestMultiplayerServer | `POST /v2/deployments` ([EdgegapAllocatorSample](../EdgegapAllocatorSample/README.md)) |
| Server runs until MPS shuts it down | The server stops its own deployment 120 s after start if nobody joins, or 30 s after the last player leaves (`ARBITRIUM_DELETE_URL`). Call `SelfStop()` when a match ends. |

Both projects are on Unity 2021.3.45f2. The server is built and containerized with the [Edgegap Unity plugin](https://github.com/edgegap/buildtool-unity-plugin) (`com.edgegap.unity-servers-plugin`, pinned to `#3.2.1` in `UnityServer/Packages/manifest.json`), so the project has no Dockerfile of its own.

### 1. Build and upload the server

Install Unity 2021.3.45f2 with **Linux Build Support** and **Linux Dedicated Server Build Support**, and Docker Desktop. Open `UnityServer`, then **Tools → Edgegap Server Hosting**:

1. **Sign in with Edgegap**, then **Build server**.
2. **Containerize with Docker**: image name `mps-unity-mirror`, image tag empty (the plugin uses the current UTC time).
3. Optional local check: run parameters `-p 7777:7777/tcp`, then **Deploy local container**.
4. **Upload to Edgegap**: application `mps-unity-mirror`, then **Upload image and Create app version**. In the version form: one port named `gameport`, 7777, **TCP** (Mirror's Telepathy transport); 0.5 vCPU / 1 GB; a max duration of 60 minutes; and, if asked, the registry username and token from the dashboard's **Container Registry** page.

From the command line (CI, scripted tests):

```bash
"<Unity>" -batchmode -projectPath UnityMirror/UnityServer -executeMethod EdgegapPluginBuild.BuildServer -logFile build-server.log
```

### 2. Start a server

```bash
cd EdgegapAllocatorSample
export EDGEGAP_API_TOKEN="token xxxxxxxx" EDGEGAP_APP=mps-unity-mirror EDGEGAP_VERSION=<version name>
dotnet run -- deploy <your public IP>
```

It prints `-server <host>:<port>` once the server is ready, usually within seconds.

### 3. Connect clients

Build `UnityClient` for Windows (it has no server build, so Unity's command line works as is), then pass the address:

```bash
"<Unity>" -batchmode -quit -projectPath UnityMirror/UnityClient -buildTarget Win64 -buildWindows64Player Builds/Client/win64/Client.exe
UnityMirror/UnityClient/Builds/Client/win64/Client.exe -server <host>:<port>
```

Without `-server`, the client uses the address and port set on its `NetworkManager` in the Inspector. Quit the clients: the server stops its deployment 30 s later.

### Tested

On 2026-10-29 with Unity 2021.3.45f2 and plugin 3.2.1: `EdgegapAllocatorSample deploy` started the server in Toronto within seconds, two Windows clients connected over TCP (`2 player(s) connected`), and after they quit the server stopped its own deployment. Also run in Docker locally: same result, and `docker stop` sent the connected client a shutdown before the server exited.

Plugin 3.2.1's Dockerfile runs `env` first, so the deployment logs show the injected variables, including the self-stop token. Fixed in edgegap/buildtool-unity-plugin#62.

### Changes from the MPS version

- `AgentListener.cs` → `EdgegapServer.cs` (same `.meta`, so the scene needs no edit), and `Assets/PlayFabSdk` removed.
- `UnityNetworkServer.StartListen` starts the server through Mirror's `NetworkManager.StartServer()`, with Mirror's headless auto-start off. Calling `NetworkServer.Listen` directly skipped the manager's connect handlers, so clients were dropped as unauthenticated once nothing delayed the start.
- Client: `-server host:port`, transport port 7777 to match the server, and the build scene fixed (`Client.unity`; the build settings pointed at a missing `ClientScene.unity`).
- `Packages/` is committed for both projects (upstream ignores it), so the plugin version is pinned.
- Removed upstream's MPS `UnityMirror/Dockerfile` (the plugin builds the image), and the unused PlayFab ID message between client and server.

---

## The original PlayFab Multiplayer Servers version

Microsoft's instructions for running this sample on PlayFab Multiplayer Servers (GSDK, LocalMultiplayerAgent, Windows containers, the MPS Linux Dockerfile) describe code this fork removed. They're in [PlayFab/MpsSamples](https://github.com/PlayFab/MpsSamples/blob/040e752/UnityMirror/README.md), at the commit this fork started from.
