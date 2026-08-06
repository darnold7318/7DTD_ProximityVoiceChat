using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace ProximityPartyVoice;

/// <summary>
/// R17 drives the stock managed capture state directly. Assembly-CSharp B14 exposes
/// SetPushToTalkActive and MuteSelf in the PartyVoice / Platform.EOS.Voice pipeline.
/// UpdateSending alone only changes EOS authorization; it does not necessarily move
/// the game's higher-level capture state machine into its talking state.
/// </summary>
internal static class NativeCaptureStateBridge
{
    static object? partyVoice;
    static object? platformVoice;
    static MethodInfo? partySetPtt;
    static MethodInfo? platformSetPtt;
    static PropertyInfo? platformMuteSelf;
    static FieldInfo? platformMuteSelfField;
    static bool? lastRequested;
    static float nextResolveAttempt;
    static bool loggedDiscovery;

    public static bool SetCaptureActive(bool active)
    {
        if (!Resolve()) return false;
        if (lastRequested == active) return true;

        bool touched = false;
        var errors = new List<string>();

        touched |= TryInvokeBool(partyVoice, partySetPtt, active, errors);
        touched |= TryInvokeBool(platformVoice, platformSetPtt, active, errors);

        // MuteSelf=false is the managed EOS voice object's explicit local-send state.
        // Apply it even when RTCAudio.UpdateSending succeeds; R15 only used this as a
        // failure fallback, which left the managed capture gate closed.
        try
        {
            if (platformVoice != null && platformMuteSelf?.CanWrite == true)
            {
                platformMuteSelf.SetValue(platformVoice, !active, null);
                touched = true;
            }
            else if (platformVoice != null && platformMuteSelfField != null)
            {
                platformMuteSelfField.SetValue(platformVoice, !active);
                touched = true;
            }
        }
        catch (Exception ex)
        {
            errors.Add("MuteSelf: " + ex.GetBaseException().Message);
        }

        if (touched)
        {
            lastRequested = active;
            ModLog.Info($"R17 native capture state {(active ? "ACTIVE" : "INACTIVE")}: SetPushToTalkActive + MuteSelf applied.");
            return true;
        }

        if (errors.Count > 0)
            ModLog.Warning("R17 native capture state failed: " + string.Join(" | ", errors));
        else
            ModLog.Warning("R17 native capture state unavailable: no writable capture controls were resolved.");
        return false;
    }

    static bool TryInvokeBool(object? instance, MethodInfo? method, bool value, List<string> errors)
    {
        if (method == null) return false;
        try
        {
            method.Invoke(method.IsStatic ? null : instance, new object[] { value });
            return true;
        }
        catch (Exception ex)
        {
            errors.Add(Describe(method) + ": " + ex.GetBaseException().Message);
            return false;
        }
    }

    static bool Resolve()
    {
        if (partyVoice != null && platformVoice != null &&
            (partySetPtt != null || platformSetPtt != null || platformMuteSelf != null || platformMuteSelfField != null))
            return true;

        if (UnityEngine.Time.unscaledTime < nextResolveAttempt) return false;
        nextResolveAttempt = UnityEngine.Time.unscaledTime + 1f;

        try
        {
            Type? partyType = AccessTools.TypeByName("PartyVoice");
            if (partyType == null) return false;

            const BindingFlags allStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            const BindingFlags allInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            partyVoice = partyType.GetProperty("Instance", allStatic)?.GetValue(null, null)
                      ?? partyType.GetField("Instance", allStatic)?.GetValue(null)
                      ?? partyType.GetProperty("instance", allStatic)?.GetValue(null, null)
                      ?? partyType.GetField("instance", allStatic)?.GetValue(null);
            if (partyVoice == null) return false;

            platformVoice = ReadMember(partyVoice, "platformPartyVoice");
            partySetPtt = FindBoolSetter(partyType, "SetPushToTalkActive");

            if (platformVoice != null)
            {
                Type platformType = platformVoice.GetType();
                platformSetPtt = FindBoolSetter(platformType, "SetPushToTalkActive");
                platformMuteSelf = platformType.GetProperty("MuteSelf", allInstance);
                platformMuteSelfField = platformType.GetField("muteSelf", allInstance);
            }

            if (!loggedDiscovery)
            {
                loggedDiscovery = true;
                ModLog.Info("R17 capture controls resolved: " +
                    $"partySetPtt={Describe(partySetPtt)}, " +
                    $"platformType={platformVoice?.GetType().FullName ?? "<null>"}, " +
                    $"platformSetPtt={Describe(platformSetPtt)}, " +
                    $"MuteSelfProperty={(platformMuteSelf != null)}, muteSelfField={(platformMuteSelfField != null)}.");
            }

            return platformVoice != null &&
                   (partySetPtt != null || platformSetPtt != null || platformMuteSelf != null || platformMuteSelfField != null);
        }
        catch (Exception ex)
        {
            ModLog.Warning("R17 capture-control resolution failed: " + ex.GetBaseException().Message);
            return false;
        }
    }

    static MethodInfo? FindBoolSetter(Type type, string name)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        return type.GetMethods(flags).FirstOrDefault(m =>
        {
            if (!string.Equals(m.Name, name, StringComparison.Ordinal)) return false;
            ParameterInfo[] p = m.GetParameters();
            return p.Length == 1 && p[0].ParameterType == typeof(bool);
        });
    }

    static object? ReadMember(object obj, string name)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        Type type = obj.GetType();
        return type.GetProperty(name, flags)?.GetValue(obj, null)
            ?? type.GetField(name, flags)?.GetValue(obj);
    }

    static string Describe(MethodInfo? method) => method == null
        ? "<none>"
        : (method.DeclaringType?.FullName ?? "<unknown>") + "." + method.Name + "(Boolean)";

    public static void Reset()
    {
        try { SetCaptureActive(false); } catch { }
        partyVoice = null;
        platformVoice = null;
        partySetPtt = null;
        platformSetPtt = null;
        platformMuteSelf = null;
        platformMuteSelfField = null;
        lastRequested = null;
        nextResolveAttempt = 0f;
        loggedDiscovery = false;
    }
}
