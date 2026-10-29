namespace PlayFab.Networking
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;
    using Mirror;

    public class UnityNetworkServer : NetworkManager
    {
        public static UnityNetworkServer Instance { get; private set; }

        public int MaxConnections = 100;
        public int Port = 7777; // the Edgegap app version's "gameport" (TCP)

        public List<UnityNetworkConnection> Connections
        {
            get { return _connections; }
            private set { _connections = value; }
        }
        private List<UnityNetworkConnection> _connections = new List<UnityNetworkConnection>();

        // Use this for initialization
        public override void Awake()
        {
            base.Awake();
            Instance = this;
            // EdgegapServer starts the server once it is set up, not Mirror's headless auto-start.
            autoStartServerBuild = false;
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
                _connections.Remove(uconn);
        }
    }

    [Serializable]
    public class UnityNetworkConnection
    {
        public string LobbyId;
        public int ConnectionId;
        public NetworkConnection Connection;
    }

    public class CustomGameServerMessageTypes
    {
        public const short ShutdownMessage = 901;
        public const short MaintenanceMessage = 902;
    }

    public struct ShutdownMessage : NetworkMessage {}

    [Serializable]
    public struct MaintenanceMessage : NetworkMessage
    {
        public DateTime ScheduledMaintenanceUTC;
    }
}
