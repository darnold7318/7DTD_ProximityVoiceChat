using UnityEngine;

namespace ProximityPartyVoice;

[DefaultExecutionOrder(-32000)]
public sealed class VoiceRuntime : MonoBehaviour
{
    public static VoiceRuntime? Instance { get; private set; }

    public static bool ShouldTransmit =>
        Instance != null && Instance.hadLocalPlayer && !Instance.hud.SelectorOpen && Instance.transmitting;

    public static bool IsSelectorOpen => Instance != null && Instance.hud.SelectorOpen;
    public static bool ProximityOpenMic => Instance != null && Instance.proximityOpenMic;
    public static bool RadioTransmitRequested => Instance != null && Instance.radioTransmitRequested;

    VoiceSettings settings = null!;
    VoiceHud hud = null!;
    bool toggled;
    bool transmitting;
    bool radioTransmitRequested;
    float lastTap = -10f;
    int channel;
    bool hadLocalPlayer;
    bool escapeWasHeld;
    bool proximityOpenMic;
    int lastContinuousDecisionFrame = -1;

    public static void Create(string modPath)
    {
        if (Instance != null) return;
        var go = new GameObject("ProximityPartyVoice.NativeRuntime");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<VoiceRuntime>();
        Instance.Initialize(modPath);
    }

    void Initialize(string path)
    {
        settings = VoiceSettings.Load(path);
        channel = settings.DefaultChannel;
        hud = new VoiceHud(settings);
        NativeVoiceRoutingBridge.Initialize();
        BaseGameVoiceTakeover.Initialize();
        ModLog.Info("R18 active: ILSpy-verified PartyVoice.Update -> Platform.EOS.Voice.MuteSelf capture path, direct B14 player enumeration, proximity open mic, and hold-V radio PTT.");
    }

    void Update()
    {
        LocalVoiceState local = GameAdapter.Local();
        bool hasLocalPlayer = local.EntityId >= 0;

        if (!hasLocalPlayer)
        {
            if (hadLocalPlayer)
            {
                if (hud.SelectorOpen)
                    CloseSelector();

                ResetTransmitState();
                toggled = false;
                escapeWasHeld = false;
                ModLog.Info("Local player unavailable; HUD and voice input suspended until a world is active.");
            }

            hadLocalPlayer = false;
            return;
        }

        hadLocalPlayer = true;

        if (hud.SelectorOpen)
            Input.ResetInputAxes();

        HandleInputEdges();
        RefreshContinuousTransmitState();
        GameUiInputBridge.MaintainModalCursor();

        if (hud.SelectorOpen)
            Input.ResetInputAxes();
    }

    void HandleInputEdges()
    {
        bool escapeHeld = Input.GetKey(KeyCode.Escape);
        bool escapePressed = Input.GetKeyDown(KeyCode.Escape) || (escapeHeld && !escapeWasHeld);
        escapeWasHeld = escapeHeld;

        if (hud.SelectorOpen && escapePressed)
        {
            CloseSelector();
            return;
        }

        if (!Input.GetKeyDown(settings.InputKey)) return;

        float now = Time.unscaledTime;
        if ((now - lastTap) * 1000f <= settings.DoubleTapWindowMs)
        {
            hud.ToggleSelector();
            lastTap = -10f;
            if (hud.SelectorOpen || settings.SuppressTransmitOnDoubleTap)
                ResetTransmitState();
            return;
        }

        lastTap = now;
        if (settings.InputMode == TransmitMode.Toggle)
            toggled = !toggled;
    }

    void RefreshContinuousTransmitState()
    {
        lastContinuousDecisionFrame = Time.frameCount;

        if (!hadLocalPlayer || hud.SelectorOpen)
        {
            ResetTransmitState();
            return;
        }

        radioTransmitRequested = settings.RadioEnabled &&
            (settings.InputMode == TransmitMode.PushToTalk
                ? Input.GetKey(settings.InputKey)
                : toggled);

        proximityOpenMic = settings.ProximityEnabled &&
            GameAdapter.AnyOtherPlayerWithin(settings.FadeEndMeters);

        transmitting = radioTransmitRequested || proximityOpenMic;
    }

    public static void EnsureVoiceDecisionCurrent()
    {
        VoiceRuntime? runtime = Instance;
        if (runtime == null || !runtime.hadLocalPlayer) return;
        if (runtime.lastContinuousDecisionFrame == Time.frameCount) return;
        runtime.RefreshContinuousTransmitState();
    }

    void ResetTransmitState()
    {
        transmitting = false;
        radioTransmitRequested = false;
        proximityOpenMic = false;
        lastContinuousDecisionFrame = Time.frameCount;
    }

    void OnGUI()
    {
        if (GameAdapter.Local().EntityId >= 0)
        {
            if (hud?.SelectorOpen == true)
                GameUiInputBridge.MaintainModalCursor();

            hud?.Draw(channel, transmitting);
            if (hud?.SelectorOpen == true)
            {
                GameUiInputBridge.MaintainModalCursor();
                Input.ResetInputAxes();
            }
        }
    }

    void LateUpdate()
    {
        if (hud?.SelectorOpen == true)
        {
            GameUiInputBridge.MaintainModalCursor();
            Input.ResetInputAxes();
        }
    }

    public void SelectChannel(int value)
    {
        channel = Mathf.Clamp(value, 1, settings.ChannelCount);
        CloseSelector();
        ModLog.Info("Native voice channel selected: CH" + channel.ToString("00"));
    }

    public void CloseSelector()
    {
        if (!hud.SelectorOpen) return;
        hud.SetSelectorOpen(false);
        ResetTransmitState();
        Input.ResetInputAxes();
        ModLog.Info("R18 channel selector closed.");
    }

    void OnDestroy()
    {
        ResetTransmitState();
        toggled = false;
        hud?.SetSelectorOpen(false);
        BaseGameVoiceTakeover.Shutdown();
        NativeVoiceRoutingBridge.Shutdown();
        Instance = null;
        ModLog.Info("R18 native routing runtime cleaned up.");
    }
}
