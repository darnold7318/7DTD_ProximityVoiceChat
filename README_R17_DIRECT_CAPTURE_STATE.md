# R17 — Direct Managed Capture State

Version: **1.0.14**
Target: **7 Days to Die V3.1 B14 Mono**

## Assembly-CSharp findings

The B14 managed assembly contains the native voice controls used by the stock pipeline:

- `PartyVoice`
- `Platform.EOS.Voice`
- `SetPushToTalkActive`
- `MuteSelf` / `set_MuteSelf`
- `handlePushToTalkButton`
- `get_SendingVoice`
- `UpdateSending`

R15 proved that `RTCAudio.UpdateSending` can return `Success` without opening the higher-level managed capture gate. R16's synthetic `VoiceHelpers` state also did not create automatic voice.

R17 therefore drives the stock managed capture state directly:

1. Resolve the live `PartyVoice` singleton.
2. Resolve `platformPartyVoice` (`Platform.EOS.Voice`).
3. Call any available `SetPushToTalkActive(bool)` implementation.
4. Set `MuteSelf = !active` even when EOS `UpdateSending` succeeds.
5. Retain the typed EOS routing and participant-volume logic.

## Expected log lines

When the world and voice lobby are ready:

```text
R17 capture controls resolved: partySetPtt=..., platformType=Platform.EOS.Voice, platformSetPtt=..., MuteSelfProperty=True, ...
R17 native capture state ACTIVE: SetPushToTalkActive + MuteSelf applied.
```

When no player is within proximity range and V is not held:

```text
R17 native capture state INACTIVE: SetPushToTalkActive + MuteSelf applied.
```

## Build

Open `ProximityPartyVoice.sln` in Visual Studio 2022 and rebuild **Release | Any CPU**.
The installable folder is produced at:

```text
Build\ProximityPartyVoice
```
