using System;
using System.Collections;
using System.Reflection;
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
            var player = GameManager.Instance?.World?.GetPrimaryPlayer();
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
            object? world = GameManager.Instance?.World;
            object? local = world == null ? null : Invoke(world, "GetPrimaryPlayer");
            if (world == null || local == null) return false;

            int localId = ReadInt(local, "entityId", "EntityId", "entityID");
            Vector3 localPos = ReadPosition(local);
            float maxSqr = maxDistance * maxDistance;

            object? players = Get(world, "Players") ?? Get(world, "players")
                           ?? Get(world, "PlayerEntities") ?? Get(world, "playerEntities");
            if (players is IEnumerable enumerable)
            {
                foreach (object? candidate in enumerable)
                {
                    object? player = UnwrapDictionaryEntry(candidate);
                    if (player == null || ReferenceEquals(player, local)) continue;
                    if (!LooksLikePlayer(player)) continue;
                    int id = ReadInt(player, "entityId", "EntityId", "entityID");
                    if (id >= 0 && id == localId) continue;
                    if ((ReadPosition(player) - localPos).sqrMagnitude <= maxSqr) return true;
                }
            }

            // Fallback for builds exposing all entities instead of a player list.
            object? entities = Get(world, "Entities") ?? Get(world, "entities")
                            ?? Get(world, "EntitiesList") ?? Get(world, "entitiesList");
            if (entities is IEnumerable all)
            {
                foreach (object? candidate in all)
                {
                    object? player = UnwrapDictionaryEntry(candidate);
                    if (player == null || ReferenceEquals(player, local) || !LooksLikePlayer(player)) continue;
                    int id = ReadInt(player, "entityId", "EntityId", "entityID");
                    if (id >= 0 && id == localId) continue;
                    if ((ReadPosition(player) - localPos).sqrMagnitude <= maxSqr) return true;
                }
            }
        }
        catch (Exception ex)
        {
            ModLog.Warning("Proximity player scan failed: " + ex.GetBaseException().Message);
        }
        return false;
    }

    private static object? UnwrapDictionaryEntry(object? value)
    {
        if (value is DictionaryEntry de) return de.Value;
        if (value == null) return null;
        object? unwrapped = Get(value, "Value");
        return unwrapped ?? value;
    }

    private static bool LooksLikePlayer(object value)
    {
        string name = value.GetType().Name;
        return name.IndexOf("EntityPlayer", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static Vector3 ReadPosition(object value)
    {
        object? result = Get(value, "position") ?? Get(value, "Position");
        if (result is Vector3 v) return v;
        object? transform = Get(value, "transform") ?? Get(value, "Transform");
        result = transform == null ? null : Get(transform, "position") ?? Get(transform, "Position");
        return result is Vector3 tv ? tv : Vector3.zero;
    }

    private static int ReadInt(object value, params string[] names)
    {
        foreach (string name in names)
        {
            object? result = Get(value, name);
            if (result is int i) return i;
        }
        return -1;
    }

    private static int FindPartyId(object player)
    {
        foreach (string name in new[] { "PartyId", "partyID", "partyId" })
        {
            object? value = Get(player, name);
            if (value is int id)
                return id;
        }

        object? party = Get(player, "Party") ?? Get(player, "party");
        if (party != null)
        {
            foreach (string name in new[] { "PartyId", "partyID", "partyId", "ID", "Id" })
            {
                if (Get(party, name) is int id)
                    return id;
            }
        }

        return 0;
    }

    public static bool IsServer()
    {
        try
        {
            Type? type = Type.GetType("ConnectionManager, Assembly-CSharp");
            object? instance = type?
                .GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?
                .GetValue(null);

            foreach (string name in new[] { "IsServer", "IsDedicatedServer", "IsServerRunning" })
            {
                if (Get(instance, name) is bool value && value)
                    return true;
            }
        }
        catch { }
        return false;
    }

    private static object? Invoke(object target, string name)
    {
        return target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, Type.EmptyTypes, null)?.Invoke(target, null);
    }

    private static object? Get(object? instance, string name)
    {
        if (instance == null) return null;
        Type type = instance.GetType();
        return type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(instance)
            ?? type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(instance);
    }
}
