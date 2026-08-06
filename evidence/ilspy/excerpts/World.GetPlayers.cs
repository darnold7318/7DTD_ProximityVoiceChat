// ILSpy 11.0.0.9338 decompilation excerpt from V3.1 b14 Assembly-CSharp.dll

public DictionaryList<int, EntityPlayer> Players = new DictionaryList<int, EntityPlayer>();

public override List<EntityPlayer> GetPlayers()
{
	return Players.list;
}
