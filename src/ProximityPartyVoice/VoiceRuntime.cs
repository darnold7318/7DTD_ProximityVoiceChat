using UnityEngine;

namespace ProximityPartyVoice;

[DefaultExecutionOrder(-32000)]
public sealed class VoiceRuntime : MonoBehaviour
{
    public static VoiceRuntime? Instance { get; private set; }
    public static bool ShouldForceProximityPtt => Instance != null && Instance.hadLocalPlayer && !Instance.hud.SelectorOpen && Instance.proximityOpenMic;
    VoiceSettings settings = null!;
    VoiceHud hud = null!;
    bool toggled;
    bool transmitting;
    float lastTap = -10f;
    int channel;
    bool hadLocalPlayer;
    bool escapeWasHeld;
    bool proximityOpenMic;
    bool priorProximityOpenMic;

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
        ModLog.Info("R17 active: direct SetPushToTalkActive/MuteSelf capture-state control, typed EOS routing, proximity open mic, radio channels, hold V radio PTT, and double-tap V channel selection.");
    }

    void Update()
    {
        LocalVoiceState local = GameAdapter.Local();
        bool hasLocalPlayer = local.EntityId >= 0;

        // A joining client can spend much longer than 1.5 seconds without a
        // primary player while the world is loading. Keep the persistent runtime
        // alive and simply remain inactive until the local player exists.
        if (!hasLocalPlayer)
        {
            if (hadLocalPlayer)
            {
                // The player left a world. Release modal/input state immediately,
                // but retain the runtime so a later world join works without a
                // second initialization pass.
                if (hud.SelectorOpen)
                    CloseSelector();

                transmitting = false;
                proximityOpenMic = false;
                priorProximityOpenMic = false;
                BaseGameVoiceTakeover.SetForcedPttState(false, false);
                NativeCaptureStateBridge.SetCaptureActive(false);
                NativeVoiceRoutingBridge.TrySetLocalSending(false);
                toggled = false;
                escapeWasHeld = false;
                ModLog.Info("Local player unavailable; HUD and voice input suspended until a world is active.");
            }

            hadLocalPlayer = false;
            return;
        }

        hadLocalPlayer = true;

        // This component executes before the game's camera/input consumers.
        // Clear legacy mouse axes first while modal so look never reaches the player.
        if (hud.SelectorOpen)
            Input.ResetInputAxes();

        HandleInput();
        GameUiInputBridge.MaintainModalCursor();

        if (hud.SelectorOpen)
            Input.ResetInputAxes();
    }

    void HandleInput()
    {
        bool escapeHeld = Input.GetKey(KeyCode.Escape);
        bool escapePressed = Input.GetKeyDown(KeyCode.Escape) || (escapeHeld && !escapeWasHeld);
        escapeWasHeld = escapeHeld;

        if (hud.SelectorOpen && escapePressed)
        {
            CloseSelector();
            return;
        }

        bool down = Input.GetKeyDown(settings.InputKey);
        if (down)
        {
            float now = Time.unscaledTime;
            if ((now - lastTap) * 1000f <= settings.DoubleTapWindowMs)
            {
                hud.ToggleSelector();
                lastTap = -10f;
                if (settings.SuppressTransmitOnDoubleTap) transmitting = false;
                return;
            }
            lastTap = now;
            if (settings.InputMode == TransmitMode.Toggle) toggled = !toggled;
        }
        bool radioPtt = settings.InputMode == TransmitMode.PushToTalk ? Input.GetKey(settings.InputKey) : toggled;
        proximityOpenMic = settings.ProximityEnabled && GameAdapter.AnyOtherPlayerWithin(settings.FadeEndMeters);
        bool proximityJustActivated = proximityOpenMic && !priorProximityOpenMic;
        priorProximityOpenMic = proximityOpenMic;

        // Do not suppress or replace PartyVoice. Extend the exact boolean input
        // the stock voice code reads, allowing it to own capture and EOS sending.
        bool forceNativePtt = !hud.SelectorOpen && proximityOpenMic;
        BaseGameVoiceTakeover.SetForcedPttState(forceNativePtt, forceNativePtt && proximityJustActivated);

        transmitting = !hud.SelectorOpen && (radioPtt || proximityOpenMic);

        // R14: use the exact EOS RTCAudio UpdateSending path with the corrected
        // by-reference options type. R13 remains as a fallback so the stock game
        // can still observe the synthetic PTT state.
        NativeCaptureStateBridge.SetCaptureActive(transmitting);
        NativeVoiceRoutingBridge.TrySetLocalSending(transmitting);
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
        transmitting = false;
        proximityOpenMic = false;
        priorProximityOpenMic = false;
        BaseGameVoiceTakeover.SetForcedPttState(false, false);
        NativeCaptureStateBridge.SetCaptureActive(false);
        NativeVoiceRoutingBridge.TrySetLocalSending(false);
        Input.ResetInputAxes();
        ModLog.Info("R9 channel selector closed.");
    }

    void OnDestroy()
    {
        transmitting = false;
        proximityOpenMic = false;
        priorProximityOpenMic = false;
        BaseGameVoiceTakeover.SetForcedPttState(false, false);
        NativeCaptureStateBridge.SetCaptureActive(false);
        NativeVoiceRoutingBridge.TrySetLocalSending(false);
        toggled = false;
        hud?.SetSelectorOpen(false);
        BaseGameVoiceTakeover.Shutdown();
        NativeCaptureStateBridge.Reset();
        NativeVoiceRoutingBridge.Shutdown();
        Instance = null;
        ModLog.Info("R9 native routing runtime cleaned up.");
    }
}
