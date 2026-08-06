namespace ProximityPartyVoice;

public sealed class ModApi : IModApi
{
    static string modPath = string.Empty;
    public void InitMod(Mod mod)
    {
        modPath = mod.Path;
        ModLog.Info("Loading ProximityPartyVoice 1.0.14 R17: direct native SetPushToTalkActive/MuteSelf capture-state control, strongly typed EOS RTCAudio routing, proximity open mic, hold-V radio PTT, ten radio channels, and double-tap-V channel selection for V3.1 B14.");
        ModEvents.GameStartDone.RegisterHandler(OnGameStartDone);
    }
    static void OnGameStartDone(ref ModEvents.SGameStartDoneData data) => VoiceRuntime.Create(modPath);
}
