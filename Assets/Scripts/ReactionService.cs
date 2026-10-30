// Godot FullReactionService 的 Unity 对等实现：预览、条件检查、原子提交和正式事件。
using System;
using System.Collections.Generic;
using System.Linq;

namespace ElementalStarSea.Data
{
    public interface IReactionState : IInventoryView
    {
        float SynthesisEnergy { get; }
        bool TryCommitReaction(IEnumerable<InventoryDeltaEntry> delta, float energyCost);
        void AddReaction(string reactionId);
        void AddLanguage(string languageId);
    }

    public sealed class ReactionService
    {
        readonly IReactionState _state;
        public event Action<ReactionGameplayEvent> GameplayEventPublished;

        public ReactionService(IReactionState state)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
        }

        public ReactionExecutionResult Preview(string reactionId, IEnumerable<string> environmentTags)
        {
            var recipe = ChemistryEngine.Recipe(reactionId);
            if (!IsValid(recipe)) return Failed(reactionId, ReactionFailure.InvalidDefinition, 0f);
            float cost = ChemistryEngine.EnergyCost(recipe);
            var delta = BuildDelta(recipe);
            var required = Aggregate(recipe.reactants);
            if (required.Any(pair => _state.CountItem(pair.Key) < pair.Value))
                return Failed(recipe.id, ReactionFailure.MissingReactant, cost, delta);

            var available = new HashSet<string>(environmentTags ?? Enumerable.Empty<string>());
            var missing = (recipe.tags ?? new string[0]).Where(tag => !available.Contains(tag)).ToList();
            var alternatives = recipe.any_tags ?? new string[0];
            if (alternatives.Length > 0 && !alternatives.Any(available.Contains))
                missing.Add(string.Join("|", alternatives));
            if (missing.Count > 0)
                return Failed(recipe.id, ReactionFailure.MissingEnvironment, cost, delta, missing.ToArray());
            if (_state.SynthesisEnergy < cost)
                return Failed(recipe.id, ReactionFailure.InsufficientEnergy, cost, delta);
            return SuccessPreview(recipe, cost, delta);
        }

        public ReactionExecutionResult Execute(string reactionId, IEnumerable<string> environmentTags)
        {
            var result = Preview(reactionId, environmentTags);
            if (!result.success)
            {
                result.gameplay_event = PublishFailure(result);
                return result;
            }
            if (!_state.TryCommitReaction(result.inventory_delta, result.energy_cost))
            {
                result.success = false;
                result.kind = ReactionResultKind.CommitFailed;
                result.failure = ReactionFailure.None;
                result.gameplay_event = PublishFailure(result, "commit_failed");
                return result;
            }

            var recipe = ChemistryEngine.Recipe(reactionId);
            _state.AddReaction(recipe.id);
            _state.AddLanguage(recipe.language);
            var gameState = _state as GameState;
            if (gameState != null) new CoreProgressionService(gameState).RecordCommittedReaction(recipe.id);
            if (gameState != null && recipe.id == "reaction_h2_synthesis") gameState.GrantAbility("ability_h2_hover");
            result.gameplay_event = new ReactionGameplayEvent
            {
                event_type = "reaction_committed",
                reaction_id = recipe.id,
                reason = "",
                energy_spent = result.energy_cost,
                inventory_delta = CloneDelta(result.inventory_delta),
                language_id = recipe.language
            };
            Publish(result.gameplay_event);
            return result;
        }

        static bool IsValid(ReactionRecipe recipe)
        {
            if (recipe == null || string.IsNullOrEmpty(recipe.id) || string.IsNullOrEmpty(recipe.branch) ||
                recipe.reactants == null || recipe.reactants.Length == 0 ||
                recipe.products == null || recipe.products.Length == 0) return false;
            if ((recipe.tags ?? new string[0]).Any(string.IsNullOrEmpty) ||
                (recipe.any_tags ?? new string[0]).Any(string.IsNullOrEmpty)) return false;
            if (recipe.reactants.Concat(recipe.products).Any(q => q == null || string.IsNullOrEmpty(q.id) || q.n <= 0))
                return false;
            return BuildDelta(recipe).Length > 0;
        }

        static Dictionary<string, int> Aggregate(IEnumerable<RecipeQuantity> quantities)
        {
            var values = new Dictionary<string, int>();
            foreach (var quantity in quantities)
            {
                int current;
                values.TryGetValue(quantity.id, out current);
                values[quantity.id] = checked(current + quantity.n);
            }
            return values;
        }

        static InventoryDeltaEntry[] BuildDelta(ReactionRecipe recipe)
        {
            var values = new Dictionary<string, int>();
            Action<RecipeQuantity, int> add = (quantity, sign) =>
            {
                int current;
                values.TryGetValue(quantity.id, out current);
                values[quantity.id] = checked(current + sign * quantity.n);
            };
            foreach (var quantity in recipe.reactants) add(quantity, -1);
            foreach (var quantity in recipe.products) add(quantity, 1);
            return values.Where(pair => pair.Value != 0)
                .OrderBy(pair => pair.Key)
                .Select(pair => new InventoryDeltaEntry(pair.Key, pair.Value)).ToArray();
        }

        static ReactionExecutionResult SuccessPreview(ReactionRecipe recipe, float cost, InventoryDeltaEntry[] delta)
        {
            return new ReactionExecutionResult
            {
                success = true,
                kind = ReactionResultKind.Success,
                failure = ReactionFailure.None,
                reaction_id = recipe.id,
                energy_cost = cost,
                missing_conditions = new string[0],
                inventory_delta = CloneDelta(delta),
                consumed = CloneQuantities(recipe.reactants),
                produced = CloneQuantities(recipe.products)
            };
        }

        static ReactionExecutionResult Failed(string reactionId, ReactionFailure failure, float cost,
            InventoryDeltaEntry[] delta = null, string[] missing = null)
        {
            return new ReactionExecutionResult
            {
                success = false,
                kind = ReactionResultKind.PreconditionFailed,
                failure = failure,
                reaction_id = reactionId ?? "",
                energy_cost = cost,
                missing_conditions = missing ?? new string[0],
                inventory_delta = CloneDelta(delta ?? new InventoryDeltaEntry[0]),
                consumed = new RecipeQuantity[0],
                produced = new RecipeQuantity[0]
            };
        }

        ReactionGameplayEvent PublishFailure(ReactionExecutionResult result, string reason = null)
        {
            var failure = new ReactionGameplayEvent
            {
                event_type = "experiment_failed",
                reaction_id = result.reaction_id,
                reason = reason ?? FailureToken(result.failure),
                energy_spent = 0f,
                inventory_delta = new InventoryDeltaEntry[0],
                language_id = ""
            };
            Publish(failure);
            return failure;
        }

        void Publish(ReactionGameplayEvent gameplayEvent)
        {
            var handler = GameplayEventPublished;
            if (handler == null) return;
            foreach (Action<ReactionGameplayEvent> subscriber in handler.GetInvocationList())
                subscriber(CloneEvent(gameplayEvent));
        }

        public static string FailureToken(ReactionFailure failure)
        {
            switch (failure)
            {
                case ReactionFailure.MissingReactant: return "missing_reactant";
                case ReactionFailure.MissingEnvironment: return "missing_environment";
                case ReactionFailure.InsufficientEnergy: return "insufficient_energy";
                case ReactionFailure.InvalidDefinition: return "invalid_definition";
                default: return "none";
            }
        }

        static InventoryDeltaEntry[] CloneDelta(IEnumerable<InventoryDeltaEntry> values)
        {
            return values.Select(x => new InventoryDeltaEntry(x.id, x.change)).ToArray();
        }

        static ReactionGameplayEvent CloneEvent(ReactionGameplayEvent value)
        {
            return new ReactionGameplayEvent
            {
                event_type = value.event_type,
                reaction_id = value.reaction_id,
                reason = value.reason,
                energy_spent = value.energy_spent,
                inventory_delta = CloneDelta(value.inventory_delta ?? new InventoryDeltaEntry[0]),
                language_id = value.language_id
            };
        }

        static RecipeQuantity[] CloneQuantities(IEnumerable<RecipeQuantity> values)
        {
            return values.Select(x => new RecipeQuantity { id = x.id, n = x.n }).ToArray();
        }
    }
}
