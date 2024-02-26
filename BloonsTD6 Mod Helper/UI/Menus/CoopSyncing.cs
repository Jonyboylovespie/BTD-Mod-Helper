using System;
using System.Collections.Generic;
using System.Linq;
using BTD_Mod_Helper.Api;
using BTD_Mod_Helper.Api.Components;
using BTD_Mod_Helper.Api.Coop;
using BTD_Mod_Helper.Api.Enums;
using Il2CppAssets.Scripts.Unity.UI_New.Coop;
using Il2CppAssets.Scripts.Unity.UI_New.Popups;
using Il2CppNinjaKiwi.NKMulti;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;
namespace BTD_Mod_Helper.UI.Menus;

internal class CoopSyncing : BloonsTD6Mod
{
    private static NKMultiGameInterface? _nkGi;
    private static ModHelperPanel restartPanel;

    public class PlayerModData
    {
        public int PlayerNumber { get; set; }
        public List<string> ModList { get; set; }
        public PlayerModData(int playerNumber, List<string> modList)
        {
            PlayerNumber = playerNumber;
            ModList = modList;
        }
    }

    [HarmonyPatch(typeof(CoopLobbyScreen), nameof(CoopLobbyScreen.Open))]
    private static class CoopLobbyScreen_Open {
        [HarmonyPostfix]
        private static void Postfix(CoopLobbyScreen __instance)
        {
            _nkGi = __instance.coopLobbyData.lobbyConnection.Connection.NKGI;

            if (_nkGi.IsCoOpHost())
            {
                restartPanel = __instance.gameObject.AddModHelperPanel(new Info("SyncMods", -200, -200, 350)
                {
                    Pivot = Vector2.one,
                    Anchor = Vector2.one
                });
                var animator = restartPanel.AddComponent<Animator>();
                animator.runtimeAnimatorController = Animations.PopupAnim;
                animator.speed *= .7f;

                restartPanel.AddButton(new Info("Button", InfoPreset.FillParent), VanillaSprites.BackupBtn,
                    new Action(() =>
                    {
                        PopupScreen.instance.SafelyQueue(screen => screen.ShowPopup(PopupScreen.Placement.menuCenter,
                            "Sync Mods",
                            "Would you like to sync mods allowing you to change others active mods?", new Action(SendMessage),
                            "Yes", null, "No", Popup.TransitionAnim.Scale));
                    }));
                restartPanel.AddText(new Info("Text", 0, -200, 500, 100), "Sync Mods", 70);
            }
        }
    }

    public static void ReSyncAction()
    {
        PopupScreen.instance.SafelyQueue(screen => screen.ShowPopup(PopupScreen.Placement.menuCenter,
            "ReSync Mods",
            "Would you like to resync mods allowing you to change others active mods?", new Action(SendMessage),
            "Yes", null, "No", Popup.TransitionAnim.Scale));
    }
    
    static List<string> hostMods = new();
    static List<string> player2Mods = new();
    static List<string> player3Mods = new();
    static List<string> player4Mods = new();
    
    public static void SendMessage()
    {
        restartPanel.transform.GetChild(0).GetComponent<Button>().SetOnClick(ReSyncAction);
        restartPanel.transform.GetChild(1).GetComponent<NK_TextMeshProUGUI>().SetText("ReSync Mods");
        restartPanel.transform.GetChild(1).GetComponent<NK_TextMeshProUGUI>().autoSizeTextContainer = true;
        List<MelonMod> loadedMods = RegisteredMelons.ToList();
        foreach (var mod in loadedMods)
        {
            hostMods.Add(mod.Info.Name);
        }
        MelonLogger.Msg(JsonConvert.SerializeObject(new PlayerModData(_nkGi.PeerID, hostMods), Formatting.Indented));
        _nkGi.SendToPeer(2, MessageUtils.CreateMessageEx(" ", "FindMods"));
        _nkGi.SendToPeer(3, MessageUtils.CreateMessageEx(" ", "FindMods"));
        _nkGi.SendToPeer(4, MessageUtils.CreateMessageEx(" ", "FindMods"));
    }
    
    static List<string> ConvertJTokenToList(JToken token)
    {
        List<string> resultList = new List<string>();

        if (token.Type == JTokenType.Array)
        {
            foreach (JToken child in token.Children())
            {
                resultList.Add(child.ToString());
            }
        }
        else
        {
            resultList.Add(token.ToString());
        }

        return resultList;
    }

    public void UpdateModDifferences(int playerNumber)
    {
        List<string> playerMods = new();
        switch (playerNumber)
        {
            case 2:
                playerMods = player2Mods;
                break;
            case 3:
                playerMods = player3Mods;
                break;
            case 4:
                playerMods = player4Mods;
                break;
        }
        var modsToAdd = hostMods.Except(playerMods).ToList();
        var modsToRemove = playerMods.Except(hostMods).ToList();
        MelonLogger.Msg("Add Mods:");
        foreach (var mod in modsToAdd)
        {
            MelonLogger.Msg(mod);
        }
        MelonLogger.Msg("Remove Mods:");
        foreach (var mod in modsToRemove)
        {
            MelonLogger.Msg(mod);
        }
    }
    
    public override bool ActOnMessage(Message message)
    {
        switch (message.Code)
        {
            case "ReturnMods":
                JObject playerModDataJson = JObject.Parse(MessageUtils.ReadMessage<string>(message));
                List<string> playerMods = new();
                var playerNumber = int.Parse(playerModDataJson.GetValue("PlayerNumber").ToString());
                player2Mods = ConvertJTokenToList(playerModDataJson.GetValue("ModList"));
                switch (playerNumber)
                {
                    case 2:
                        player2Mods = playerMods;
                        break;
                    case 3:
                        player3Mods = playerMods;
                        break;
                    case 4:
                        player4Mods = playerMods;
                        break;
                }
                UpdateModDifferences(playerNumber);
                return true;
            case "FindMods":
                List<MelonMod> loadedMods = RegisteredMelons.ToList();
                List<string> playersMods = new List<string>();
                foreach (var mod in loadedMods)
                {
                    playersMods.Add(mod.Info.Name);
                }
                _nkGi.SendToPeer(1, MessageUtils.CreateMessageEx(JsonConvert.SerializeObject(new PlayerModData(_nkGi.PeerID, playersMods), Formatting.Indented), "ReturnMods"));
                return true;
            default:
                return false;
        }
    }
}
