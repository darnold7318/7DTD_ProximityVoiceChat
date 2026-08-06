// ILSpy 11.0.0.9338 decompilation excerpt from V3.1 b14 Assembly-CSharp.dll

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
				audioInterface.UpdateSending(ref options, null, delegate(ref UpdateSendingCallbackInfo _data)
				{
					if (_data.ResultCode != Result.Success)
					{
						Log.Error("[EOS-Voice] Failed updating sending: " + _data.ResultCode);
					}
				});
			}
		}
	}
}
