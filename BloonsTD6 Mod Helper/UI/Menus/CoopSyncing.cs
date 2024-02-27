using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BTD_Mod_Helper;
using BTD_Mod_Helper.Api;
using BTD_Mod_Helper.Api.Components;
using BTD_Mod_Helper.Api.Coop;
using BTD_Mod_Helper.Api.Enums;
using BTD_Mod_Helper.Api.Helpers;
using BTD_Mod_Helper.Extensions;
using CoopSync;
using HarmonyLib;
using Il2Cpp;
using Il2CppAssets.Scripts.Unity.UI_New.Coop;
using Il2CppAssets.Scripts.Unity.UI_New.Popups;
using Il2CppNinjaKiwi.NKMulti;
using Il2CppTMPro;
using MelonLoader;
using MelonLoader.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;
using BTD_Mod_Helper.Api.Internal;

[assembly: MelonInfo(typeof(CoopSync.MelonMain), ModHelperData.Name, ModHelperData.Version, ModHelperData.RepoOwner)]
[assembly: MelonGame("Ninja Kiwi", "BloonsTD6")]

namespace CoopSync;

internal partial class MelonMain : BloonsTD6Mod
{
        private static NKMultiGameInterface _nkGi;
        private static ModHelperPanel syncPanel;
        private static ModHelperPanel playerSyncButton;
        private static bool waitingForMenuOpen;
        private static string gameCode;

        public override void OnTitleScreen()
        {
            if (File.Exists(MelonEnvironment.ModsDirectory + "/BloonsTD6 Mod Helper/matchCode.json"))
            {
                GameObject.Find("Canvas/ScreenBoxer/TitleScreen/Start").GetComponent<Button>().onClick.Invoke();
                gameCode = File.ReadAllText(MelonEnvironment.ModsDirectory + "/BloonsTD6 Mod Helper/matchCode.json");
                File.Delete(MelonEnvironment.ModsDirectory + "/BloonsTD6 Mod Helper/matchCode.json");
                waitingForMenuOpen = true;
            }
        }

        public override void OnUpdate()
        {
            if (waitingForMenuOpen)
            {
                try
                {
                    GameObject.Find("MainMenuCanvas/MainMenu/BottomButtonGroup/CoOp/CoopAnim/Button")
                        .GetComponent<Button>().onClick.Invoke();
                }
                catch
                {
                }

                try
                {
                    GameObject.Find("PlaySocialCanvas/PlaySocialScreen/Coop/Buttons/JoinMatch").GetComponent<Button>()
                        .onClick.Invoke();
                }
                catch
                {
                }

                try
                {
                    GameObject.Find("CoopJoinMatchCanvas/CoopJoinScreen/ButtonGroup/BgPanel/Code/CodeInput")
                        .GetComponent<TMP_InputField>().text = gameCode;
                }
                catch
                {
                }

                try
                {
                    GameObject.Find("CoopJoinMatchCanvas/CoopJoinScreen/ButtonGroup/BgPanel/Code/GoButton")
                        .GetComponent<Button>().onClick.Invoke();
                }
                catch
                {
                }
            }
        }

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
        private static class CoopLobbyScreen_Open
        {
            [HarmonyPostfix]
            private static void Postfix(CoopLobbyScreen __instance)
            {
                _nkGi = __instance.coopLobbyData.lobbyConnection.Connection.NKGI;
                waitingForMenuOpen = false;

                if (_nkGi.IsCoOpHost())
                {
                    syncPanel = __instance.gameObject.AddModHelperPanel(new Info("SyncMods", -200, -200, 350)
                    {
                        Pivot = Vector2.one,
                        Anchor = Vector2.one
                    });
                    var animator = syncPanel.AddComponent<Animator>();
                    animator.runtimeAnimatorController = Animations.PopupAnim;
                    animator.speed *= .7f;

                    syncPanel.AddButton(new Info("Button", InfoPreset.FillParent), VanillaSprites.BackupBtn,
                        new Action(() =>
                        {
                            PopupScreen.instance.SafelyQueue(screen => screen.ShowPopup(
                                PopupScreen.Placement.menuCenter,
                                "Sync Mods",
                                "Would you like to sync mods allowing you to change others active mods?",
                                new Action(SendMessage),
                                "Yes", null, "No", Popup.TransitionAnim.Scale));
                        }));
                    syncPanel.AddText(new Info("Text", 0, -200, 500, 100), "Sync Mods", 70);
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

        private static List<string> hostMods = new();
        private static List<string> player2Mods = new();
        private static List<string> player3Mods = new();
        private static List<string> player4Mods = new();

        public static void SendMessage()
        {
            syncPanel.transform.GetChild(0).GetComponent<Button>().SetOnClick(ReSyncAction);
            syncPanel.transform.GetChild(1).GetComponent<NK_TextMeshProUGUI>().SetText("ReSync Mods");
            syncPanel.transform.GetChild(1).GetComponent<NK_TextMeshProUGUI>().autoSizeTextContainer = true;
            List<MelonMod> loadedMods = RegisteredMelons.ToList();
            foreach (var mod in loadedMods)
            {
                hostMods.Add(mod.Info.Name);
            }

            _nkGi.SendToPeer(2, MessageUtils.CreateMessageEx(" ", "FindMods"));
            _nkGi.SendToPeer(3, MessageUtils.CreateMessageEx(" ", "FindMods"));
            _nkGi.SendToPeer(4, MessageUtils.CreateMessageEx(" ", "FindMods"));
        }

        private static List<string> ConvertJTokenToList(JToken token)
        {
            var resultList = new List<string>();

            if (token.Type == JTokenType.Array)
                foreach (var child in token.Children())
                    resultList.Add(child.ToString());
            else
                resultList.Add(token.ToString());

            return resultList;
        }

        public void UpdateModDifferences(int playerNumber, JObject playerModDataJson)
        {
            List<string> playerMods = new();
            playerMods = ConvertJTokenToList(playerModDataJson.GetValue("ModList"));
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

            var modsToAdd = hostMods.Except(playerMods).ToList();
            var modsToRemove = playerMods.Except(hostMods).ToList();
            if (modsToRemove.Count != 0 || modsToAdd.Count != 0)
            {
                playerSyncButton = GameObject.Find("Canvas/CoopLobbyScreen/CoopPlayerInfo").transform
                    .GetChild(playerNumber - 1).gameObject.AddModHelperPanel(new Info("SyncMods", -206.85f, -50f, 100)
                    {
                        Pivot = Vector2.one,
                        Anchor = Vector2.one
                    });
                var animator = playerSyncButton.AddComponent<Animator>();
                animator.runtimeAnimatorController = Animations.PopupAnim;
                animator.speed *= .7f;

                Action action = () => ChangeMods(playerNumber);
                playerSyncButton.AddButton(new Info("Button", InfoPreset.FillParent), VanillaSprites.BackupBtn,
                    new Action(() =>
                    {
                        PopupScreen.instance.SafelyQueue(screen => screen.ShowPopup(PopupScreen.Placement.menuCenter,
                            "Sync Player's Mods",
                            "Would you like to sync this player's mods with yours?",
                            action,
                            "Yes", null, "No", Popup.TransitionAnim.Scale));
                    }));
                playerSyncButton.AddText(new Info("Text", 0, -75, 500, 100), "Sync Player's Mods", 30);
            }
        }

        public void RestartGame(string matchCode)
        {
            File.WriteAllText(MelonEnvironment.ModsDirectory + "/BloonsTD6 Mod Helper/matchCode.json", matchCode);
            ProcessHelper.RestartGame();
        }

        public void AddAndRemoveMods(List<string> modsToAdd, List<string> modsToRemove)
        {
            foreach (var mod in modsToRemove)
            {
                foreach (var melonMod in RegisteredMelons.ToList())
                {
                    if (mod == melonMod.Info.Name)
                    {
                        var modPath = melonMod.MelonAssembly.Assembly.Location;
                        var modName = Path.GetFileName(modPath);
                        var folderPath = MelonEnvironment.ModsDirectory + "/Disabled";
                        if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);
                        var newFilePath = Path.Combine(folderPath, modName);
                        if (File.Exists(newFilePath)) File.Delete(newFilePath);
                        File.Move(modPath, newFilePath);
                    }
                }
            }

            foreach (var mod in ModHelperGithub.Mods)
            {
                foreach (var modToAdd in modsToAdd)
                {
                    if (mod.Name == modToAdd)
                    {
                        modsToAdd.Remove(modToAdd);
                        ModHelperGithub.DownloadLatest(mod, true);
                    }
                }
            }

            RestartGame(_nkGi.MatchID);
        }

        public void RequestModChange(List<string> modsToAdd, List<string> modsToRemove)
        {
            Action declineModSyncAction = () =>
            {
                _nkGi.SendToPeer(1, MessageUtils.CreateMessageEx(_nkGi.PeerID, "DeclineSync"));
            };
            Action syncModsAction = () => { AddAndRemoveMods(modsToAdd, modsToRemove); };
            StringBuilder sb = new StringBuilder();
            sb.Append("The host has requested a mod sync. ");
            if (modsToAdd.Count > 0)
            {
                sb.Append("If you sync, the mod(s) that will be added are: ");
                foreach (var mod in modsToAdd)
                {
                    if (mod != modsToAdd[0])
                    {
                        sb.Append(", ");
                    }

                    sb.Append(mod);
                }

                sb.Append(". ");
            }

            if (modsToRemove.Count > 0)
            {
                sb.Append("If you sync, the mod(s) that will be removed are: ");
                foreach (var mod in modsToRemove)
                {
                    if (mod != modsToRemove[0])
                    {
                        sb.Append(", ");
                    }

                    sb.Append(mod);
                }

                sb.Append(". ");
            }

            sb.Append(
                "By clicking ok, it will download added mods off mod browser, disable removed mods, and restart your game.");
            PopupScreen.instance.SafelyQueue(screen => screen.ShowPopup(PopupScreen.Placement.menuCenter,
                "Sync Mods",
                sb.ToString(),
                syncModsAction,
                "Yes", declineModSyncAction, "No", Popup.TransitionAnim.Scale));
        }

        public void ChangeMods(int playerNumber)
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
            Dictionary<string, object> modsData = new Dictionary<string, object>
            {
                { "modsToAdd", modsToAdd },
                { "modsToRemove", modsToRemove }
            };
            _nkGi.SendToPeer(playerNumber,
                MessageUtils.CreateMessageEx(JsonConvert.SerializeObject(modsData, Formatting.Indented), "ChangeMods"));
        }

        public void PlayerDeclined(int playerNumber)
        {
            Action cancelAction = () =>
            {
                GameObject.Find("Canvas/CoopLobbyScreen/CoopPlayerInfo/PlayerSlot" + playerNumber + "/SyncMods")
                    .Destroy();
            };
            Action okAction = () =>
            {
                _nkGi.SendToPeer(playerNumber, MessageUtils.CreateMessageEx(" ", "KickPlayer"));
                GameObject.Find("Canvas/CoopLobbyScreen/CoopPlayerInfo/PlayerSlot" + playerNumber + "/SyncMods")
                    .Destroy();
            };
            PopupScreen.instance.SafelyQueue(screen => screen.ShowPopup(PopupScreen.Placement.menuCenter,
                "Player" + playerNumber + " Declined Mod Sync",
                "Would you like to kick this player?",
                okAction,
                "Yes", cancelAction, "No", Popup.TransitionAnim.Scale));
        }

        public override bool ActOnMessage(Message message)
        {
            int playerNumber;
            switch (message.Code)
            {
                case "KickPlayer":
                    GameObject.Find("Canvas/ForegroundScreen/Back/Back").GetComponent<Button>().onClick.Invoke();
                    return true;
                case "DeclineSync":
                    playerNumber = MessageUtils.ReadMessage<int>(message);
                    PlayerDeclined(playerNumber);
                    return true;
                case "ChangeMods":
                    var modsData =
                        JsonConvert.DeserializeObject<Dictionary<string, List<object>>>(
                            MessageUtils.ReadMessage<string>(message));
                    var modsToAdd = modsData["modsToAdd"].Cast<string>().ToList();
                    var modsToRemove = modsData["modsToRemove"].Cast<string>().ToList();
                    RequestModChange(modsToAdd, modsToRemove);
                    return true;
                case "ReturnMods":
                    var playerModDataJson = JObject.Parse(MessageUtils.ReadMessage<string>(message));
                    playerNumber = int.Parse(playerModDataJson.GetValue("PlayerNumber").ToString());
                    UpdateModDifferences(playerNumber, playerModDataJson);
                    return true;
                case "FindMods":
                    List<MelonMod> loadedMods = RegisteredMelons.ToList();
                    var playersMods = new List<string>();
                    foreach (var mod in loadedMods)
                    {
                        playersMods.Add(mod.Info.Name);
                    }

                    _nkGi.SendToPeer(1,
                        MessageUtils.CreateMessageEx(
                            JsonConvert.SerializeObject(new PlayerModData(_nkGi.PeerID, playersMods),
                                Formatting.Indented), "ReturnMods"));
                    return true;
                default:
                    return false;
            }
        }
    }
