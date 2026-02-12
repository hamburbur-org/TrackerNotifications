using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using GorillaNotifications;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using Photon.Pun;
using UnityEngine.Networking;
using WebSocketSharp;

namespace TrackerNotifications;

[BepInIncompatibility("hansolo1000falcon.zlothy.hamburbur")]
[BepInPlugin(Constants.PluginGuid, Constants.PluginName, Constants.PluginVersion)]
public class Plugin : BaseUnityPlugin
{
    // doing it like this with a queue so it's on the main thread instead of the network thread (yuck)
    private readonly Queue<string> receivedMessages = new();
    private readonly WebSocket     trackerWebSocket = new("wss://hamburbur.org/tracker");
    private static          JObject       cachedData;

    private void Start()
    {
        GorillaTagger.OnPlayerSpawned(OnGameInitialized);
#if GC_TRACKER
        
#else
        new Harmony(Constants.PluginGuid).PatchAll();
#endif
    }

    private void Update()
    {
        lock (receivedMessages)
        {
            while (receivedMessages.Count > 0)
                ParseAndReceiveMessage(receivedMessages.Dequeue());
        }
    }

    private void OnGameInitialized()
    {
        trackerWebSocket.OnMessage += (sender, messageEventArgs) =>
                                      {
                                          lock (receivedMessages)
                                          {
                                              receivedMessages.Enqueue(messageEventArgs.Data);
                                          }
                                      };

        trackerWebSocket.OnClose += (sender, closeEventArgs) => trackerWebSocket.ConnectAsync();
        trackerWebSocket.ConnectAsync();
        
        #if GC_TRACKER
        #else
        StartCoroutine(FetchData());
#endif
    }

    private IEnumerator FetchData()
    {
        UnityWebRequest request = UnityWebRequest.Get("https://hamburbur.org/data");
        yield return request.SendWebRequest();
        
        if (request.result != UnityWebRequest.Result.Success)
            yield break;
        
        cachedData = JObject.Parse(request.downloadHandler.text);
    }

    private void ParseAndReceiveMessage(string data)
    {
        TrackingData trackingData = JObject.Parse(data).ToObject<TrackingData>();
        NotificationController.SendNotification("<color=green>Tracker</color>",
                $"{(trackingData.IsUserKnown ? trackingData.Username : "Someone")} {(trackingData.HasSpecialCosmetic ? $"with {trackingData.SpecialCosmetic}" : "")} found in {(PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom.Name == trackingData.RoomCode ? "your code" : $"code {trackingData.RoomCode}")} with {trackingData.PlayersInRoom}/10 players. Their in game name is {trackingData.InGameName} and the gamemode string is {trackingData.GameModeString}",
                10f);
    }

    [HarmonyPatch(typeof(VRRig))]
    [HarmonyPatch("IUserCosmeticsCallback.OnGetUserCosmetics", MethodType.Normal)]
    private static class OnRigCosmeticsLoadedPatch
    {
        private static void Postfix(VRRig __instance)
        {
            string specialCosmetic = cachedData["specialCosmetics"].ToObject<Dictionary<string, string>>()
                                                     .Where(cosmeticData => __instance.HasCosmetic(cosmeticData.Key))
                                                     .Aggregate("",
                                                              (current, cosmeticData) =>
                                                                      current + cosmeticData.Value + ", ");

            specialCosmetic = specialCosmetic.TrimEnd(',', ' ');
            specialCosmetic = specialCosmetic.Trim();

            string username = cachedData["knownPeople"].ToObject<Dictionary<string, string>>()
                                                        .GetValueOrDefault(__instance.OwningNetPlayer.UserId, "");

            if (string.IsNullOrWhiteSpace(specialCosmetic) && string.IsNullOrWhiteSpace(username))
                return;

            TrackingData trackingData = new(!string.IsNullOrWhiteSpace(username),
                    !string.IsNullOrWhiteSpace(specialCosmetic), username, specialCosmetic,
                    __instance.OwningNetPlayer.SanitizedNickName, PhotonNetwork.CurrentRoom.Name,
                    PhotonNetwork.CurrentRoom.PlayerCount, NetworkSystem.Instance.GameModeString);
            NotificationController.SendNotification("<color=green>Tracker</color>",
                    $"{(trackingData.IsUserKnown ? trackingData.Username : "Someone")} {(trackingData.HasSpecialCosmetic ? $"with {trackingData.SpecialCosmetic}" : "")} found in {(PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom.Name == trackingData.RoomCode ? "your code" : $"code {trackingData.RoomCode}")} with {trackingData.PlayersInRoom}/10 players. Their in game name is {trackingData.InGameName} and the gamemode string is {trackingData.GameModeString}",
                    10f);
        }
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