using System;
using System.Globalization;
using System.IO;
using System.Xml.Linq;
using UnityEngine;

namespace ProximityPartyVoice;

public enum TransmitMode { PushToTalk, Toggle }

public sealed class VoiceSettings
{
    public KeyCode InputKey = KeyCode.V;
    public TransmitMode InputMode = TransmitMode.PushToTalk;
    public int DoubleTapWindowMs = 350;
    public bool SuppressTransmitOnDoubleTap = true;
    public bool ProximityEnabled = true;
    public float FadeStartMeters = 15f;
    public float FadeEndMeters = 50f;
    public float LogStrength = 9f;
    public bool RadioEnabled = true;
    public int DefaultChannel = 1;
    public int ChannelCount = 10;
    public float HudX = 182, HudY = 35, HudWidth = 112, HudHeight = 30;
    public int HudFontSize = 16, TxFontSize = 14;
    public float SelectorWidth = 360, SelectorHeight = 560, SelectorButtonHeight = 38;

    public static VoiceSettings Load(string modPath)
    {
        var s = new VoiceSettings();
        string path = Path.Combine(modPath, "Config", "ProximityVoice.xml");
        if (!File.Exists(path)) return s;
        XElement root = XDocument.Load(path).Root ?? throw new InvalidDataException("Missing proximityVoice root.");
        XElement? e = root.Element("input");
        if (e != null) { ParseEnum(e, "key", ref s.InputKey); ParseEnum(e, "mode", ref s.InputMode); s.DoubleTapWindowMs = Int(e,"doubleTapWindowMs",s.DoubleTapWindowMs); s.SuppressTransmitOnDoubleTap=Bool(e,"suppressTransmitOnDoubleTap",s.SuppressTransmitOnDoubleTap); }
        e = root.Element("proximity");
        if (e != null) { s.ProximityEnabled=Bool(e,"enabled",s.ProximityEnabled); s.FadeStartMeters=Float(e,"startMeters",s.FadeStartMeters); s.FadeEndMeters=Float(e,"endMeters",s.FadeEndMeters); s.LogStrength=Math.Max(.001f,Float(e,"logarithmicStrength",s.LogStrength)); }
        e = root.Element("radio");
        if (e != null) { s.RadioEnabled=Bool(e,"enabled",s.RadioEnabled); s.DefaultChannel=Int(e,"defaultChannel",s.DefaultChannel); s.ChannelCount=Mathf.Clamp(Int(e,"channelCount",s.ChannelCount),1,10); }
        e = root.Element("ui");
        if (e != null) { s.HudX=Float(e,"hudX",s.HudX); s.HudY=Float(e,"hudY",s.HudY); s.HudWidth=Float(e,"hudWidth",s.HudWidth); s.HudHeight=Float(e,"hudHeight",s.HudHeight); s.HudFontSize=Int(e,"hudFontSize",s.HudFontSize); s.TxFontSize=Int(e,"txFontSize",s.TxFontSize); s.SelectorWidth=Float(e,"selectorWidth",s.SelectorWidth); s.SelectorHeight=Float(e,"selectorHeight",s.SelectorHeight); s.SelectorButtonHeight=Float(e,"selectorButtonHeight",s.SelectorButtonHeight); }
        if (s.FadeEndMeters <= s.FadeStartMeters) s.FadeEndMeters = s.FadeStartMeters + 1f;
        s.DefaultChannel = Mathf.Clamp(s.DefaultChannel,1,s.ChannelCount);
        return s;
    }

    public float ProximityGain(float distance)
    {
        if (distance <= FadeStartMeters) return 1f;
        if (distance >= FadeEndMeters) return 0f;
        float remaining = 1f - ((distance-FadeStartMeters)/(FadeEndMeters-FadeStartMeters));
        return Mathf.Log(1f + LogStrength*remaining) / Mathf.Log(1f+LogStrength);
    }
    static void ParseEnum<T>(XElement e,string n,ref T value) where T:struct { if(Enum.TryParse((string?)e.Attribute(n),true,out T v)) value=v; }
    static int Int(XElement e,string n,int d)=>int.TryParse((string?)e.Attribute(n),NumberStyles.Integer,CultureInfo.InvariantCulture,out int v)?v:d;
    static float Float(XElement e,string n,float d)=>float.TryParse((string?)e.Attribute(n),NumberStyles.Float,CultureInfo.InvariantCulture,out float v)?v:d;
    static bool Bool(XElement e,string n,bool d)=>bool.TryParse((string?)e.Attribute(n),out bool v)?v:d;
}
