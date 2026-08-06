# ProximityPartyVoice 1.0.15 R18

R18 replaces the speculative capture bridges with the exact managed path proven by ILSpy from the supplied V3.1 b14 `Assembly-CSharp.dll`.

## Main change

The mod now patches `PartyVoice.Update()` and applies automatic proximity/radio transmission through:

```text
PartyVoice.platformPartyVoice.MuteSelf = false
```

The stock `Platform.EOS.Voice.MuteSelf` setter performs `RTCAudio.UpdateSending(AudioStatus.Enabled)`.

R18 does not synthesize `VoiceHelpers.PushToTalkPressed()`, search for a nonexistent `SetPushToTalkActive`, or issue a second direct EOS sending call.

## Build

Open `ProximityPartyVoice.sln` in Visual Studio 2022 and build `Release | Any CPU`.

Install the generated folder:

```text
Build/ProximityPartyVoice
```

## Expected log entries

```text
Loading ProximityPartyVoice 1.0.15 R18
R18 patched PartyVoice.Update prefix/postfix at the verified V3.1 b14 MuteSelf capture path.
R18 verified MuteSelf state: SENDING (source=proximity).
```

See `DECOMPILED_EVIDENCE_R18.md` and `evidence/ilspy/csharp/` for the decompiled evidence.
