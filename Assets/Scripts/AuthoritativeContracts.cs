// Godot 权威目录的 Unity 数据契约。
// 本文件只定义迁移边界和校验规则，不改变现有玩法执行路径。
using System;
using System.Collections.Generic;
using System.Linq;

namespace ElementalStarSea.Data
{
    [Serializable] public sealed class OreProfilesDatabase { public OreProfile[] ores; }
    [Serializable] public sealed class OreProfile { public string id, metal, oxide, gangue; public float grade; }
    [Serializable]
    public class ConditionThreshold
    {
        public string id;
        public float minimum;
    }

    [Serializable]
    public class MigrationManifest
    {
        public int schema_version;
        public string source_commit;
        public string source_commit_time;
        public string[] source_files;
        public MigrationCounts counts;
    }

    [Serializable]
    public class MigrationCounts
    {
        public int regions;
        public int reaction_specs;
        public int reaction_content_units;
        public int formal_devices;
        public int processing_recipes;
        public int materials;
        public int localization_entries;
    }

    [Serializable]
    public class ReactionContentDatabase
    {
        public ReactionContentDef[] reactions;
    }

    [Serializable]
    public class ReactionContentDef
    {
        public string id;
        public string[] source_environments;
        public string[] source_nodes;
        public string[] required_tags;
        public string obstacle_id;
        public string shard_id;
        public string memory_id;
        public string[] route_nodes;
        public string[] alternate_reaction_ids;
    }

    [Serializable]
    public class DevicesDatabase
    {
        public FormalDeviceDef[] devices;
    }

    [Serializable]
    public class FormalDeviceDef
    {
        public int slot;
        public string id;
        public string display_name;
        public string purpose;
        public int stage;
        public string processing_type;
        public string processing_scale;
        public string resource_status;
        public string world_sprite;
        public string[] recipe_ids;
        public int input_slots;
        public int output_slots;
        public int storage_capacity;
        public int footprint_width;
        public int footprint_height;
        public DeviceCraftingContract crafting;
    }

    [Serializable]
    public class DeviceCraftingContract
    {
        public string crafting_station;
        public string unlock_region;
        public string unlock_condition_id;
        public int unlock_lab_stage;
        public RecipeQuantity[] inputs;
    }

    [Serializable]
    public class ProcessingRecipesDatabase
    {
        public ProcessingRecipeDef[] recipes;
    }

    [Serializable]
    public class ProcessingRecipeDef
    {
        public string id;
        public string device_id;
        public string device_type;
        public RecipeQuantity[] input_items;
        public RecipeQuantity[] process_inputs;
        public RecipeQuantitySet[] alternate_input_sets;
        public RecipeQuantity[] output_items;
        public float processing_time;
        public int required_stage;
        public ConditionValue[] required_conditions;
        public string[] progression_tags;
    }

    [Serializable]
    public class RecipeQuantitySet
    {
        public RecipeQuantity[] items;
    }

    [Serializable]
    public class ConditionValue
    {
        public string id;
        public string value;
    }

    // 后续 runtime 迁移共用的显式状态词；当前阶段不接管现有玩法。
    public enum DeviceProcessingState
    {
        Idle,
        Ready,
        Processing,
        OutputReady,
        InvalidInput,
        ConditionMissing,
        Completed,
        Error,
        Cancelled
    }

    [Serializable]
    public class DeviceRuntimeContract
    {
        public string instance_id;
        public string device_id;
        public bool is_unlocked;
        public bool is_placed;
        public DeviceProcessingState state;
        public string active_recipe_id;
        public float progress;
        public float elapsed;
        public float duration;
        public RecipeQuantity[] reserved_inputs;
        public RecipeQuantity[] buffered_outputs;
        public string error_reason;
        public string region_id;
        public float world_x;
        public float world_y;
        public string stored_item_id;
        public int stored_amount;
        public int storage_capacity;
    }

    [Serializable]
    public class DeviceGameplayEvent
    {
        public string analyzed_sample_id = "";
        public string event_type;
        public string device_id;
        public string instance_id;
        public string recipe_id;
        public string reason;
        public RecipeQuantity[] items;
    }

    [Serializable]
    public class DeviceOperationResult
    {
        public bool success;
        public string reason;
        public string device_id;
        public string instance_id;
        public string recipe_id;
        public DeviceRuntimeContract state;
    }

    public enum ReactionResultKind
    {
        Success,
        PreconditionFailed,
        CommitFailed
    }

    // 数值顺序与 Godot ReactionPreview.Failure 保持一致。
    public enum ReactionFailure
    {
        None = 0,
        MissingReactant = 1,
        MissingEnvironment = 2,
        InsufficientEnergy = 3,
        InvalidDefinition = 4
    }

    [Serializable]
    public class InventoryDeltaEntry
    {
        public string id;
        public int change;

        public InventoryDeltaEntry() { }
        public InventoryDeltaEntry(string contentId, int amount)
        {
            id = contentId;
            change = amount;
        }
    }

    [Serializable]
    public class ReactionGameplayEvent
    {
        public string event_type;
        public string reaction_id;
        public string reason;
        public float energy_spent;
        public InventoryDeltaEntry[] inventory_delta;
        public string language_id;
    }

    [Serializable]
    public class ReactionExecutionResult
    {
        public bool success;
        public ReactionResultKind kind;
        public ReactionFailure failure;
        public string reaction_id;
        public float energy_cost;
        public string[] missing_conditions;
        public InventoryDeltaEntry[] inventory_delta;
        public RecipeQuantity[] consumed;
        public RecipeQuantity[] produced;
        public ReactionGameplayEvent gameplay_event;
    }

    public static class AuthoritativeDataValidator
    {
        public static List<string> Validate(
            MigrationManifest manifest,
            RegionsDatabase regions,
            ChemistryDatabase chemistry,
            ReactionsDatabase reactions,
            ReactionContentDatabase content,
            DevicesDatabase devices,
            ProcessingRecipesDatabase processingRecipes,
            ShardsDatabase shards,
            MemoriesDatabase memories,
            LocalizationDatabase localization)
        {
            var errors = new List<string>();
            if (manifest == null || manifest.counts == null) return new List<string> { "manifest missing" };
            if (manifest.schema_version != 2) errors.Add("manifest schema_version != 2");
            if (string.IsNullOrWhiteSpace(manifest.source_commit)) errors.Add("manifest source_commit missing");

            var regionRows = regions != null && regions.regions != null ? regions.regions : new RegionDef[0];
            var materialRows = chemistry != null && chemistry.materials != null ? chemistry.materials : new MaterialDef[0];
            var recipeRows = chemistry != null && chemistry.reactions != null ? chemistry.reactions : new ReactionRecipe[0];
            var reactionRows = reactions != null && reactions.reactions != null ? reactions.reactions : new ReactionDef[0];
            var contentRows = content != null && content.reactions != null ? content.reactions : new ReactionContentDef[0];
            var deviceRows = devices != null && devices.devices != null ? devices.devices : new FormalDeviceDef[0];
            var processingRows = processingRecipes != null && processingRecipes.recipes != null
                ? processingRecipes.recipes : new ProcessingRecipeDef[0];
            var shardRows = shards != null && shards.shards != null ? shards.shards : new ShardDef[0];
            var memoryRows = memories != null && memories.memories != null ? memories.memories : new MemoryDef[0];

            CheckCount(errors, "regions", regionRows.Length, manifest.counts.regions, 17);
            CheckCount(errors, "reaction specs", recipeRows.Length, manifest.counts.reaction_specs, 40);
            CheckCount(errors, "reaction metadata", reactionRows.Length, manifest.counts.reaction_specs, 40);
            CheckCount(errors, "reaction content", contentRows.Length, manifest.counts.reaction_content_units, 40);
            CheckCount(errors, "formal devices", deviceRows.Length, manifest.counts.formal_devices, 15);
            CheckCount(errors, "processing recipes", processingRows.Length,
                manifest.counts.processing_recipes, manifest.counts.processing_recipes);
            CheckCount(errors, "materials", materialRows.Length, manifest.counts.materials, 54);

            CheckUnique(errors, "region", regionRows.Select(x => x.id));
            CheckUnique(errors, "material", materialRows.Select(x => x.id));
            CheckUnique(errors, "reaction spec", recipeRows.Select(x => x.id));
            CheckUnique(errors, "reaction metadata", reactionRows.Select(x => x.id));
            CheckUnique(errors, "reaction content", contentRows.Select(x => x.id));
            CheckUnique(errors, "device", deviceRows.Select(x => x.id));
            CheckUnique(errors, "processing recipe", processingRows.Select(x => x.id));

            var regionIds = new HashSet<string>(regionRows.Select(x => x.id));
            var materialIds = new HashSet<string>(materialRows.Select(x => x.id));
            var recipeIds = new HashSet<string>(recipeRows.Select(x => x.id));
            var processingIds = new HashSet<string>(processingRows.Select(x => x.id));
            var shardIds = new HashSet<string>(shardRows.Select(x => x.id));
            var memoryIds = new HashSet<string>(memoryRows.Select(x => x.id));
            var order = new List<string> { "prologue_lab" };
            if (regions != null && regions.order != null)
            {
                order.AddRange(regions.order.surface ?? new string[0]);
                order.AddRange(regions.order.underground ?? new string[0]);
            }
            if (!order.SequenceEqual(regionRows.Select(x => x.id))) errors.Add("region authoritative order mismatch");

            foreach (var region in regionRows)
                if (!string.IsNullOrEmpty(region.shard) && !shardIds.Contains(region.shard))
                    errors.Add("region shard missing: " + region.id + " -> " + region.shard);
            foreach (var recipe in recipeRows)
            {
                foreach (var quantity in (recipe.reactants ?? new RecipeQuantity[0]).Concat(recipe.products ?? new RecipeQuantity[0]))
                    if (!materialIds.Contains(quantity.id)) errors.Add("reaction material missing: " + recipe.id + " -> " + quantity.id);
            }
            CompareIdSets(errors, "reaction metadata", recipeIds, reactionRows.Select(x => x.id));
            CompareIdSets(errors, "reaction content", recipeIds, contentRows.Select(x => x.id));
            foreach (var unit in contentRows)
            {
                foreach (var environment in unit.source_environments ?? new string[0])
                    if (!regionIds.Contains(environment)) errors.Add("reaction environment missing: " + unit.id + " -> " + environment);
                foreach (var alternate in unit.alternate_reaction_ids ?? new string[0])
                    if (!recipeIds.Contains(alternate)) errors.Add("alternate reaction missing: " + unit.id + " -> " + alternate);
                if (!shardIds.Contains(unit.shard_id)) errors.Add("reaction shard missing: " + unit.id + " -> " + unit.shard_id);
                if (!memoryIds.Contains(unit.memory_id)) errors.Add("reaction memory missing: " + unit.id + " -> " + unit.memory_id);
            }
            for (var index = 0; index < deviceRows.Length; index++)
            {
                var device = deviceRows[index];
                if (device.slot != index + 1) errors.Add("device slot mismatch: " + device.id);
                if (device.stage < 0 || device.stage > 3) errors.Add("device stage invalid: " + device.id);
                if (device.crafting == null) errors.Add("device crafting contract missing: " + device.id);
                else if (!string.IsNullOrEmpty(device.crafting.unlock_region) && !regionIds.Contains(device.crafting.unlock_region))
                    errors.Add("device unlock region missing: " + device.id + " -> " + device.crafting.unlock_region);
                foreach (var processingId in device.recipe_ids ?? new string[0])
                    if (!processingIds.Contains(processingId))
                        errors.Add("device processing recipe missing: " + device.id + " -> " + processingId);
            }
            var deviceIds = new HashSet<string>(deviceRows.Select(x => x.id));
            foreach (var processing in processingRows)
            {
                if (!deviceIds.Contains(processing.device_id))
                    errors.Add("processing recipe device missing: " + processing.id + " -> " + processing.device_id);
                if (processing.required_stage < 0 || processing.required_stage > 3)
                    errors.Add("processing recipe stage invalid: " + processing.id);
                if (processing.output_items == null || processing.output_items.Length == 0)
                    errors.Add("processing recipe output missing: " + processing.id);
            }
            var entries = localization != null && localization.entries != null ? localization.entries : new LocEntry[0];
            CheckCount(errors, "localization", entries.Length, manifest.counts.localization_entries, manifest.counts.localization_entries);
            CheckUnique(errors, "localization key", entries.Select(x => x.key));
            return errors;
        }

        static void CheckCount(List<string> errors, string name, int actual, int manifest, int expected)
        {
            if (actual != manifest || actual != expected)
                errors.Add(name + " count mismatch: actual=" + actual + ", manifest=" + manifest + ", expected=" + expected);
        }

        static void CheckUnique(List<string> errors, string name, IEnumerable<string> values)
        {
            var rows = values.ToArray();
            if (rows.Any(string.IsNullOrEmpty)) errors.Add(name + " id/key empty");
            if (rows.Distinct().Count() != rows.Length) errors.Add(name + " ids/keys not unique");
        }

        static void CompareIdSets(List<string> errors, string name, HashSet<string> expected, IEnumerable<string> actual)
        {
            if (!expected.SetEquals(actual)) errors.Add(name + " ID set differs from reaction specs");
        }
    }
}
