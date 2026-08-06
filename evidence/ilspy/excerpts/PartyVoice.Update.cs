// ILSpy 11.0.0.9338 decompilation excerpt from V3.1 b14 Assembly-CSharp.dll

public void Update()
{
	if (platformPartyVoiceInitialized && !(localPlayer == null) && platformPartyVoice.InLobby)
	{
		platformPartyVoice.MuteSelf = !VoiceHelpers.PlatformVoiceEnabled || !VoiceHelpers.PushToTalkPressed();
		platformPartyVoice.MuteOthers = !VoiceHelpers.PlatformVoiceEnabled;
	}
}
