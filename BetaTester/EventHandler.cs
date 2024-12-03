using Achievements;
using BetaTester.Features;
using PluginAPI.Core;
using PluginAPI.Core.Attributes;
using PluginAPI.Enums;

namespace BetaTester
{
    public class EventHandler
    {
        [PluginEvent(ServerEventType.PlayerJoined)]
        public void OnPlayerJoined(Player e)
        {
            SSHandler.OnJoin(e.ReferenceHub);
        }

        [PluginEvent(ServerEventType.PlayerLeft)]
        public void OnPlayerLeft(Player e)
        {
            SSHandler.OnLeave(e.ReferenceHub);
        }
    }
}