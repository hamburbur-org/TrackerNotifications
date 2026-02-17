using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using GorillaNotifications;
using GorillaNotifications.Core;
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
        JObject trackingData = JObject.Parse(data);
        NotificationController.SendNotification("<color=green>Tracker</color>",
                $"{(trackingData["isUserKnown"].ToObject<bool>() ? trackingData["username"].ToObject<string>() : "Someone")} {(trackingData["hasSpeecialCosmetic"].ToObject<bool>() ? $"with {trackingData["specialCosmetic"].ToObject<string>()}" : "")} found in {(PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom.Name == trackingData["roomCode"].ToObject<string>() ? "your code" : $"code {trackingData["roomCode"].ToObject<string>()}")} with {trackingData["playersInRoom"].ToObject<int>()} players. Their in game name is {trackingData["inGameName"].ToObject<string>()} and the gamemode string is {trackingData["gameModeString"].ToObject<string>()}",
                10f, FontType.Bit_Cell, StylingOptions.BlackBox);
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

            JObject trackingData = new()
            {
                    {"isUserKnown", !string.IsNullOrWhiteSpace(username)},
                    {"username", username},
                    {"hasSpecialCosmetic", !string.IsNullOrWhiteSpace(specialCosmetic)},
                    {"specialCosmetic", specialCosmetic},
                    {"roomCode", PhotonNetwork.CurrentRoom.Name},
                    {"playersInRoom", PhotonNetwork.CurrentRoom.PlayerCount},
                    {"inGameName", __instance.OwningNetPlayer.NickName},
                    {"gameModeString", NetworkSystem.Instance.GameModeString},
                    {"userId", __instance.OwningNetPlayer.UserId},
            };
            
            NotificationController.SendNotification("<color=green>Tracker</color>",
                    $"{(trackingData["isUserKnown"].ToObject<bool>() ? trackingData["username"].ToObject<string>() : "Someone")} {(trackingData["hasSpeecialCosmetic"].ToObject<bool>() ? $"with {trackingData["specialCosmetic"].ToObject<string>()}" : "")} found in {(PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom.Name == trackingData["roomCode"].ToObject<string>() ? "your code" : $"code {trackingData["roomCode"].ToObject<string>()}")} with {trackingData["playersInRoom"].ToObject<int>()} players. Their in game name is {trackingData["inGameName"].ToObject<string>()} and the gamemode string is {trackingData["gameModeString"].ToObject<string>()}",
                    10f, FontType.Bit_Cell, StylingOptions.BlackBox);
        }
    }
}