using System;
using System.Linq;
using System.Threading.Tasks;
using BTD_Mod_Helper;
using BTD_Mod_Helper.Api;
using BTD_Mod_Helper.Api.Helpers;
using BTD_Mod_Helper.Api.Internal;
using BTD_Mod_Helper.Api.ModMenu;
using BTD_Mod_Helper.Api.ModOptions;
using BTD_Mod_Helper.UI.BTD6;
using BTD_Mod_Helper.UI.Modded;
using Il2CppAssets.Scripts.Data;
using Il2CppAssets.Scripts.Unity;
using Il2CppAssets.Scripts.Unity.UI_New.InGame;
using Il2CppAssets.Scripts.Unity.UI_New.InGame.TowerSelectionMenu;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using BTD_Mod_Helper.Api.Components;
using BTD_Mod_Helper.Api.Coop;
using BTD_Mod_Helper.Api.Enums;
using Il2CppAssets.Scripts.Unity.UI_New.Coop;
using Il2CppAssets.Scripts.Unity.UI_New.Popups;
using Il2CppNinjaKiwi.NKMulti;
using Il2CppTMPro;
using MelonLoader.Utils;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;
using TaskScheduler = BTD_Mod_Helper.Api.TaskScheduler;
[assembly: MelonInfo(typeof(MelonMain), ModHelper.Name, ModHelper.Version, ModHelper.Author)]
[assembly: MelonGame("Ninja Kiwi", "BloonsTD6")]
[assembly: MelonGame("Ninja Kiwi", "BloonsTD6-Epic")]
[assembly: MelonPriority(-1000)]
[assembly: MelonOptionalDependencies("NAudio.WinMM", "NAudio.Wasapi")] // Avoids the warning about these not getting ILRepacked; they're not needed

namespace BTD_Mod_Helper;

internal partial class MelonMain : BloonsTD6Mod
{
    public override void OnInitialize()
    {
        ModContentInstances.AddInstance(GetType(), this);

        // Create all and load default mod settings
        ModSettingsHandler.InitializeModSettings();

        try
        {
            ModHelperHttp.Init();
            ModHelperGithub.Init();

            Task.Run(ModHelperGithub.GetVerifiedModders);
            if (PopulateOnStartup)
            {
                Task.Run(ModHelperGithub.PopulateMods);
            }
        }
        catch (Exception e)
        {
            ModHelper.Warning(e);
        }

        // Load Content from other mods
        ModHelper.LoadAllMods();

        // Load any mod settings that were added from other types
        ModSettingsHandler.LoadModSettings();

        // Utility to patch all valid UI "Open" methods for custom UI
        ModGameMenu.PatchAllTheOpens(HarmonyInstance);


        Schedule_GameModel_Loaded();
        Schedule_GameModel_Loaded();
        Schedule_GameData_Loaded();

        try
        {
            // Create the targets file for mod sources
            ModHelperFiles.CreateTargetsFile(ModSourcesFolder);
        }
        catch (Exception e)
        {
            ModHelper.Warning("Could not create .targets file in Mod Sources. " +
                              "If you have no intention of making mods, you can ignore this.");
            ModHelper.Warning(e);
        }

        if (!ModHelper.IsEpic)
        {
            HarmonyInstance.CreateClassProcessor(typeof(EmbeddedBrowser.SteamWebView_OnGUI), true).Patch();
        }
    }

    public override void OnUpdate()
    {
        
        if (Input.GetKeyDown(KeyCode.I))
        {
            CoopSync.CreateModPopup(new List<string>(0), new List<string>(1) {"coolMod"});
        }
            
        if (waitingForMenuOpen)
        {
            if (GameObject.Find("MainMenuCanvas/MainMenu/BottomButtonGroup/CoOp/CoopAnim/Button").Exists()) GameObject.Find("MainMenuCanvas/MainMenu/BottomButtonGroup/CoOp/CoopAnim/Button").GetComponent<Button>().onClick.Invoke();
            if (GameObject.Find("PlaySocialCanvas/PlaySocialScreen/Coop/Buttons/JoinMatch").Exists()) GameObject.Find("PlaySocialCanvas/PlaySocialScreen/Coop/Buttons/JoinMatch").GetComponent<Button>().onClick.Invoke();
            if (GameObject.Find("CoopJoinMatchCanvas/CoopJoinScreen/ButtonGroup/BgPanel/Code/CodeInput").Exists()) GameObject.Find("CoopJoinMatchCanvas/CoopJoinScreen/ButtonGroup/BgPanel/Code/CodeInput").GetComponent<TMP_InputField>().text = gameCode;
            if (GameObject.Find("CoopJoinMatchCanvas/CoopJoinScreen/ButtonGroup/BgPanel/Code/GoButton").Exists()) GameObject.Find("CoopJoinMatchCanvas/CoopJoinScreen/ButtonGroup/BgPanel/Code/GoButton").GetComponent<Button>().onClick.Invoke();
        }
        
        ModByteLoader.OnUpdate();
        RoundSetChanger.OnUpdate();
        // InitialLoadTasks_MoveNext.Update();

        if (Game.instance is null || InGame.instance is null)
            return;

        NotificationMgr.CheckForNotifications();
        RoundSetChanger.EnsureHidden();
        ModSettingHotkey.HandleTowerHotkeys();

#if DEBUG
        if (ExportSelectedTower.JustPressed() &&
            TowerSelectionMenu.instance != null &&
            TowerSelectionMenu.instance.selectedTower != null)
        {
            GameModelExporter.Export(TowerSelectionMenu.instance.selectedTower.tower.towerModel, "selected_tower.json");
        }
#endif
    }

    public override void OnTitleScreen()
    {
        Schedule_InGame_Loaded();
        
        if (File.Exists(MelonEnvironment.ModsDirectory + "/BloonsTD6 Mod Helper/matchCode.json"))
        {
            GameObject.Find("Canvas/ScreenBoxer/TitleScreen/Start").GetComponent<Button>().onClick.Invoke();
            gameCode = File.ReadAllText(MelonEnvironment.ModsDirectory + "/BloonsTD6 Mod Helper/matchCode.json");
            File.Delete(MelonEnvironment.ModsDirectory + "/BloonsTD6 Mod Helper/matchCode.json");
            waitingForMenuOpen = true;
        }
    }

    private void Schedule_GameModel_Loaded()
    {
        TaskScheduler.ScheduleTask(
            () => ModHelper.PerformHook(mod => mod.OnGameModelLoaded(Game.instance.model)),
            () => Game.instance && Game.instance.model != null);
    }

    private void Schedule_InGame_Loaded()
    {
        TaskScheduler.ScheduleTask(() => ModHelper.PerformHook(mod => mod.OnInGameLoaded(InGame.instance)),
            () => InGame.instance && InGame.instance.GetSimulation() != null);
    }

    public void Schedule_GameData_Loaded()
    {
        TaskScheduler.ScheduleTask(() => ModHelper.PerformHook(mod => mod.OnGameDataLoaded(GameData.Instance)),
            () => GameData.Instance != null);
    }

    public override void OnInGameLoaded(InGame inGame)
    {
        inGame.gameObject.AddComponent<Instances>();
    }

    public override void OnLoadSettings(JObject settings)
    {
        var version = settings["Version"];
        if (version == null || version.ToString() != ModHelper.Version)
        {
            ModHelperHttp.DownloadDocumentationXml();
        }
    }

    public override void OnSaveSettings(JObject settings)
    {
        settings["Version"] = ModHelper.Version;
    }

    public override void OnMainMenu()
    {
        if (ModHelper.IsEpic &&
            MelonBase.RegisteredMelons.All(melon => melon.GetName() != EpicCompatibility.RepoName))
        {
            EpicCompatibility.PromptDownloadPlugin();
        }
    }
    
    private static NKMultiGameInterface _nkGi;
    private static ModHelperPanel syncPanel;
    private static ModHelperPanel playerSyncButton;
    private static ModHelperPanel progressPanel;
    private static bool waitingForMenuOpen;
    private static string gameCode;
    private static int modsChanged;

    internal partial class CoopSync : BloonsTD6Mod
    {
        
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
            {
                foreach (var child in token.Children())
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
            GameObject.Find("Canvas/CoopLobbyScreen/progressPanel/Text").GetComponent<NK_TextMeshProUGUI>().text = "Done Downloading/Disabling mods, restarting.";
            File.WriteAllText(MelonEnvironment.ModsDirectory + "/BloonsTD6 Mod Helper/matchCode.json", matchCode);
            ProcessHelper.RestartGame();
        }

        public void UpdatePopup()
        {
            modsChanged++;
            string text = GameObject.Find("Canvas/CoopLobbyScreen/progressPanel/Text").GetComponent<NK_TextMeshProUGUI>().text.Replace((modsChanged - 1).ToString(), modsChanged.ToString(), StringComparison.CurrentCultureIgnoreCase);
            GameObject.Find("Canvas/CoopLobbyScreen/progressPanel/Text").GetComponent<NK_TextMeshProUGUI>().text = text;
        }

        public static void CreateModPopup(List<string> modsToAdd, List<string> modsToRemove)
        {
            string text = "Downloading/Disabling mod " + modsChanged + " out of " + (modsToAdd.Count + modsToRemove.Count) + ".";
            progressPanel = GameObject.Find("Canvas/CoopLobbyScreen").gameObject.AddModHelperPanel(new Info("progressPanel", -2300, -1200, 500)
            {
                Pivot = Vector2.one,
                Anchor = Vector2.one
            });
            var animator = progressPanel.AddComponent<Animator>();
            animator.runtimeAnimatorController = Animations.PopupAnim;
            animator.speed *= .7f;
            progressPanel.AddImage(new Info("backgroundImage", 0, 0, 500), VanillaSprites.BlueBtnLong);
            progressPanel.AddText(new Info("Text", 0, 0, 5000, 100), text, 75);
            GameObject.Find("Canvas/CoopLobbyScreen/progressPanel/backgroundImage").transform.localScale = new Vector3(5, 2, 1);
        }

        public async void AddAndRemoveMods(List<string> modsToAdd, List<string> modsToRemove)
        {
            CreateModPopup(modsToAdd, modsToRemove);
            foreach (var mod in RegisteredMelons.ToList())
            {
                foreach (var modToRemove in modsToRemove)
                {
                    if (mod.Info.Name == modToRemove)
                    {
                        File.Move(mod.MelonAssembly.Assembly.Location, ModHelper.DisabledModsDirectory + "/" + mod.MelonAssembly.Assembly.Location.Substring(MelonEnvironment.ModsDirectory.Length + 1));
                        UpdatePopup();
                    }
                }
            }
            foreach (var mod in ModHelperGithub.Mods)
            {
                foreach (var modToAdd in modsToAdd)
                {
                    if (mod.Name == modToAdd)
                    {
                        await ModHelperGithub.DownloadLatest(mod, true);
                        UpdatePopup();
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
            sb.Append("By clicking ok, it will download added mods off mod browser, disable removed mods, and restart your game.");
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
            _nkGi.SendToPeer(playerNumber, MessageUtils.CreateMessageEx(JsonConvert.SerializeObject(modsData, Formatting.Indented), "ChangeMods"));
        }

        public void PlayerDeclined(int playerNumber)
        {
            Action cancelAction = () =>
            {
                GameObject.Find("Canvas/CoopLobbyScreen/CoopPlayerInfo/PlayerSlot" + playerNumber + "/SyncMods").Destroy();
            };
            Action okAction = () =>
            {
                _nkGi.SendToPeer(playerNumber, MessageUtils.CreateMessageEx(" ", "KickPlayer"));
                GameObject.Find("Canvas/CoopLobbyScreen/CoopPlayerInfo/PlayerSlot" + playerNumber + "/SyncMods").Destroy();
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
                    var modsData = JsonConvert.DeserializeObject<Dictionary<string, List<object>>>(MessageUtils.ReadMessage<string>(message));
                    var modsToAdd = modsData!["modsToAdd"].Cast<string>().ToList();
                    var modsToRemove = modsData["modsToRemove"].Cast<string>().ToList();
                    RequestModChange(modsToAdd, modsToRemove);
                    return true;
                case "ReturnMods":
                    var playerModDataJson = JObject.Parse(MessageUtils.ReadMessage<string>(message));
                    playerNumber = int.Parse(playerModDataJson.GetValue("PlayerNumber")!.ToString());
                    UpdateModDifferences(playerNumber, playerModDataJson);
                    return true;
                case "FindMods":
                    List<MelonMod> loadedMods = RegisteredMelons.ToList();
                    var playersMods = new List<string>();
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
}
