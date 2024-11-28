using BetaTester.SS;
using PluginAPI.Core.Attributes;
using PluginAPI.Enums;

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

            SSHandler.Initialize();
        }

        [PluginUnload]
        private void OnDisabled()
        {
            SSHandler.Dispose();
        }
    }
}