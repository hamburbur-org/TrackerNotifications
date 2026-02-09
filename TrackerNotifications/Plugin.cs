using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using Newtonsoft.Json.Linq;
using Photon.Pun;
using UnityEngine;
using WebSocketSharp;

namespace TrackerNotifications;

[BepInPlugin(Constants.PluginGuid, Constants.PluginName, Constants.PluginVersion)]
public class Plugin : BaseUnityPlugin
{
    // doing it like this with a queue so its on the main thread instead of the network thread (yuck)
    private readonly Queue<string> receivedMessages = new();
    private readonly WebSocket     trackerWebSocket = new("wss://hamburbur.org/tracker");

    public static AssetBundle NotificationBundle { get; private set; }

    private void Start() => GorillaTagger.OnPlayerSpawned(OnGameInitialized);

    private void Update()
    {
        if (NotificationLib.Instance == null)
            return;

        lock (receivedMessages)
        {
            while (receivedMessages.Count > 0)
                ParseAndReceiveMessage(receivedMessages.Dequeue());
        }
    }

    private void OnGameInitialized()
    {
        Stream bundleStream = Assembly.GetExecutingAssembly()
                                      .GetManifestResourceStream("TrackerNotifications.Resources.trackernotifications");

        NotificationBundle = AssetBundle.LoadFromStream(bundleStream);
        bundleStream?.Close();

        gameObject.AddComponent<NotificationLib>();

        trackerWebSocket.OnMessage += (sender, messageEventArgs) =>
                                      {
                                          lock (receivedMessages)
                                          {
                                              receivedMessages.Enqueue(messageEventArgs.Data);
                                          }
                                      };

        trackerWebSocket.OnClose += (sender, closeEventArgs) => trackerWebSocket.ConnectAsync();
        trackerWebSocket.ConnectAsync();
    }

    private void ParseAndReceiveMessage(string data)
    {
        TrackingData trackingData = JObject.Parse(data).ToObject<TrackingData>();
        NotificationLib.Instance.SendNotification(
                $"[<color=green>Tracker</color>] {(trackingData.IsUserKnown ? trackingData.Username : "Someone")} {(trackingData.HasSpecialCosmetic ? $"with {trackingData.SpecialCosmetic}" : "")} found in {(PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom.Name == trackingData.RoomCode ? "your code" : $"code {trackingData.RoomCode}")} with {trackingData.PlayersInRoom}/10 players. Their in game name is {trackingData.InGameName} and the gamemode string is {trackingData.GameModeString}",
                10f);
    }

    [Serializable]
    private class TrackingData(
            bool   isUserKnown,
            bool   hasSpecialCosmetic,
            string username,
            string specialCosmetic,
            string inGameName,
            string roomCode,
            int    playersInRoom,
            string gameModeString)
    {
        public readonly string GameModeString     = gameModeString;
        public readonly bool   HasSpecialCosmetic = hasSpecialCosmetic;
        public readonly string InGameName         = inGameName;
        public readonly bool   IsUserKnown        = isUserKnown;
        public readonly int    PlayersInRoom      = playersInRoom;
        public readonly string RoomCode           = roomCode;
        public readonly string SpecialCosmetic    = specialCosmetic;
        public readonly string Username           = username;
    }
}