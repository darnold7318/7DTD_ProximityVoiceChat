using UnityEngine;

namespace ProximityPartyVoice;

internal static class ModLog
{
    private const string Prefix = "[ProximityPartyVoice] ";

    public static void Info(string message) => Debug.Log(Prefix + message);
    public static void Warning(string message) => Debug.LogWarning(Prefix + message);
    public static void Error(string message) => Debug.LogError(Prefix + message);
}
