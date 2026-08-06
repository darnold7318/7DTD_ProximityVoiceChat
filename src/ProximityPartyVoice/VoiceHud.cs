using UnityEngine;

namespace ProximityPartyVoice;

public sealed class VoiceHud
{
    readonly VoiceSettings settings;
    GUIStyle? label, tx, panel, button;
    public bool SelectorOpen { get; private set; }

    public VoiceHud(VoiceSettings s) { settings = s; }

    void Styles()
    {
        label ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = settings.HudFontSize,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };
        tx ??= new GUIStyle(label) { fontSize = settings.TxFontSize };
        panel ??= new GUIStyle(GUI.skin.box) { padding = new RectOffset(12, 12, 10, 10) };
        button ??= new GUIStyle(GUI.skin.button) { fontSize = 18, fixedHeight = settings.SelectorButtonHeight };
    }

    public void SetSelectorOpen(bool open)
    {
        SelectorOpen = open;
        GameUiInputBridge.SetModalCursor(open);
    }

    public void ToggleSelector() => SetSelectorOpen(!SelectorOpen);

    public void Draw(int channel, bool transmitting)
    {
        Styles();

        // hudY is measured upward from the bottom edge so the indicator stays
        // aligned with the health/stamina area at different resolutions.
        float y = Screen.height - settings.HudY;
        GUI.Box(new Rect(settings.HudX, y, settings.HudWidth, settings.HudHeight), GUIContent.none, panel);
        GUI.Label(new Rect(settings.HudX + 8, y + 4, 58, settings.HudHeight - 8), $"CH {channel:00}", label);
        if (transmitting)
            GUI.Label(new Rect(settings.HudX + 66, y + 5, 40, settings.HudHeight - 10), "TX", tx);

        if (!SelectorOpen) return;

        // IMGUI receives keyboard events independently of the game's InControl
        // action map. This is the fallback when gameplay consumes Escape first.
        if (Event.current != null && Event.current.type == EventType.KeyDown &&
            Event.current.keyCode == KeyCode.Escape)
        {
            Event.current.Use();
            VoiceRuntime.Instance?.CloseSelector();
            return;
        }

        float minimumHeight = 82f + settings.ChannelCount * (settings.SelectorButtonHeight + 4f);
        float height = Mathf.Max(settings.SelectorHeight, minimumHeight);
        height = Mathf.Min(height, Screen.height - 40f);
        float width = Mathf.Min(settings.SelectorWidth, Screen.width - 40f);
        var r = new Rect((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);

        GUI.Box(r, "SELECT CHANNEL", panel);
        GUILayout.BeginArea(new Rect(r.x + 18, r.y + 42, r.width - 36, r.height - 58));
        for (int i = 1; i <= settings.ChannelCount; i++)
        {
            if (GUILayout.Button($"CHANNEL {i:00}", button))
                VoiceRuntime.Instance?.SelectChannel(i);
        }
        GUILayout.EndArea();

        // Consume only after controls have processed the event. Consuming before
        // GUILayout.Button prevented every channel button from receiving clicks.
        GameUiInputBridge.ConsumeGuiMouseEvent();
    }
}
