# R18 decompiled evidence — 7 Days to Die V3.1 b14

## Provenance

- Decompiler: ILSpy / `ilspycmd` 11.0.0.9338
- Input: the `Assembly-CSharp.dll` extracted from the supplied `Managed(4).zip`
- SHA-256: `b13862e30d8b28f42b83fe6a36bf074d155a6c43164e7b0797a6e4f77bd7dea3`
- Full decompiled types are under `evidence/ilspy/csharp/`.

No method behavior in this report is inferred from a method name or from runtime logs. The conclusions below come from the decompiled method bodies.

## Exact physical push-to-talk call path

### 1. Physical input is read by `VoiceHelpers.PushToTalkPressed()`

From `evidence/ilspy/csharp/VoiceHelpers.cs`:

```csharp
public static bool PushToTalkPressed()
{
    LocalPlayerUI uIForPrimaryPlayer = LocalPlayerUI.GetUIForPrimaryPlayer();
    if (uIForPrimaryPlayer == null || uIForPrimaryPlayer.playerInput == null)
    {
        return false;
    }
    if (uIForPrimaryPlayer.playerInput.PermanentActions.PushToTalk.IsPressed)
    {
        return pushToTalkButtonValid(uIForPrimaryPlayer);
    }
    return false;
}
```

The physical game binding is therefore read from:

```text
LocalPlayerUI.GetUIForPrimaryPlayer()
  .playerInput.PermanentActions.PushToTalk.IsPressed
```

It is then filtered by `pushToTalkButtonValid(...)`.

### 2. `PartyVoice.Update()` converts that input into `MuteSelf`

From `evidence/ilspy/csharp/PartyVoice.cs`:

```csharp
public void Update()
{
    if (platformPartyVoiceInitialized && !(localPlayer == null) && platformPartyVoice.InLobby)
    {
        platformPartyVoice.MuteSelf = !VoiceHelpers.PlatformVoiceEnabled || !VoiceHelpers.PushToTalkPressed();
        platformPartyVoice.MuteOthers = !VoiceHelpers.PlatformVoiceEnabled;
    }
}
```

With voice enabled and PTT held, this writes:

```text
platformPartyVoice.MuteSelf = false
```

### 3. `Platform.EOS.Voice.MuteSelf` performs the EOS sending transition

From `evidence/ilspy/csharp/Platform.EOS.Voice.cs`:

```csharp
public bool MuteSelf
{
    get
    {
        return muteSelf;
    }
    set
    {
        if (Status != EPartyVoiceStatus.Ok)
        {
            Log.Error("[EOS-Voice] Can not mute self because voice is currently not ready.");
        }
        else
        {
            if (value == muteSelf)
            {
                return;
            }
            muteSelf = value;
            if (roomName == null)
            {
                return;
            }
            EosHelpers.AssertMainThread("Voice.Mute");
            UpdateSendingOptions options = new UpdateSendingOptions
            {
                LocalUserId = localProductUserId,
                RoomName = roomName,
                AudioStatus = ((!value) ? RTCAudioStatus.Enabled : RTCAudioStatus.Disabled)
            };
            lock (AntiCheatCommon.LockObject)
            {
                audioInterface.UpdateSending(ref options, null, ...);
            }
        }
    }
}
```

## Final required state and method

The final managed state required to start microphone transmission is:

```text
Platform.EOS.Voice.MuteSelf = false
```

The setter then performs the final API call:

```text
RTCAudioInterface.UpdateSending(
    AudioStatus = RTCAudioStatus.Enabled)
```

There is no `SetPushToTalkActive(bool)` method in the decompiled `PartyVoice` or `Platform.EOS.Voice` types. R17's search for such a method was not supported by the actual assembly.

## Lobby initialization state

Both lobby creation and lobby joining set:

```csharp
LocalAudioDeviceInputStartsMuted = true
```

When the local participant joins the RTC room, `participantStatusChanged(...)` sets:

```csharp
roomEntered = true;
muteSelf = true;
muteOthers = false;
```

Therefore, the first transition to `MuteSelf = false` after room entry is required to invoke `UpdateSending(Enabled)`.

## Exact call graph

```text
Physical PTT binding
  PermanentActions.PushToTalk.IsPressed
    -> VoiceHelpers.PushToTalkPressed()
       -> PartyVoice.Update()
          -> IPartyVoice.MuteSelf = false
             -> Platform.EOS.Voice.MuteSelf setter
                -> muteSelf = false
                -> RTCAudioInterface.UpdateSending(AudioStatus.Enabled)
```

## R18 implementation

R18 patches `PartyVoice.Update()` with a prefix/postfix:

- The prefix makes the current proximity/radio decision available for the same frame.
- Stock `PartyVoice.Update()` remains intact and processes physical PTT normally.
- The postfix writes the verified final state, `platformPartyVoice.MuteSelf = false`, when proximity or radio transmission is requested.
- The postfix does not call EOS directly. The stock `Platform.EOS.Voice.MuteSelf` setter owns `UpdateSending` exactly as the game does.
- When no mod transmission is requested, the postfix preserves the stock physical-PTT result.
- The channel selector or disabled game voice forces `MuteSelf = true`.

R18 also replaces the reflection-based proximity enumeration with the concrete decompiled B14 API:

```csharp
List<EntityPlayer> players = world.GetPlayers();
```

The decompiled `World` type proves `GetPlayers()` returns `Players.list`.

## Input starts muted and local join state

ILSpy also shows both `CreateLobby` and `JoinLobby` setting:

```csharp
LocalAudioDeviceInputStartsMuted = true
```

When the local participant reports `RTCParticipantStatus.Joined`, `participantStatusChanged(...)` writes:

```csharp
roomEntered = true;
muteSelf = true;
muteOthers = false;
```

This proves that the local RTC input begins muted and that changing the `MuteSelf` property to `false` is the managed transition that causes the stock setter to request `RTCAudioStatus.Enabled`.
