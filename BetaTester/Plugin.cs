using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using BetaTester.Features;
using HarmonyLib;
using Mirror;
using PluginAPI.Core.Attributes;
using PluginAPI.Enums;
using UserSettings.ServerSpecific;
using VoiceChat.Codec;
using VoiceChat.Networking;

namespace BetaTester;

public class Plugin
{
    [PluginConfig] public Config Config;

    public Harmony Harmony;

    [PluginPriority(LoadPriority.Highest)]
    [PluginEntryPoint("BetaTester", "1.0.0", "BetaTester", "Cocoa")]
    private void OnEnabled()
    {
        if (!Config.IsEnabled)
        {
            return;
        }

        PluginAPI.Events.EventManager.RegisterEvents(this, new EventHandler());

        Harmony = new Harmony("com.github.cocoa.BetaTester." + DateTime.Now.Ticks);
        Harmony.PatchAll();

        SSHandler.Initialize();

        VoiceTransceiver.OnVoiceMessageReceiving += OnReceivingVoiceMessage;
    }

    private OpusDecoder _decoder = new();
    private float[] _samples = new float[24000];

    private void OnReceivingVoiceMessage(VoiceMessage message, ReferenceHub hub)
    {
        var length = _decoder.Decode(message.Data, message.Data.Length, _samples);

    }

    [PluginUnload]
    private void OnDisabled()
    {
        VoiceTransceiver.OnVoiceMessageReceiving -= OnReceivingVoiceMessage;

        PluginAPI.Events.EventManager.UnregisterEvents(this);

        SSHandler.Dispose();

        Harmony.UnpatchAll();
        Harmony = null;
    }
}

[HarmonyPatch(typeof(ServerSpecificSettingsSync), nameof(ServerSpecificSettingsSync.ServerPrevalidateClientResponse))]
public class PrevalidateResponsePatch
{
    public static bool Prefix(SSSClientResponse msg, ref bool __result)
    {
        var elements = SSHandler.PageManager.Pages.SelectMany(x => x.Value.Elements).ToList();

        __result = elements.Any(x =>
            x.SettingId == msg.Id && x.Base.GetType() == msg.SettingType);

        return false;
    }
}

[HarmonyPatch(typeof(ServerSpecificSettingBase), nameof(ServerSpecificSettingBase.OriginalDefinition), MethodType.Getter)]
public class OriginalDefinition
{
    public static bool Prefix(ServerSpecificSettingBase __instance, ref ServerSpecificSettingBase __result)
    {
        var elements = SSHandler.PageManager.Pages.SelectMany(x => x.Value.Elements).ToList();

        __result = elements.FirstOrDefault(x =>
                x.SettingId == __instance.SettingId && x.Base.GetType() == __instance.GetType())
            ?.Base;

        return false;
    }
}

[HarmonyPatch(typeof(ServerSpecificSettingsSync), nameof(ServerSpecificSettingsSync.ServerDeserializeClientResponse))]
public class ServerDeserializeClientResponse
{
    public static bool Prefix(ReferenceHub sender, ServerSpecificSettingBase setting, NetworkReaderPooled reader)
    {
        if (setting.ResponseMode != ServerSpecificSettingBase.UserResponseMode.None)
        {
            var readerCopy = NetworkReaderPool.Get(reader.buffer);

            readerCopy.Position = reader.Position;

            SSHandler.PageManager.OnUserInputReceived(sender, setting, readerCopy);
        }

        return true;
    }
}