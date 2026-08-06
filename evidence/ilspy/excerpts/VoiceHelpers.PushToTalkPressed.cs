// ILSpy 11.0.0.9338 decompilation excerpt from V3.1 b14 Assembly-CSharp.dll

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
