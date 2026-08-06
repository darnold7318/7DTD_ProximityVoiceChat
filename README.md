# ProximityPartyVoice 1.0.15 R18

7 Days to Die V3.1 b14 proximity/radio voice mod.

R18 uses the ILSpy-verified stock capture path:

```text
VoiceHelpers.PushToTalkPressed()
  -> PartyVoice.Update()
  -> Platform.EOS.Voice.MuteSelf = false
  -> RTCAudio.UpdateSending(AudioStatus.Enabled)
```

Open `ProximityPartyVoice.sln` in Visual Studio 2022 and build **Release | Any CPU**. The installable mod folder is generated at `Build/ProximityPartyVoice`.

See `DECOMPILED_EVIDENCE_R18.md` and `README_R18_VERIFIED_CAPTURE_PATH.md`.
