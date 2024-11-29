using BetaTester.SS;
using HarmonyLib;
using PluginAPI.Core.Attributes;
using PluginAPI.Enums;
using UserSettings.ServerSpecific;

namespace BetaTester
{
    public class Plugin
    {
        [PluginConfig] public Config Config;

        [PluginPriority(LoadPriority.Highest)]
        [PluginEntryPoint("BetaTester", "1.0.0", "BetaTester", "Cocoa")]
        private void OnEnabled()
        {
            if (!Config.IsEnabled)
            {
                return;
            }

            PluginAPI.Events.EventManager.RegisterEvents(this, new EventHandler());

            SSHandler.Initialize();
        }

        [PluginUnload]
        private void OnDisabled()
        {
            PluginAPI.Events.EventManager.UnregisterEvents(this);

            SSHandler.Dispose();
        }
    }

    [HarmonyPatch(typeof(ServerSpecificSettingsSync), nameof(ServerSpecificSettingsSync.ServerPrevalidateClientResponse))]
    public class PrevalidateResponsePatch
    {
        public static bool Prefix(SSSClientResponse response, ref bool __result)
        {
            __result = true;
            return false;
        }
    }
}