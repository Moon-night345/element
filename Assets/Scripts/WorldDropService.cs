using System;
using System.Linq;
using UnityEngine;

namespace ElementalStarSea.Data
{
    [Serializable] public sealed class WorldDropState
    {
        public string instance_id, item_id, source_block_id, region_id;
        public int amount;
        public float x, y;
    }
    public sealed class WorldDropService
    {
        readonly GameState state;
        public WorldDropService(GameState state) { this.state = state; }
        // Godot WorldDrop uses feet-relative 44x76 px rectangle, offset (0,-32).
        // Convert Y down -> Y up, 32 pixels per unit, preserving half-open edges.
        public static bool WithinPickupArea(Vector2 center, Vector2 drop)
        {
            Vector2 relative = drop - (center - Vector2.up * TerrainRuntimeService.PlayerFootOffset);
            return relative.x >= -22f / 32f && relative.x < 22f / 32f &&
                relative.y > -6f / 32f && relative.y <= 70f / 32f;
        }
        public bool Pickup(string instanceId, Vector2 player, out string reason)
        {
            reason = "";
            var drops = state.worldDrops ?? new WorldDropState[0];
            var drop = drops.FirstOrDefault(d => d.instance_id == instanceId);
            if (drop == null) { reason = "missing_drop"; return false; }
            if (!WithinPickupArea(player, new Vector2(drop.x, drop.y))) { reason = "out_of_range"; return false; }
            if (!state.TryApplyInventoryDelta(new[] { new InventoryDeltaEntry(drop.item_id, drop.amount) }))
            { reason = "inventory_rejected"; return false; }
            state.worldDrops = drops.Where(d => d.instance_id != instanceId).ToArray();
            return true;
        }
    }
}
