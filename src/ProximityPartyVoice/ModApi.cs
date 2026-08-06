namespace ProximityPartyVoice;

public sealed class ModApi : IModApi
{
    static string modPath = string.Empty;

    public void InitMod(Mod mod)
    {
        modPath = mod.Path;
        ModLog.Info("Loading ProximityPartyVoice 1.0.15 R18: ILSpy-verified PartyVoice.Update -> Platform.EOS.Voice.MuteSelf capture path for V3.1 b14, proximity open mic, hold-V radio PTT, and channel selection.");
        ModEvents.GameStartDone.RegisterHandler(OnGameStartDone);
    }

    static void OnGameStartDone(ref ModEvents.SGameStartDoneData data) => VoiceRuntime.Create(modPath);
}
