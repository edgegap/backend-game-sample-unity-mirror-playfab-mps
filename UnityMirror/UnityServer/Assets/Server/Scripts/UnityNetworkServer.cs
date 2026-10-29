namespace PlayFab.Networking
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;
    using Mirror;
    using UnityEngine.Events;

    public class UnityNetworkServer : NetworkManager
    {
        public static UnityNetworkServer Instance { get; private set; }

        public PlayerEvent OnPlayerAdded = new PlayerEvent();
        public PlayerEvent OnPlayerRemoved = new PlayerEvent();

        public int MaxConnections = 100;
        public int Port = 7777; // the Edgegap app version's "gameport" (TCP)

        public List<UnityNetworkConnection> Connections
        {
            get { return _connections; }
            private set { _connections = value; }
        }
        private List<UnityNetworkConnection> _connections = new List<UnityNetworkConnection>();

        public class PlayerEvent : UnityEvent<string> { }

        // Use this for initialization
        public override void Awake()
        {
            base.Awake();
            Instance = this;
            // EdgegapServer starts the server once it is set up, not Mirror's headless auto-start.
            autoStartServerBuild = false;
            NetworkServer.RegisterHandler<ReceiveAuthenticateMessage>(OnReceiveAuthenticate);
        }

        public void StartListen()
        {
            this.GetComponent<TelepathyTransport>().port = (ushort)Port;
            maxConnections = MaxConnections;
            // Through the NetworkManager: NetworkServer.Listen alone skips its connect and ready handlers,
            // so clients would be dropped as unauthenticated.
            if (!NetworkServer.active)
                StartServer();
        }

        public override void OnApplicationQuit()
        {
            base.OnApplicationQuit();
            NetworkServer.Shutdown();
        }

        private void OnReceiveAuthenticate(NetworkConnection nconn, ReceiveAuthenticateMessage message)
        {
            var conn = _connections.Find(c => c.ConnectionId == nconn.connectionId);
            if(conn != null)
            {
                conn.PlayFabId = message.PlayFabId;
                conn.IsAuthenticated = true;
                OnPlayerAdded.Invoke(message.PlayFabId);
            }
        }

        public override void OnServerConnect(NetworkConnection conn)
        {
            base.OnServerConnect(conn);

            Debug.LogWarning("Client Connected");
            var uconn = _connections.Find(c => c.ConnectionId == conn.connectionId);
            if (uconn == null)
            {
                _connections.Add(new UnityNetworkConnection()
                {
                    Connection = conn,
                    ConnectionId = conn.connectionId,
                    LobbyId = Environment.GetEnvironmentVariable("ARBITRIUM_REQUEST_ID") // the Edgegap deployment
                });
            }
        }

        public override void OnServerError(NetworkConnection conn, Exception ex)
        {
            base.OnServerError(conn, ex);

            Debug.Log(string.Format("Unity Network Connection Status: exception - {0}", ex.Message));
        }

        public override void OnServerDisconnect(NetworkConnection conn)
        {
            base.OnServerDisconnect(conn);

            var uconn = _connections.Find(c => c.ConnectionId == conn.connectionId);
            if (uconn != null)
            {
                if (!string.IsNullOrEmpty(uconn.PlayFabId))
                {
                    OnPlayerRemoved.Invoke(uconn.PlayFabId);
                }
                _connections.Remove(uconn);
            }
        }
    }

    [Serializable]
    public class UnityNetworkConnection
    {
        public bool IsAuthenticated;
        public string PlayFabId;
        public string LobbyId;
        public int ConnectionId;
        public NetworkConnection Connection;
    }

    public class CustomGameServerMessageTypes
    {
        public const short ReceiveAuthenticate = 900;
        public const short ShutdownMessage = 901;
        public const short MaintenanceMessage = 902;
    }

    public struct ReceiveAuthenticateMessage : NetworkMessage
    {
        public string PlayFabId;
    }

    public struct ShutdownMessage : NetworkMessage {}

    [Serializable]
    public struct MaintenanceMessage : NetworkMessage
    {
        public DateTime ScheduledMaintenanceUTC;
    }
}
