using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProximityPartyVoice;

internal readonly struct LocalVoiceState
{
    public readonly int EntityId;
    public readonly int PartyId;
    public readonly Vector3 Position;

    public LocalVoiceState(int entityId, int partyId, Vector3 position)
    {
        EntityId = entityId;
        PartyId = partyId;
        Position = position;
    }
}

internal static class GameAdapter
{
    public static LocalVoiceState Local()
    {
        try
        {
            EntityPlayerLocal? player = GameManager.Instance?.World?.GetPrimaryPlayer();
            if (player == null)
                return new LocalVoiceState(-1, 0, Vector3.zero);

            return new LocalVoiceState(player.entityId, FindPartyId(player), player.position);
        }
        catch
        {
            return new LocalVoiceState(-1, 0, Vector3.zero);
        }
    }

    public static bool AnyOtherPlayerWithin(float maxDistance)
    {
        try
        {
            World? world = GameManager.Instance?.World;
            EntityPlayerLocal? local = world?.GetPrimaryPlayer();
            if (world == null || local == null) return false;

            List<EntityPlayer> players = world.GetPlayers();
            float maxSqr = maxDistance * maxDistance;
            Vector3 localPosition = local.position;

            for (int i = 0; i < players.Count; i++)
            {
                EntityPlayer? candidate = players[i];
                if (candidate == null || candidate.entityId == local.entityId) continue;

                if ((candidate.position - localPosition).sqrMagnitude <= maxSqr)
                    return true;
            }
        }
        catch (Exception ex)
        {
            ModLog.Warning("Proximity player scan failed: " + ex.GetBaseException().Message);
        }

        return false;
    }

    static int FindPartyId(EntityPlayer player)
    {
        try
        {
            Party? party = player.Party;
            return party?.PartyID ?? 0;
        }
        catch
        {
            return 0;
        }
    }
}
