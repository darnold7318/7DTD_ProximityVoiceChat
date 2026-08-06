using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Epic.OnlineServices;
using Epic.OnlineServices.RTCAudio;
using HarmonyLib;

namespace ProximityPartyVoice;

/// <summary>
/// R15 obtains the game's existing EOS voice/RTC objects through the PartyVoice wrapper,
/// but calls the EOS RTCAudio API with its real compile-time types. This removes the
/// reflection invocation/callback mismatch present in R14.
/// </summary>
internal static class NativeVoiceRoutingBridge
{
    static readonly object Sync = new();
    static readonly HashSet<string> SeenParticipants = new(StringComparer.Ordinal);
    static Harmony? harmony;
    static bool initialized;
    static object? eosVoice;
    static RTCAudioInterface? rtcAudio;
    static string roomName = string.Empty;
    static bool? lastSendingState;
    static float nextResolveAttempt;

    public static void Initialize()
    {
        if (initialized) return;
        initialized = true;
        try
        {
            harmony = new Harmony("NoFriendlyFire.ProximityPartyVoice.NativeRouting.R15");
            Type? voiceType = AccessTools.TypeByName("Platform.EOS.Voice");
            MethodInfo? participantChanged = voiceType == null ? null : AccessTools.Method(voiceType, "participantVoiceChanged");
            if (participantChanged != null)
            {
                MethodInfo postfix = AccessTools.Method(typeof(NativeVoiceRoutingBridge), nameof(ParticipantVoiceChangedPostfix));
                harmony.Patch(participantChanged, postfix: new HarmonyMethod(postfix));
                ModLog.Info("R15: patched Platform.EOS.Voice.participantVoiceChanged for typed participant routing.");
            }
            else
            {
                ModLog.Warning("R15: Platform.EOS.Voice.participantVoiceChanged was not found.");
            }
        }
        catch (Exception ex)
        {
            ModLog.Warning("R15 native routing initialization failed: " + ex);
        }
    }

    public static bool TrySetLocalSending(bool sending)
    {
        if (lastSendingState == sending && rtcAudio != null) return true;
        object? voice = ResolveVoiceInstance();
        if (voice == null) return false;

        try
        {
            RTCAudioInterface? audio = ResolveRtcAudio(voice);
            string room = ReadString(voice, "roomName") ?? string.Empty;
            ProductUserId? localUser = ResolveLocalProductUserId();
            if (audio == null || string.IsNullOrEmpty(room) || localUser == null)
            {
                ModLog.Warning($"R15 typed EOS sending unavailable: audio={(audio != null)}, room={!string.IsNullOrEmpty(room)}, localUser={(localUser != null)}.");
                return TrySetMuteSelfFallback(voice, sending);
            }

            var options = new UpdateSendingOptions
            {
                LocalUserId = localUser,
                RoomName = room,
                AudioStatus = sending ? RTCAudioStatus.Enabled : RTCAudioStatus.Disabled
            };

            audio.UpdateSending(ref options, null, OnUpdateSendingResult);
            lastSendingState = sending;
            ModLog.Info($"R15: typed EOS microphone sending requested {(sending ? "ENABLED" : "DISABLED")} for room {room}.");
            return true;
        }
        catch (Exception ex)
        {
            ModLog.Warning("R15 typed EOS UpdateSending failed: " + ex.GetBaseException().Message);
            return TrySetMuteSelfFallback(voice, sending);
        }
    }

    static void OnUpdateSendingResult(ref UpdateSendingCallbackInfo data)
    {
        ModLog.Info("R15: typed EOS UpdateSending result=" + data.ResultCode + ".");
    }

    static bool TrySetMuteSelfFallback(object voice, bool sending)
    {
        try
        {
            PropertyInfo? mute = voice.GetType().GetProperty("MuteSelf", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (mute?.CanWrite == true && mute.PropertyType == typeof(bool))
            {
                mute.SetValue(voice, !sending, null);
                lastSendingState = sending;
                ModLog.Info($"R15 fallback: MuteSelf set to {!sending}.");
                return true;
            }

            FieldInfo? field = voice.GetType().GetField("muteSelf", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field?.FieldType == typeof(bool))
            {
                field.SetValue(voice, !sending);
                lastSendingState = sending;
                ModLog.Info($"R15 fallback: muteSelf field set to {!sending}.");
                return true;
            }
        }
        catch (Exception ex)
        {
            ModLog.Warning("R15 MuteSelf fallback failed: " + ex.GetBaseException().Message);
        }
        return false;
    }

    static object? ResolveVoiceInstance()
    {
        lock (Sync)
        {
            if (eosVoice != null) return eosVoice;
        }

        if (UnityEngine.Time.unscaledTime < nextResolveAttempt) return null;
        nextResolveAttempt = UnityEngine.Time.unscaledTime + 1f;

        try
        {
            Type? partyVoiceType = AccessTools.TypeByName("PartyVoice");
            if (partyVoiceType != null)
            {
                const BindingFlags sf = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                object? partyVoice = partyVoiceType.GetProperty("Instance", sf)?.GetValue(null, null)
                                  ?? partyVoiceType.GetField("Instance", sf)?.GetValue(null)
                                  ?? partyVoiceType.GetProperty("instance", sf)?.GetValue(null, null)
                                  ?? partyVoiceType.GetField("instance", sf)?.GetValue(null);
                object? platform = ReadMember(partyVoice, "platformPartyVoice");
                if (platform != null && platform.GetType().FullName?.IndexOf("Platform.EOS.Voice", StringComparison.Ordinal) >= 0)
                {
                    lock (Sync) eosVoice = platform;
                    ModLog.Info("R15: resolved Platform.EOS.Voice through PartyVoice.platformPartyVoice.");
                    return platform;
                }
            }
        }
        catch (Exception ex)
        {
            ModLog.Warning("R15 EOS voice instance resolution failed: " + ex.GetBaseException().Message);
        }
        return null;
    }

    static RTCAudioInterface? ResolveRtcAudio(object voice)
    {
        lock (Sync)
        {
            if (rtcAudio != null) return rtcAudio;
        }

        object? rtc = ReadMember(voice, "rtcInterface");
        if (rtc == null) return null;
        MethodInfo? getAudio = rtc.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(m => (m.Name == "GetAudioInterface" || m.Name == "GetAudioInterfaceHandle") && m.GetParameters().Length == 0);
        object? raw = getAudio?.Invoke(rtc, null) ?? ReadMember(rtc, "AudioInterface") ?? ReadMember(rtc, "RTCAudioInterface");
        RTCAudioInterface? typed = raw as RTCAudioInterface;
        if (typed != null)
        {
            lock (Sync) rtcAudio = typed;
            ModLog.Info("R15: resolved strongly typed Epic.OnlineServices.RTCAudio.RTCAudioInterface.");
        }
        return typed;
    }

    static ProductUserId? ResolveLocalProductUserId()
    {
        object? voice;
        lock (Sync) voice = eosVoice;
        if (voice == null) return null;
        foreach (MemberInfo m in voice.GetType().GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            Type? memberType = m is FieldInfo f ? f.FieldType : m is PropertyInfo p && p.GetIndexParameters().Length == 0 ? p.PropertyType : null;
            if (memberType == null || !typeof(ProductUserId).IsAssignableFrom(memberType)) continue;
            try
            {
                return m is FieldInfo ff ? ff.GetValue(voice) as ProductUserId : ((PropertyInfo)m).GetValue(voice, null) as ProductUserId;
            }
            catch { }
        }
        return null;
    }

    static void ParticipantVoiceChangedPostfix(object __instance, object _data)
    {
        try
        {
            lock (Sync)
            {
                eosVoice = __instance;
                roomName = ReadString(__instance, "roomName") ?? string.Empty;
            }
            ResolveRtcAudio(__instance);

            ProductUserId? participant = ReadMember(_data, "ParticipantId") as ProductUserId
                                      ?? ReadMember(_data, "ParticipantID") as ProductUserId
                                      ?? ReadMember(_data, "ParticipantUserId") as ProductUserId
                                      ?? ReadMember(_data, "ParticipantUserID") as ProductUserId;
            if (participant == null) return;

            string participantText = participant.ToString();
            bool first;
            lock (Sync) first = SeenParticipants.Add(participantText);
            if (first) ModLog.Info($"R15: native participant discovered id={participantText}, room={roomName}.");
            SetParticipantVolume(participant, 1.0f);
        }
        catch (Exception ex)
        {
            ModLog.Warning("R15 participant callback bridge failed: " + ex.GetBaseException().Message);
        }
    }

    static void SetParticipantVolume(ProductUserId participant, float volume)
    {
        RTCAudioInterface? audio;
        string room;
        lock (Sync) { audio = rtcAudio; room = roomName; }
        ProductUserId? local = ResolveLocalProductUserId();
        if (audio == null || local == null || string.IsNullOrEmpty(room)) return;

        var options = new UpdateParticipantVolumeOptions
        {
            LocalUserId = local,
            RoomName = room,
            ParticipantId = participant,
            Volume = volume
        };
        audio.UpdateParticipantVolume(ref options, null, OnParticipantVolumeResult);
    }

    static void OnParticipantVolumeResult(ref UpdateParticipantVolumeCallbackInfo data)
    {
        ModLog.Info($"R15: typed UpdateParticipantVolume result={data.ResultCode}, participant={data.ParticipantId}, volume=1.0.");
    }

    public static void Shutdown()
    {
        try { TrySetLocalSending(false); } catch { }
        try { harmony?.UnpatchSelf(); } catch { }
        lock (Sync)
        {
            SeenParticipants.Clear();
            eosVoice = null;
            rtcAudio = null;
            roomName = string.Empty;
            lastSendingState = null;
        }
        initialized = false;
    }

    static object? ReadMember(object? obj, string name)
    {
        if (obj == null) return null;
        Type t = obj.GetType();
        return t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(obj, null)
            ?? t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(obj);
    }

    static string? ReadString(object obj, string name) => ReadMember(obj, name) as string;
}
