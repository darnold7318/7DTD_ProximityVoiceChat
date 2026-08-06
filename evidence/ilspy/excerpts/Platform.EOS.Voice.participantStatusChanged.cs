// ILSpy 11.0.0.9338 decompilation excerpt from V3.1 b14 Assembly-CSharp.dll

[PublicizedFrom(EAccessModifier.Private)]
public void participantStatusChanged(ref ParticipantStatusChangedCallbackInfo _data)
{
	if (Status != EPartyVoiceStatus.Ok)
	{
		return;
	}
	if (Api.DebugLevel == Api.EDebugLevel.Verbose)
	{
		Log.Out($"[EOS-Voice] Participant state changed: {_data.ParticipantId}, {_data.ParticipantStatus}");
	}
	UserIdentifierEos userIdentifierEos = new UserIdentifierEos(_data.ParticipantId);
	if (userIdentifierEos.Equals(localUserIdentifier))
	{
		roomEntered = _data.ParticipantStatus == RTCParticipantStatus.Joined;
		if (roomEntered)
		{
			createInProgress = false;
			joinInProgress = false;
			muteSelf = true;
			muteOthers = false;
		}
		OnLocalPlayerStateChanged?.Invoke((_data.ParticipantStatus != RTCParticipantStatus.Joined) ? IPartyVoice.EVoiceChannelAction.Left : IPartyVoice.EVoiceChannelAction.Joined);
	}
	else
	{
		OnRemotePlayerStateChanged?.Invoke(userIdentifierEos, (_data.ParticipantStatus != RTCParticipantStatus.Joined) ? IPartyVoice.EVoiceChannelAction.Left : IPartyVoice.EVoiceChannelAction.Joined);
	}
}
