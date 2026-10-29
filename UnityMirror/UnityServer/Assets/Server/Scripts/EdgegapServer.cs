using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using PlayFab.Networking;

/// <summary>
/// Game server lifecycle on Edgegap, in place of the GSDK's AgentListener. No SDK is needed:
/// - the server listens on a fixed port (the app version's "gameport", TCP 7777) as soon as it starts;
/// - it stops its own deployment through ARBITRIUM_DELETE_URL when nobody joins, or once everyone has left,
///   because an empty server still costs money;
/// - when Edgegap stops the deployment, the container gets SIGTERM, Unity quits, and connected players
///   get a ShutdownMessage first (the GSDK's OnShutDownCallback did this before).
/// </summary>
public class EdgegapServer : MonoBehaviour
{
    [Tooltip("Stop the deployment if no player connects within this time.")]
    public float FirstPlayerTimeoutSeconds = 120f;

    [Tooltip("Stop the deployment once all players have been gone for this long.")]
    public float EmptyServerTimeoutSeconds = 30f;

    private string _deleteUrl;
    private string _deleteToken;
    private float _startedAt;
    private float _emptySince = -1f;
    private int _lastPlayerCount;
    private bool _anyPlayerJoined;
    private bool _stopping;

    void Start()
    {
        Application.targetFrameRate = 60;
        UnityNetworkServer.Instance.StartListen();
        Debug.Log($"Edgegap Server | Listening on 0.0.0.0:{UnityNetworkServer.Instance.Port}.");
        Application.quitting += NotifyPlayersOfShutdown;

        _deleteUrl = Environment.GetEnvironmentVariable("ARBITRIUM_DELETE_URL");
        _deleteToken = Environment.GetEnvironmentVariable("ARBITRIUM_DELETE_TOKEN");
        _startedAt = Time.realtimeSinceStartup;

        string requestId = Environment.GetEnvironmentVariable("ARBITRIUM_REQUEST_ID");
        if (string.IsNullOrEmpty(requestId))
        {
            Debug.LogWarning("Edgegap Server | Not running on Edgegap, self-stop disabled.");
            enabled = false;
            return;
        }
        Debug.Log($"Edgegap Server | Deployment {requestId}.");
    }

    void Update()
    {
        if (_stopping)
            return;

        float now = Time.realtimeSinceStartup;
        int players = UnityNetworkServer.Instance.Connections.Count;
        if (players != _lastPlayerCount)
        {
            Debug.Log($"Edgegap Server | {players} player(s) connected.");
            _lastPlayerCount = players;
        }

        if (players > 0)
        {
            _anyPlayerJoined = true;
            _emptySince = -1f;
            return;
        }

        if (!_anyPlayerJoined)
        {
            if (now - _startedAt > FirstPlayerTimeoutSeconds)
                SelfStop("no player connected in time");
            return;
        }

        if (_emptySince < 0f)
            _emptySince = now;
        else if (now - _emptySince > EmptyServerTimeoutSeconds)
            SelfStop("all players left");
    }

    /// <summary>Stop this Edgegap deployment. Call it from game code when the match ends.</summary>
    public void SelfStop(string reason)
    {
        if (_stopping)
            return;
        _stopping = true;
        Debug.Log($"Edgegap Server | Stopping deployment: {reason}.");
        StartCoroutine(DeleteDeployment());
    }

    IEnumerator DeleteDeployment()
    {
        if (string.IsNullOrEmpty(_deleteUrl) || string.IsNullOrEmpty(_deleteToken))
        {
            Application.Quit();
            yield break;
        }

        // URL and one-time token are injected by Edgegap into this deployment only.
        using (UnityWebRequest request = UnityWebRequest.Delete(_deleteUrl))
        {
            request.SetRequestHeader("authorization", _deleteToken);
            yield return request.SendWebRequest();
            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("Edgegap Server | Self-stop requested.");
            }
            else
            {
                Debug.LogError($"Edgegap Server | Self-stop failed ({request.responseCode} {request.error}), quitting.");
                Application.Quit();
            }
        }
    }

    private void NotifyPlayersOfShutdown()
    {
        Debug.Log("Edgegap Server | Shutting down.");
        foreach (var conn in UnityNetworkServer.Instance.Connections)
            conn.Connection.Send(new ShutdownMessage());
    }
}
