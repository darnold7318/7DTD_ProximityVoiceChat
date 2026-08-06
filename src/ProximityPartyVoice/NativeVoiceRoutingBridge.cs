using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Epic.OnlineServices;
using Epic.OnlineServices.RTCAudio;
using HarmonyLib;

namespace ProximityPartyVoice;

/// <summary>
/// Keeps the existing R15 participant-volume routing only. R18 local microphone
/// control no longer calls EOS directly; it writes PartyVoice.platformPartyVoice
/// .MuteSelf and lets the stock Platform.EOS.Voice setter call UpdateSending.
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

    public static void Initialize()
    {
        if (initialized) return;
        initialized = true;
        try
        {
            harmony = new Harmony("ProximityPartyVoice.ParticipantRouting.R18");
            Type? voiceType = AccessTools.TypeByName("Platform.EOS.Voice");
            MethodInfo? participantChanged = voiceType == null ? null : AccessTools.Method(voiceType, "participantVoiceChanged");
            if (participantChanged != null)
            {
                MethodInfo postfix = AccessTools.Method(typeof(NativeVoiceRoutingBridge), nameof(ParticipantVoiceChangedPostfix));
                harmony.Patch(participantChanged, postfix: new HarmonyMethod(postfix));
                ModLog.Info("R18 patched Platform.EOS.Voice.participantVoiceChanged for participant-volume routing.");
            }
            else
            {
                ModLog.Warning("R18 Platform.EOS.Voice.participantVoiceChanged was not found.");
            }
        }
        catch (Exception ex)
        {
            ModLog.Warning("R18 participant routing initialization failed: " + ex);
        }
    }

    static object? ResolveVoiceInstance()
    {
        lock (Sync)
        {
            if (eosVoice != null) return eosVoice;
        }

        try
        {
            PartyVoice partyVoice = PartyVoice.Instance;
            object? platform = partyVoice.platformPartyVoice;
            if (platform != null && platform.GetType().FullName?.IndexOf("Platform.EOS.Voice", StringComparison.Ordinal) >= 0)
            {
                lock (Sync) eosVoice = platform;
                return platform;
            }
        }
        catch (Exception ex)
        {
            ModLog.Warning("R18 EOS voice instance resolution failed: " + ex.GetBaseException().Message);
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
            ModLog.Info("R18 resolved Epic.OnlineServices.RTCAudio.RTCAudioInterface for participant volume.");
        }
        return typed;
    }

    static ProductUserId? ResolveLocalProductUserId()
    {
        object? voice;
        lock (Sync) voice = eosVoice;
        voice ??= ResolveVoiceInstance();
        if (voice == null) return null;

        foreach (MemberInfo member in voice.GetType().GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            Type? memberType = member is FieldInfo field
                ? field.FieldType
                : member is PropertyInfo property && property.GetIndexParameters().Length == 0
                    ? property.PropertyType
                    : null;
            if (memberType == null || !typeof(ProductUserId).IsAssignableFrom(memberType)) continue;

            try
            {
                return member is FieldInfo f
                    ? f.GetValue(voice) as ProductUserId
                    : ((PropertyInfo)member).GetValue(voice, null) as ProductUserId;
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
            if (first) ModLog.Info($"R18 native participant discovered id={participantText}, room={roomName}.");
            SetParticipantVolume(participant, 1.0f);
        }
        catch (Exception ex)
        {
            ModLog.Warning("R18 participant callback bridge failed: " + ex.GetBaseException().Message);
        }
    }

    static void SetParticipantVolume(ProductUserId participant, float volume)
    {
        RTCAudioInterface? audio;
        string room;
        lock (Sync)
        {
            audio = rtcAudio;
            room = roomName;
        }
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
        ModLog.Info($"R18 UpdateParticipantVolume result={data.ResultCode}, participant={data.ParticipantId}, volume=1.0.");
    }

    public static void Shutdown()
    {
        try { harmony?.UnpatchSelf(); } catch { }
        lock (Sync)
        {
            SeenParticipants.Clear();
            eosVoice = null;
            rtcAudio = null;
            roomName = string.Empty;
        }
        harmony = null;
        initialized = false;
    }

    static object? ReadMember(object? obj, string name)
    {
        if (obj == null) return null;
        Type type = obj.GetType();
        return type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(obj, null)
            ?? type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(obj);
    }

    static string? ReadString(object obj, string name) => ReadMember(obj, name) as string;
}
