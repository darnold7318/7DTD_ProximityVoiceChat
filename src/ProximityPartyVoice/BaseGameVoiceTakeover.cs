using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace ProximityPartyVoice;

/// <summary>
/// R16 injects the synthetic proximity PTT state at the exact beginning of the
/// stock PartyVoice.Update call. This avoids depending on Unity component update
/// ordering and lets the game's own capture state machine observe a real
/// pressed/held/released sequence.
/// </summary>
internal static class BaseGameVoiceTakeover
{
    static Harmony? harmony;
    static bool initialized;
    static int patchedHeldTargets;
    static int patchedPressedTargets;
    static int patchedReleasedTargets;
    static bool lastNativeFrameHeld;

    public static bool ForcePttHeld { get; private set; }
    public static bool ForcePttWasPressed { get; private set; }
    public static bool ForcePttWasReleased { get; private set; }
    public static bool NativeGatePatched => patchedHeldTargets > 0;

    public static void Initialize()
    {
        if (initialized) return;
        initialized = true;
        try
        {
            harmony = new Harmony("ProximityPartyVoice.NativeCaptureGate.R16");
            PatchDecisionGroup(new[] { "PushToTalkPressed", "IsPushToTalkPressed" }, nameof(HeldPostfix), ref patchedHeldTargets);
            PatchDecisionGroup(new[] { "PushToTalkWasPressed", "WasPushToTalkPressed" }, nameof(PressedPostfix), ref patchedPressedTargets);
            PatchDecisionGroup(new[] { "PushToTalkWasReleased", "WasPushToTalkReleased" }, nameof(ReleasedPostfix), ref patchedReleasedTargets);

            Type? partyVoice = AccessTools.TypeByName("PartyVoice");
            MethodInfo? update = partyVoice == null ? null : AccessTools.Method(partyVoice, "Update");
            if (update != null)
            {
                harmony.Patch(update, prefix: new HarmonyMethod(AccessTools.Method(typeof(BaseGameVoiceTakeover), nameof(PartyVoiceUpdatePrefix))));
                ModLog.Info("R16 patched PartyVoice.Update prefix; synthetic PTT is now injected at the native capture decision point.");
            }
            else ModLog.Warning("R16 could not find PartyVoice.Update.");

            ModLog.Info($"R16 native capture gate active: held={patchedHeldTargets}, pressed={patchedPressedTargets}, released={patchedReleasedTargets}.");
        }
        catch (Exception ex) { ModLog.Warning("R16 native capture-gate initialization failed: " + ex); }
    }

    static void PatchDecisionGroup(string[] logicalNames, string postfixName, ref int count)
    {
        MethodInfo postfix = AccessTools.Method(typeof(BaseGameVoiceTakeover), postfixName);
        foreach (MethodInfo target in FindBooleanDecisionMethods(logicalNames).Distinct())
        {
            harmony!.Patch(target, postfix: new HarmonyMethod(postfix));
            count++;
            ModLog.Info("R16 patched native PTT decision: " + Describe(target));
        }
    }

    // Runs immediately before the stock game processes microphone capture.
    static void PartyVoiceUpdatePrefix()
    {
        bool held = VoiceRuntime.ShouldForceProximityPtt;
        ForcePttHeld = held;
        ForcePttWasPressed = held && !lastNativeFrameHeld;
        ForcePttWasReleased = !held && lastNativeFrameHeld;
        lastNativeFrameHeld = held;
    }

    // Retained for menu close/world exit and for compatibility with existing runtime flow.
    public static void SetForcedPttState(bool held, bool wasPressed)
    {
        ForcePttHeld = held;
        ForcePttWasPressed = wasPressed;
        if (!held && lastNativeFrameHeld) ForcePttWasReleased = true;
    }

    static void HeldPostfix(ref bool __result) { if (ForcePttHeld) __result = true; }
    static void PressedPostfix(ref bool __result) { if (ForcePttWasPressed) __result = true; }
    static void ReleasedPostfix(ref bool __result) { if (ForcePttWasReleased) __result = true; }

    static IEnumerable<MethodInfo> FindBooleanDecisionMethods(IEnumerable<string> logicalNames)
    {
        var names = new HashSet<string>(logicalNames, StringComparer.OrdinalIgnoreCase);
        foreach (string n in logicalNames) names.Add("get_" + n);
        Assembly gameAssembly = typeof(GameManager).Assembly;
        foreach (Type type in SafeGetTypes(gameAssembly))
        {
            string fullName = type.FullName ?? type.Name;
            if (fullName.IndexOf("Voice", StringComparison.OrdinalIgnoreCase) < 0 &&
                fullName.IndexOf("Input", StringComparison.OrdinalIgnoreCase) < 0 &&
                fullName.IndexOf("Action", StringComparison.OrdinalIgnoreCase) < 0) continue;
            MethodInfo[] methods;
            try { methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic); }
            catch { continue; }
            foreach (MethodInfo method in methods)
            {
                if (method.ReturnType != typeof(bool) || method.GetParameters().Length != 0 || method.IsAbstract || method.ContainsGenericParameters) continue;
                if (names.Contains(method.Name)) yield return method;
            }
        }
    }

    static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null)!; }
    }
    static string Describe(MethodInfo method) => (method.DeclaringType?.FullName ?? "<unknown>") + "." + method.Name + "()";

    public static void Shutdown()
    {
        ForcePttHeld = ForcePttWasPressed = ForcePttWasReleased = false;
        lastNativeFrameHeld = false;
        try { harmony?.UnpatchSelf(); } catch { }
        harmony = null;
        patchedHeldTargets = patchedPressedTargets = patchedReleasedTargets = 0;
        initialized = false;
    }
}
