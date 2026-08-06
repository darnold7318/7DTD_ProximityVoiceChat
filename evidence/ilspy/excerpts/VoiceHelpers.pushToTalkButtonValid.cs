// ILSpy 11.0.0.9338 decompilation excerpt from V3.1 b14 Assembly-CSharp.dll

[PublicizedFrom(EAccessModifier.Private)]
public static bool pushToTalkButtonValid(LocalPlayerUI _playerUI)
{
	bool controlKeyPressed = InputUtils.ControlKeyPressed;
	bool flag = _playerUI.windowManager.IsInputActive();
	bool flag2 = PlatformManager.NativePlatform.Input.CurrentInputStyle != PlayerInputManager.InputStyle.Keyboard && GameManager.Instance.isAnyCursorWindowOpen();
	if (!(GameManager.Instance.IsEditMode() & controlKeyPressed) && !flag)
	{
		return !flag2;
	}
	return false;
}
