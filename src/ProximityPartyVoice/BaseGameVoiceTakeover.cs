using System;
using HarmonyLib;

namespace ProximityPartyVoice;

/// <summary>
/// Applies the mod's transmit decision at the same managed state used by the
/// stock V3.1 b14 push-to-talk path. ILSpy evidence shows PartyVoice.Update
/// writes IPartyVoice.MuteSelf and Platform.EOS.Voice.MuteSelf is the method
/// that calls EOS RTCAudio.UpdateSending.
/// </summary>
internal static class BaseGameVoiceTakeover
{
    static Harmony? harmony;
    static bool initialized;
    static bool? lastObservedSending;

    public static void Initialize()
    {
        if (initialized) return;
        initialized = true;

        try
        {
            harmony = new Harmony("ProximityPartyVoice.VerifiedCapturePath.R18");
            harmony.Patch(
                AccessTools.Method(typeof(PartyVoice), nameof(PartyVoice.Update)),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(BaseGameVoiceTakeover), nameof(PartyVoiceUpdatePrefix))),
                postfix: new HarmonyMethod(AccessTools.Method(typeof(BaseGameVoiceTakeover), nameof(PartyVoiceUpdatePostfix))));

            ModLog.Info("R18 patched PartyVoice.Update prefix/postfix at the verified V3.1 b14 MuteSelf capture path.");
        }
        catch (Exception ex)
        {
            ModLog.Warning("R18 PartyVoice capture-path patch failed: " + ex);
        }
    }

    static void PartyVoiceUpdatePrefix()
    {
        VoiceRuntime.EnsureVoiceDecisionCurrent();
    }

    static void PartyVoiceUpdatePostfix(PartyVoice __instance)
    {
        try
        {
            if (!__instance.platformPartyVoiceInitialized ||
                __instance.localPlayer == null ||
                !__instance.platformPartyVoice.InLobby)
            {
                ObserveSending(false, "not-in-lobby");
                return;
            }

            if (!VoiceHelpers.PlatformVoiceEnabled || VoiceRuntime.IsSelectorOpen)
            {
                __instance.platformPartyVoice.MuteSelf = true;
            }
            else if (VoiceRuntime.ShouldTransmit)
            {
                __instance.platformPartyVoice.MuteSelf = false;
            }

            ObserveSending(!__instance.platformPartyVoice.MuteSelf,
                VoiceRuntime.ProximityOpenMic ? "proximity" :
                VoiceRuntime.RadioTransmitRequested ? "radio" : "stock-ptt");
        }
        catch (Exception ex)
        {
            ModLog.Warning("R18 verified capture-path application failed: " + ex.GetBaseException().Message);
        }
    }

    static void ObserveSending(bool sending, string source)
    {
        if (lastObservedSending == sending) return;
        lastObservedSending = sending;
        ModLog.Info($"R18 verified MuteSelf state: {(sending ? "SENDING" : "MUTED")} (source={source}).");
    }

    public static void Shutdown()
    {
        try { harmony?.UnpatchSelf(); } catch { }
        harmony = null;
        lastObservedSending = null;
        initialized = false;
    }
}
