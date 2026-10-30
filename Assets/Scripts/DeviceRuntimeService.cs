// 15 台正式设备共享的制造、放置、加工和输出领取运行时。
using System;
using System.Collections.Generic;
using System.Linq;

namespace ElementalStarSea.Data
{
    public sealed class DeviceRuntimeService
    {
        readonly GameState _state;
        readonly Dictionary<string, FormalDeviceDef> _devices;
        readonly Dictionary<string, ProcessingRecipeDef> _recipes;

        public event Action<DeviceGameplayEvent> GameplayEventPublished;

        public FormalDeviceDef[] Devices { get { return _devices.Values.OrderBy(value => value.slot).ToArray(); } }
        public ProcessingRecipeDef[] RecipesFor(string deviceId)
        {
            return _recipes.Values.Where(value => value.device_id == deviceId).OrderBy(value => value.id).ToArray();
        }

        public DeviceRuntimeService(GameState state, DevicesDatabase devices, ProcessingRecipesDatabase recipes)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _devices = (devices != null && devices.devices != null ? devices.devices : new FormalDeviceDef[0])
                .ToDictionary(value => value.id, value => value);
            _recipes = (recipes != null && recipes.recipes != null ? recipes.recipes : new ProcessingRecipeDef[0])
                .ToDictionary(value => value.id, value => value);
        }

        public DeviceOperationResult Craft(string deviceId)
        {
            FormalDeviceDef device;
            if (!_devices.TryGetValue(deviceId ?? "", out device)) return Failure("unknown_device", deviceId);
            if (!string.Equals(device.resource_status, "READY", StringComparison.OrdinalIgnoreCase))
                return Failure("device_resource_pending", device.id);
            if (_state.HasCraftedDevice(device.id)) return Failure("already_crafted", device.id);
            var crafting = device.crafting;
            if (crafting == null) return Failure("crafting_contract_missing", device.id);
            if (_state.laboratoryStage < crafting.unlock_lab_stage) return Failure("laboratory_stage_locked", device.id);
            // Godot craft_phase4b_device + set_phase4b_device_operational support
            // workbench kits before the region-gated direct-crafting route.
            string kit = device.id == "device_large_industrial_liquid_tank" ? "item_large_industrial_liquid_tank_kit" :
                device.id == "device_high_pressure_catalytic_reactor" ? "item_high_pressure_catalytic_reactor_kit" : "";
            bool useKit = !string.IsNullOrEmpty(kit) && _state.CountItem(kit) > 0;
            if (!useKit && !string.IsNullOrEmpty(crafting.unlock_region) && !_state.HasVisitedRegion(crafting.unlock_region))
                return Failure("unlock_region_required", device.id);
            if (string.Equals(crafting.crafting_station, "WORKBENCH", StringComparison.OrdinalIgnoreCase) &&
                !_state.HasCraftedDevice("device_chemical_workbench"))
                return Failure("workbench_required", device.id);
            if (crafting.unlock_condition_id == "device_chemical_workbench_built" &&
                !_state.HasCraftedDevice("device_chemical_workbench"))
                return Failure("unlock_condition_missing", device.id);

            var cost = useKit ? new[] { new RecipeQuantity { id = kit, n = 1 } } : crafting.inputs ?? new RecipeQuantity[0];
            if (cost.Any(value => _state.CountItem(value.id) < value.n))
                return Failure("missing_crafting_input", device.id);
            if (!_state.TryApplyInventoryDelta(cost.Select(value => new InventoryDeltaEntry(value.id, -value.n))))
                return Failure("inventory_rejected", device.id);
            _state.AddCraftedDevice(device.id);
            Publish("device_built", device.id, "", "", "", cost);
            return Success(device.id);
        }

        public DeviceOperationResult Place(string deviceId, string instanceId = null,
            string regionId = "", float worldX = 0f, float worldY = 0f)
        {
            FormalDeviceDef device;
            if (!_devices.TryGetValue(deviceId ?? "", out device)) return Failure("unknown_device", deviceId);
            if (!_state.HasCraftedDevice(device.id)) return Failure("device_not_built", device.id);
            string resolved = string.IsNullOrEmpty(instanceId)
                ? "device_instance_" + device.id + "_" + ((_state.deviceInstances ?? new DeviceRuntimeContract[0]).Length + 1)
                : instanceId;
            if (_state.DeviceInstance(resolved) != null) return Failure("instance_id_exists", device.id, resolved);
            var runtime = new DeviceRuntimeContract
            {
                instance_id = resolved,
                device_id = device.id,
                is_unlocked = true,
                is_placed = true,
                state = DeviceProcessingState.Idle,
                active_recipe_id = "",
                reserved_inputs = new RecipeQuantity[0],
                buffered_outputs = new RecipeQuantity[0],
                error_reason = "",
                region_id = regionId ?? "",
                world_x = worldX,
                world_y = worldY,
                stored_item_id = "",
                stored_amount = 0,
                storage_capacity = Math.Max(0, device.storage_capacity)
            };
            _state.UpsertDeviceInstance(runtime);
            Publish("device_placed", device.id, resolved, "", "", new RecipeQuantity[0]);
            return Success(device.id, resolved, runtime);
        }

        public DeviceOperationResult Store(string instanceId, string itemId, int amount = 1)
        {
            var runtime = _state.DeviceInstance(instanceId);
            if (runtime == null || !runtime.is_placed) return Failure("device_not_operational", "", instanceId);
            FormalDeviceDef device;
            if (!_devices.TryGetValue(runtime.device_id ?? "", out device) || device.storage_capacity <= 0)
                return Failure("invalid_storage_request", runtime.device_id, instanceId, "", runtime);
            if (amount <= 0 || string.IsNullOrEmpty(itemId))
                return Failure("invalid_storage_request", runtime.device_id, instanceId, "", runtime);
            if (device.processing_type == "store_liquid" && !SpecializedDeviceChemistryService.IsLiquid(itemId))
                return Failure("invalid_physical_state", runtime.device_id, instanceId, "", runtime);
            if (!string.IsNullOrEmpty(runtime.stored_item_id) && runtime.stored_item_id != itemId)
                return Failure("incompatible_stored_item", runtime.device_id, instanceId, "", runtime);
            int capacity = runtime.storage_capacity > 0 ? runtime.storage_capacity : device.storage_capacity;
            if (runtime.stored_amount + amount > capacity)
                return Failure("storage_capacity_exceeded", runtime.device_id, instanceId, "", runtime);
            if (_state.CountItem(itemId) < amount ||
                !_state.TryApplyInventoryDelta(new[] { new InventoryDeltaEntry(itemId, -amount) }))
                return Failure("missing_input", runtime.device_id, instanceId, "", runtime);
            runtime.stored_item_id = itemId;
            runtime.stored_amount += amount;
            runtime.storage_capacity = capacity;
            _state.AddChemistryKnowledge("bulk_liquid_stored");
            _state.UpsertDeviceInstance(runtime);
            Publish("device_storage_deposited", runtime.device_id, instanceId, "", "", new[] { new RecipeQuantity { id = itemId, n = amount } });
            return Success(runtime.device_id, instanceId, runtime);
        }

        public DeviceOperationResult Withdraw(string instanceId, int amount = 1)
        {
            var runtime = _state.DeviceInstance(instanceId);
            if (runtime == null || amount <= 0 || string.IsNullOrEmpty(runtime.stored_item_id) || runtime.stored_amount < amount)
                return Failure("stored_item_unavailable", runtime != null ? runtime.device_id : "", instanceId, "", runtime);
            string itemId = runtime.stored_item_id;
            if (!_state.TryApplyInventoryDelta(new[] { new InventoryDeltaEntry(itemId, amount) }))
                return Failure("inventory_rejected", runtime.device_id, instanceId, "", runtime);
            runtime.stored_amount -= amount;
            if (runtime.stored_amount == 0) runtime.stored_item_id = "";
            _state.AddChemistryKnowledge("bulk_liquid_reused");
            _state.UpsertDeviceInstance(runtime);
            Publish("device_storage_withdrawn", runtime.device_id, instanceId, "", "", new[] { new RecipeQuantity { id = itemId, n = amount } });
            return Success(runtime.device_id, instanceId, runtime);
        }

        public DeviceOperationResult Start(string instanceId, string recipeId,
            IDictionary<string, string> conditions = null)
        {
            var runtime = _state.DeviceInstance(instanceId);
            if (runtime == null || !runtime.is_placed) return Failure("device_not_operational", "", instanceId, recipeId);
            FormalDeviceDef device;
            ProcessingRecipeDef recipe;
            if (!_devices.TryGetValue(runtime.device_id, out device) || !_recipes.TryGetValue(recipeId ?? "", out recipe))
                return MarkFailure(runtime, "invalid_recipe", false, recipeId);
            // Godot 的统一入口以 recipe.device_id 为路由依据；部分合法熔炉配方未重复列入
            // FormalDeviceCatalog.recipe_ids，因此这里不得把该展示列表当作执行白名单。
            if (recipe.device_id != device.id)
                return MarkFailure(runtime, "device_id_mismatch", false, recipe.id);
            if (runtime.state == DeviceProcessingState.Processing) return Failure("already_processing", device.id, instanceId, recipe.id, runtime);
            if (runtime.state == DeviceProcessingState.OutputReady) return Failure("output_not_collected", device.id, instanceId, recipe.id, runtime);
            if (_state.laboratoryStage < device.stage || _state.laboratoryStage < recipe.required_stage)
                return MarkFailure(runtime, "laboratory_stage_locked", true, recipe.id);

            var provided = conditions ?? new Dictionary<string, string>();
            foreach (var condition in recipe.required_conditions ?? new ConditionValue[0])
            {
                string actual;
                if (condition.id == "required_stage") continue;
                if (!provided.TryGetValue(condition.id, out actual) || !SameValue(actual, condition.value))
                    return MarkFailure(runtime, "condition_missing:" + condition.id, true, recipe.id);
            }

            RecipeQuantity[] selected;
            RecipeQuantity[] processInputs;
            RecipeQuantity[] outputs;
            Action applySpecializedState = null;
            string analyzedSample = "";
            if ((recipe.progression_tags ?? new string[0]).Contains("runtime_service_only"))
            {
                var plan = SpecializedDeviceChemistryService.Build(_state, recipe, provided);
                if (!plan.success) return MarkFailure(runtime, plan.reason, plan.conditionFailure, recipe.id);
                selected = plan.consumed ?? new RecipeQuantity[0];
                processInputs = plan.processInputs ?? new RecipeQuantity[0];
                outputs = plan.outputs ?? new RecipeQuantity[0];
                applySpecializedState = plan.applyState;
                analyzedSample = plan.analyzed_sample_id;
            }
            else
            {
                selected = SelectConsumableInputs(recipe);
                if (selected == null) return MarkFailure(runtime, "missing_input", false, recipe.id);
                processInputs = recipe.process_inputs ?? new RecipeQuantity[0];
                outputs = recipe.output_items ?? new RecipeQuantity[0];
            }
            foreach (var processInput in processInputs)
                if (_state.CountItem(processInput.id) < processInput.n)
                    return MarkFailure(runtime, "missing_process_input:" + processInput.id, false, recipe.id);
            if (!_state.TryApplyInventoryDelta(selected.Select(value => new InventoryDeltaEntry(value.id, -value.n))))
                return MarkFailure(runtime, "inventory_rejected", false, recipe.id);
            if (applySpecializedState != null) applySpecializedState();

            runtime.active_recipe_id = recipe.id;
            runtime.state = DeviceProcessingState.Processing;
            runtime.progress = 0f;
            runtime.elapsed = 0f;
            runtime.duration = Math.Max(0.1f, recipe.processing_time);
            runtime.reserved_inputs = CloneQuantities(selected.Concat(processInputs));
            runtime.buffered_outputs = CloneQuantities(outputs);
            runtime.error_reason = "";
            _state.UpsertDeviceInstance(runtime);
            Publish("device_processing_started", device.id, runtime.instance_id, recipe.id, "", runtime.reserved_inputs, analyzedSample);
            return Success(device.id, runtime.instance_id, runtime, recipe.id);
        }

        public int Advance(float deltaSeconds)
        {
            if (deltaSeconds <= 0f) return 0;
            int completed = 0;
            foreach (var runtime in (_state.deviceInstances ?? new DeviceRuntimeContract[0]).Where(value => value != null).ToArray())
            {
                if (runtime.state != DeviceProcessingState.Processing) continue;
                runtime.elapsed += deltaSeconds;
                runtime.progress = Math.Min(1f, runtime.elapsed / Math.Max(0.1f, runtime.duration));
                if (runtime.progress >= 1f)
                {
                    runtime.state = DeviceProcessingState.OutputReady;
                    completed++;
                    Publish("device_output_ready", runtime.device_id, runtime.instance_id,
                        runtime.active_recipe_id, "", runtime.buffered_outputs);
                }
                _state.UpsertDeviceInstance(runtime);
            }
            return completed;
        }

        public DeviceOperationResult Collect(string instanceId)
        {
            var runtime = _state.DeviceInstance(instanceId);
            if (runtime == null) return Failure("unknown_instance", "", instanceId);
            if (runtime.state != DeviceProcessingState.OutputReady)
                return Failure("output_not_ready", runtime.device_id, instanceId, runtime.active_recipe_id, runtime);
            var outputs = runtime.buffered_outputs ?? new RecipeQuantity[0];
            if (!_state.TryApplyInventoryDelta(outputs.Select(value => new InventoryDeltaEntry(value.id, value.n))))
            {
                runtime.state = DeviceProcessingState.Error;
                runtime.error_reason = "output_commit_failed";
                _state.UpsertDeviceInstance(runtime);
                return Failure("output_commit_failed", runtime.device_id, instanceId, runtime.active_recipe_id, runtime);
            }
            string recipeId = runtime.active_recipe_id;
            var delivered = CloneQuantities(outputs);
            runtime.state = DeviceProcessingState.Idle;
            runtime.active_recipe_id = "";
            runtime.progress = 0f;
            runtime.elapsed = 0f;
            runtime.duration = 0f;
            runtime.reserved_inputs = new RecipeQuantity[0];
            runtime.buffered_outputs = new RecipeQuantity[0];
            runtime.error_reason = "";
            _state.UpsertDeviceInstance(runtime);
            Publish("device_output_collected", runtime.device_id, instanceId, recipeId, "", delivered);
            return Success(runtime.device_id, instanceId, runtime, recipeId);
        }

        RecipeQuantity[] SelectConsumableInputs(ProcessingRecipeDef recipe)
        {
            var candidates = new List<RecipeQuantity[]> { recipe.input_items ?? new RecipeQuantity[0] };
            candidates.AddRange((recipe.alternate_input_sets ?? new RecipeQuantitySet[0])
                .Where(value => value != null).Select(value => value.items ?? new RecipeQuantity[0]));
            return candidates.FirstOrDefault(set => set.Length > 0 && set.All(value => _state.CountItem(value.id) >= value.n));
        }

        DeviceOperationResult MarkFailure(DeviceRuntimeContract runtime, string reason, bool condition, string recipeId)
        {
            runtime.active_recipe_id = recipeId ?? "";
            runtime.state = condition ? DeviceProcessingState.ConditionMissing : DeviceProcessingState.InvalidInput;
            runtime.progress = 0f;
            runtime.elapsed = 0f;
            runtime.duration = 0f;
            runtime.reserved_inputs = new RecipeQuantity[0];
            runtime.buffered_outputs = new RecipeQuantity[0];
            runtime.error_reason = reason;
            _state.UpsertDeviceInstance(runtime);
            Publish("device_processing_failed", runtime.device_id, runtime.instance_id, recipeId, reason, new RecipeQuantity[0]);
            return Failure(reason, runtime.device_id, runtime.instance_id, recipeId, runtime);
        }

        static bool SameValue(string left, string right)
        {
            return string.Equals(left ?? "", right ?? "", StringComparison.OrdinalIgnoreCase);
        }

        void Publish(string type, string deviceId, string instanceId, string recipeId,
            string reason, IEnumerable<RecipeQuantity> items, string analyzedSample = "")
        {
            var handler = GameplayEventPublished;
            if (handler == null) return;
            var value = new DeviceGameplayEvent
            {
                event_type = type, device_id = deviceId ?? "", instance_id = instanceId ?? "",
                recipe_id = recipeId ?? "", reason = reason ?? "", items = CloneQuantities(items), analyzed_sample_id = analyzedSample
            };
            foreach (Action<DeviceGameplayEvent> subscriber in handler.GetInvocationList())
                subscriber(new DeviceGameplayEvent
                {
                    event_type = value.event_type, device_id = value.device_id, instance_id = value.instance_id,
                    recipe_id = value.recipe_id, reason = value.reason, items = CloneQuantities(value.items), analyzed_sample_id = value.analyzed_sample_id
                });
        }

        static DeviceOperationResult Success(string deviceId, string instanceId = "",
            DeviceRuntimeContract state = null, string recipeId = "")
        {
            return new DeviceOperationResult { success = true, reason = "", device_id = deviceId ?? "",
                instance_id = instanceId ?? "", recipe_id = recipeId ?? "", state = state };
        }

        static DeviceOperationResult Failure(string reason, string deviceId, string instanceId = "",
            string recipeId = "", DeviceRuntimeContract state = null)
        {
            return new DeviceOperationResult { success = false, reason = reason, device_id = deviceId ?? "",
                instance_id = instanceId ?? "", recipe_id = recipeId ?? "", state = state };
        }

        static RecipeQuantity[] CloneQuantities(IEnumerable<RecipeQuantity> values)
        {
            return (values ?? Enumerable.Empty<RecipeQuantity>())
                .Select(value => new RecipeQuantity { id = value.id, n = value.n }).ToArray();
        }
    }
}
