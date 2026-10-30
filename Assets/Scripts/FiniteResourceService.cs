using System;
using System.Linq;

namespace ElementalStarSea.Data
{
    [Serializable] public sealed class ResourceSourceState
    {
        public string id, item_id;
        public int capacity, remaining;
    }

    // Capacities: WorldSession.ENVIRONMENT_RESOURCE_CAPACITY, SourceDefinition and
    // ChemicalResourceCatalog.SOURCE_TYPES. Scene ore samplers remain slice adapters.
    public sealed class FiniteResourceService
    {
        readonly GameState state;
        readonly RegionsDatabase regions;
        public FiniteResourceService(GameState state, RegionsDatabase regions)
        { this.state = state; this.regions = regions; }

        public int RegionRemaining(string region, string item)
        {
            if (!IsRegional(region, item)) return 0;
            return Remaining("region:" + region + ":" + item, item, 6);
        }
        bool IsRegional(string region, string item)
        {
            return regions.regions.Any(r => r.id == region && (r.resources ?? new string[0]).Contains(item));
        }
        public bool GatherRegion(string region, string item, out string reason)
        {
            if (state.currentRegion != region || !IsRegional(region, item))
            { reason = "invalid_region_resource"; return false; }
            return Capture("region:" + region + ":" + item, item, 6, out reason);
        }
        public int Remaining(string source, string item, int capacity)
        {
            var row = (state.resourceSources ?? new ResourceSourceState[0]).FirstOrDefault(s => s.id == source);
            return row == null ? capacity : row.item_id == item && row.capacity == capacity ? row.remaining : 0;
        }
        public bool Capture(string source, string item, int capacity, out string reason)
        {
            reason = "";
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(item) || capacity <= 0)
            { reason = "invalid_source"; return false; }
            var rows = state.resourceSources ?? new ResourceSourceState[0];
            var row = rows.FirstOrDefault(s => s.id == source);
            if (row != null && (row.item_id != item || row.capacity != capacity))
            { reason = "source_contract_mismatch"; return false; }
            int remaining = row == null ? capacity : row.remaining;
            if (remaining <= 0) { reason = "source_depleted"; return false; }
            if (!state.TryApplyInventoryDelta(new[] { new InventoryDeltaEntry(item, 1) }))
            { reason = "inventory_rejected"; return false; }
            if (row == null)
            {
                row = new ResourceSourceState { id = source, item_id = item, capacity = capacity, remaining = remaining };
                state.resourceSources = rows.Concat(new[] { row }).ToArray();
            }
            row.remaining--;
            return true;
        }
    }
}
