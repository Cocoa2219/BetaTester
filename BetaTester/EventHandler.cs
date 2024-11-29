using BetaTester.SS;
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
    }
}