using HarmonyLib;
using KMod;
using PeterHan.PLib.Core;
using PeterHan.PLib.Options;

namespace StoringWhat
{
    public sealed class StoringWhatMod : UserMod2
    {
        public override void OnLoad(Harmony harmony)
        {
            base.OnLoad(harmony); // applies every [HarmonyPatch] in this assembly
            PUtil.InitLibrary(false);
            new POptions().RegisterOptions(this, typeof(StoringWhatOptions)); // "Options" button in the Mods screen
            Debug.Log("[StoringWhat] loaded");
        }
    }

    /// <summary>A save is about to load: re-read options before its buildings spawn.</summary>
    [HarmonyPatch(typeof(Game), "OnPrefabInit")]
    internal static class Game_OnPrefabInit_Patch
    {
        private static void Prefix()
        {
            StoringWhatOptions.Reload();
        }
    }

    /// <summary>Every finished building (built or loaded from a save): attach the display if it stores things.</summary>
    [HarmonyPatch(typeof(BuildingComplete), "OnSpawn")]
    internal static class BuildingComplete_OnSpawn_Patch
    {
        private static void Postfix(BuildingComplete __instance)
        {
            if (__instance != null) StoredItemsDisplay.TryAttach(__instance.gameObject);
        }
    }
}
