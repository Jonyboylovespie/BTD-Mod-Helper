using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Assets.Scripts.Unity;
using Assets.Scripts.Unity.Analytics;
using BTD_Mod_Helper.Api;
using Il2CppSystem.Threading.Tasks;
using Ninjakiwi.BuildAutomation;
using NinjaKiwi.Common;
using UnityEngine;
using Exception = System.Exception;
using Int32 = Il2CppSystem.Int32;
using Main = Assets.Main.Main;
namespace BTD_Mod_Helper.Patches;

[HarmonyPatch(typeof(Assets.Main.Main._InitialLoadTasks_d__45),
    nameof(Assets.Main.Main._InitialLoadTasks_d__45.MoveNext))]
internal static class InitialLoadTasks_MoveNext
{
    private static List<ModLoadTask> modsTasks;

    private static int modStep;

    private static bool started;

    private static bool finished;

    public static bool Active => started && !finished;

    [HarmonyPrefix]
    private static bool Prefix(Assets.Main.Main._InitialLoadTasks_d__45 __instance)
    {
        if (modsTasks == null)
        {
            modsTasks = new List<ModLoadTask>
            {
                ByteWaitTask.Instance,
                PreLoadResourcesTask.Instance
            };

            // All the tasks for loading mod content
            modsTasks.AddRange(ModHelper.Mods
                .Where(mod => mod.Content.Count > 0)
                .OrderBy(mod => mod.Priority)
                .Select(mod => mod.LoadContentTask));

            // Modders own custom load tasks
            modsTasks.AddRange(ModContent
                .GetContent<ModLoadTask>()
                .OrderBy(task => task.mod.Priority));
        }

        return true;
    }

    [HarmonyPostfix]
    private static void Postfix(Assets.Main.Main._InitialLoadTasks_d__45 __instance)
    {
        var tasks = __instance._tasks_5__3;
        if (tasks == null || MelonMain.UseOldLoading) return;

        if (__instance._taskIdx_5__4 == tasks.Count && !finished)
        {
            started = true;
            __instance.__1__state = -1;
        }

        UpdateLoadingScreen(__instance);
    }

    public static void Update()
    {
        // Allows pressing space bar to switch to synchronously loading, if you're really in a hurry
        if (Input.GetKeyDown(KeyCode.Space))
        {
            finished = true;
        }

        if (started && !finished && Game.instance.model != null)
        {
            if (modStep < modsTasks.Count)
            {
                var task = modsTasks[modStep];

                try
                {
                    if (!task.MoveNext())
                    {
                        modStep++;
                    }
                }
                catch (Exception e)
                {
                    ModHelper.Error($"Mod Load Task {task.Name} Failed");
                    ModHelper.Error(e);
                    modStep++;
                }
            }
            else
            {
                finished = true;
                ModHelper.FallbackToOldLoading = false;
            }
        }
    }

    private static void UpdateLoadingScreen(Assets.Main.Main._InitialLoadTasks_d__45 __instance)
    {
        var tasks = __instance._tasks_5__3;
        var step = started ? tasks.Count : __instance._taskIdx_5__4;
        var loadingScreen = __instance.loadingScreen;

        var total = tasks.Count + modsTasks.Count;
        var moddedStep = Math.Min(modStep, modsTasks.Count - 1);
        var current = started ? step + moddedStep + 1 : step;

        loadingScreen.SetMainText(LocalizationManager.Instance.Format("Loading Step", new[]
        {
            new Int32 {m_value = current}.BoxIl2CppObject(),
            new Int32 {m_value = total}.BoxIl2CppObject()
        }));

        if (started)
        {
            var task = modsTasks[moddedStep];
            loadingScreen.SetProgressBarVisible(task.ShowProgressBar && !finished);
            loadingScreen.SetProgress(task.Progress);
            loadingScreen.SetSubText(task.DisplayName);
            loadingScreen.SetStatusText(task.Description);
        }
    }
}

[HarmonyPatch]
internal static class PreventTaskPatches
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return typeof(AnalyticsManager).GetMethod(nameof(AnalyticsManager.Initialize));
        yield return typeof(NKTimer).GetMethod(nameof(NKTimer.RefreshServerDateTime));
        yield return typeof(SkuSettings).GetMethod(nameof(SkuSettings.RefreshEventsData));
        yield return typeof(SkuSettings).GetMethod(nameof(SkuSettings.Initialise));
        yield return typeof(Main).GetMethod(nameof(Main.CheckVersionAsync));
        yield return typeof(BundleLoader).GetMethod(nameof(BundleLoader.LoadBundlesAsync));
        yield return typeof(Main).GetMethod(nameof(Main.LoadGlobalScene));
    }

    [HarmonyPrefix]
    private static bool Prefix(ref Task __result)
    {
        if (InitialLoadTasks_MoveNext.Active)
        {
            __result = Task.CompletedTask;
            return false;
        }
        return true;
    }
}