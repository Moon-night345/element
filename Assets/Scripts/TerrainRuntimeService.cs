using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ElementalStarSea.Data
{
    [Serializable] public sealed class TerrainBlockDef
    {
        public string id, item_id, region_id;
        public int material, placement_material, power, icon_x, icon_y;
        public float hardness;
    }
    [Serializable] public sealed class TerrainCatalog { public int schema_version; public TerrainBlockDef[] blocks; }
    [Serializable] public sealed class TerrainCellChange { public int x, y; public string block_id; }
    public sealed class TerrainOperation
    {
        public bool success;
        public string reason = "", item_id = "";
        public float duration;
    }
    public sealed class TerrainPatch
    {
        public string region;
        public int x, floor, width, depth;
    }

    // Coordinates: one Godot 32px cell = one Unity unit, Y points upward.
    // Rectangular slice generation is explicit; this is not Godot's full cave generator.
    public sealed class TerrainRuntimeService
    {
        public const int GeneratorVersion = 1;
        public const float BuildRange = 6f; // WorldSession.TERRAIN_BUILD_RANGE / 32
        public const float PlayerHalfWidth = 0.375f, PlayerFootOffset = 1.2f, PlayerHeight = 2f;
        readonly GameState state;
        readonly RegionEnvironmentService environment;
        readonly MechanicalToolService tools;
        readonly Dictionary<string, TerrainBlockDef> blocks;
        readonly Dictionary<Vector2Int, string> cells = new Dictionary<Vector2Int, string>();
        readonly Dictionary<Vector2Int, string> initial = new Dictionary<Vector2Int, string>();
        readonly Dictionary<Vector2Int, string> changes = new Dictionary<Vector2Int, string>();
        readonly List<Rect> protectedAreas = new List<Rect>();
        readonly List<TerrainPatch> patches = new List<TerrainPatch>();
        readonly HashSet<Vector2Int> foundation = new HashSet<Vector2Int>();
        public event Action<Vector2Int> CellChanged;
        public IEnumerable<KeyValuePair<Vector2Int, string>> Cells { get { return cells; } }
        public IEnumerable<TerrainPatch> Patches { get { return patches; } }
        public IEnumerable<TerrainBlockDef> PlaceableItems
        {
            get { return blocks.Values.Where(b => b.placement_material > 0 && state.CountItem(b.item_id) > 0)
                .GroupBy(b => b.item_id).Select(g => g.First()); }
        }

        public TerrainRuntimeService(GameState state, RegionsDatabase regions, TerrainCatalog catalog)
        {
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            if (catalog == null || catalog.schema_version != 1 || catalog.blocks == null) throw new ArgumentException("Invalid terrain catalog");
            blocks = catalog.blocks.ToDictionary(b => b.id);
            environment = new RegionEnvironmentService(state, regions);
            tools = new MechanicalToolService(state);
        }
        public TerrainBlockDef Definition(string id) { TerrainBlockDef b; return id != null && blocks.TryGetValue(id, out b) ? b : null; }
        public string BlockAt(Vector2Int cell) { string id; return cells.TryGetValue(cell, out id) ? id : ""; }
        public bool Solid(Vector2Int cell) { return cells.ContainsKey(cell); }
        public static Vector2 Center(Vector2Int cell) { return new Vector2(cell.x + 0.5f, cell.y + 0.5f); }
        public static Rect PlayerBounds(Vector2 center) { return new Rect(center.x - PlayerHalfWidth, center.y - PlayerFootOffset, PlayerHalfWidth * 2f, PlayerHeight); }
        public void Protect(Rect rect) { protectedAreas.Add(rect); }
        public bool IsProtected(Vector2Int cell)
        {
            var tile = new Rect(cell.x, cell.y, 1f, 1f);
            return foundation.Contains(cell) || protectedAreas.Any(r => r.Overlaps(tile));
        }

        public void AddPatch(string region, int x, int floor, int width, int depth)
        {
            var patch = new TerrainPatch { region = region, x = x, floor = floor, width = width, depth = depth };
            patches.Add(patch);
            var authored = blocks.Values.Where(b => b.region_id == region).OrderBy(b => b.id).ToArray();
            for (int row = 0; row < depth; row++)
                for (int col = 0; col < width; col++)
                {
                    var cell = new Vector2Int(x + col, floor - row - 1);
                    uint hash = Hash(state.terrainSeed, cell);
                    string id = row == 0 ? "soil" : row > 4 ? "dense_rock" : hash % 9 == 0 ? "ore" : "stone";
                    if (authored.Length > 0) id = authored[hash % (uint)authored.Length].id;
                    cells[cell] = initial[cell] = id;
                    // Keep current slice progression on authored elevators until connected caves migrate.
                    if (row == depth - 1) foundation.Add(cell);
                }
            if (region == "prologue_lab") Protect(new Rect(x, floor - depth, width, depth + 24));
        }

        public void RestoreChanges()
        {
            if (state.terrainGeneratorVersion != 0 && state.terrainGeneratorVersion != GeneratorVersion)
                throw new InvalidOperationException("Unsupported terrain generator version");
            var seen = new HashSet<Vector2Int>();
            var rows = state.terrainChanges ?? new TerrainCellChange[0];
            foreach (var row in rows)
            {
                if (row == null) throw new InvalidOperationException("Null terrain delta");
                var cell = new Vector2Int(row.x, row.y);
                if (!InBounds(cell) || IsProtected(cell) || !seen.Add(cell) ||
                    (!string.IsNullOrEmpty(row.block_id) && Definition(row.block_id) == null))
                    throw new InvalidOperationException("Invalid terrain delta at " + cell);
            }
            foreach (var row in rows) Apply(new Vector2Int(row.x, row.y), row.block_id ?? "", false);
            state.terrainGeneratorVersion = GeneratorVersion;
        }
        static uint Hash(int seed, Vector2Int p)
        {
            unchecked { uint h = (uint)seed ^ (uint)p.x * 374761393u ^ (uint)p.y * 668265263u; h = (h ^ h >> 13) * 1274126177u; return h ^ h >> 16; }
        }
        public string RegionAt(Vector2Int p)
        {
            var patch = patches.Where(t => p.x >= t.x && p.x < t.x + t.width && p.y < t.floor + 32 && p.y >= t.floor - t.depth)
                .OrderBy(t => Math.Abs(p.y - t.floor)).FirstOrDefault();
            return patch != null ? patch.region : "";
        }
        public bool InBounds(Vector2Int p) { return RegionAt(p) != ""; }
        public TerrainOperation MiningStatus(Vector2Int cell, Vector2 player)
        {
            var b = Definition(BlockAt(cell));
            if (b == null) return Fail("invalid_block");
            if (IsProtected(cell)) return Fail("protected_block");
            if (Vector2.Distance(player, Center(cell)) > BuildRange) return Fail("out_of_range");
            if (tools.EquippedStats.mining_power < b.power) return Fail("mining_power_too_low");
            var access = environment.MiningAccess(RegionAt(cell));
            if (!access.allowed) return Fail(access.reason);
            return new TerrainOperation { success = true, item_id = b.item_id,
                duration = Math.Max(0.01f, b.hardness) / Math.Max(0.01f, tools.EquippedStats.mining_speed) };
        }
        public TerrainOperation Mine(Vector2Int cell, Vector2 player)
        {
            var result = MiningStatus(cell, player);
            if (!result.success) return result;
            // Pickup is immediate in this slice; reserve no floating drop that could duplicate on reload.
            if (!state.TryApplyInventoryDelta(new[] { new InventoryDeltaEntry(result.item_id, 1) })) return Fail("inventory_rejected");
            Apply(cell, "", true);
            return result;
        }
        // Gameplay path: removing a block creates a persistent entity, not inventory.
        // Mine above remains the immediate-pickup compatibility API for U12 contracts.
        public TerrainOperation MineToDrop(Vector2Int cell, Vector2 player)
        {
            var result = MiningStatus(cell, player);
            if (!result.success) return result;
            var position = Center(cell);
            var drop = new WorldDropState { instance_id = Guid.NewGuid().ToString("N"), item_id = result.item_id,
                amount = 1, source_block_id = BlockAt(cell), region_id = RegionAt(cell), x = position.x, y = position.y };
            state.worldDrops = (state.worldDrops ?? new WorldDropState[0]).Concat(new[] { drop }).ToArray();
            Apply(cell, "", true);
            return result;
        }
        public TerrainOperation PlacementStatus(Vector2Int cell, string item, Vector2 player)
        {
            if (!InBounds(cell)) return Fail("out_of_bounds");
            var block = blocks.Values.FirstOrDefault(b => b.item_id == item && b.placement_material > 0);
            if (block == null) return Fail("item_not_placeable");
            if (state.CountItem(item) < 1) return Fail("missing_item");
            if (Solid(cell)) return Fail("occupied");
            if (!(Solid(cell + Vector2Int.left) || Solid(cell + Vector2Int.right) || Solid(cell + Vector2Int.up) || Solid(cell + Vector2Int.down)))
                return Fail("no_adjacent_support");
            if (Vector2.Distance(player, Center(cell)) > BuildRange) return Fail("out_of_range");
            if (PlayerBounds(player).Overlaps(new Rect(cell.x, cell.y, 1, 1))) return Fail("player_overlap");
            if (IsProtected(cell)) return Fail("protected_anchor");
            foreach (var device in state.deviceInstances ?? new DeviceRuntimeContract[0])
                if (device != null && device.is_placed && Vector2.Distance(Center(cell), new Vector2(device.world_x, device.world_y)) < 1f)
                    return Fail("device_overlap");
            return new TerrainOperation { success = true, item_id = item };
        }
        public TerrainOperation Place(Vector2Int cell, string item, Vector2 player)
        {
            var result = PlacementStatus(cell, item, player);
            if (!result.success) return result;
            var b = blocks.Values.First(d => d.item_id == item && d.placement_material > 0);
            if (!state.TryApplyInventoryDelta(new[] { new InventoryDeltaEntry(item, -1) })) return Fail("inventory_rejected");
            Apply(cell, b.id, true);
            return result;
        }
        void Apply(Vector2Int cell, string id, bool notify)
        {
            if (id == "") cells.Remove(cell); else cells[cell] = id;
            string original; initial.TryGetValue(cell, out original);
            if ((original ?? "") == id) changes.Remove(cell); else changes[cell] = id;
            state.terrainChanges = changes.OrderBy(p => p.Key.x).ThenBy(p => p.Key.y).Select(p =>
                new TerrainCellChange { x = p.Key.x, y = p.Key.y, block_id = p.Value }).ToArray();
            state.terrainGeneratorVersion = GeneratorVersion;
            if (notify && CellChanged != null) CellChanged(cell);
        }
        public bool Collides(Rect bounds)
        {
            if (patches.Count > 0 && (bounds.xMin < patches.Min(t => t.x) || bounds.xMax > patches.Max(t => t.x + t.width) ||
                bounds.yMin < patches.Min(t => t.floor - t.depth))) return true;
            for (int y = Mathf.FloorToInt(bounds.yMin + 0.0001f); y <= Mathf.FloorToInt(bounds.yMax - 0.0001f); y++)
                for (int x = Mathf.FloorToInt(bounds.xMin + 0.0001f); x <= Mathf.FloorToInt(bounds.xMax - 0.0001f); x++)
                    if (Solid(new Vector2Int(x, y))) return true;
            return false;
        }
        public Vector2 Move(Vector2 center, Vector2 displacement, out bool horizontalBlocked, out bool verticalBlocked)
        {
            horizontalBlocked = verticalBlocked = false;
            int steps = Math.Max(1, Mathf.CeilToInt(Mathf.Max(Mathf.Abs(displacement.x), Mathf.Abs(displacement.y)) / 0.1f));
            Vector2 delta = displacement / steps;
            for (int i = 0; i < steps; i++)
            {
                var next = center + new Vector2(horizontalBlocked ? 0 : delta.x, 0);
                if (Collides(PlayerBounds(next))) horizontalBlocked = true; else center = next;
                next = center + new Vector2(0, verticalBlocked ? 0 : delta.y);
                if (Collides(PlayerBounds(next))) verticalBlocked = true; else center = next;
            }
            return center;
        }
        static TerrainOperation Fail(string reason) { return new TerrainOperation { reason = reason }; }
    }

    public sealed class TerrainMiningProgress
    {
        public Vector2Int target;
        public bool active;
        public float elapsed, duration;
        public void Begin(Vector2Int cell, float time)
        {
            if (time <= 0f || float.IsNaN(time) || float.IsInfinity(time)) { Cancel(); return; }
            if (active && target == cell) return;
            target = cell; elapsed = 0; duration = time; active = true;
        }
        public bool Advance(float delta)
        {
            if (!active || delta <= 0 || float.IsNaN(delta) || float.IsInfinity(delta)) return false;
            elapsed = Math.Min(duration, elapsed + delta); return elapsed >= duration;
        }
        public float Ratio { get { return active && duration > 0 ? Mathf.Clamp01(elapsed / duration) : 0; } }
        public void Cancel() { active = false; elapsed = duration = 0; }
    }
}
