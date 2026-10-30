using System;
using System.Linq;

namespace ElementalStarSea.Data
{
    // WorldSession CORE_REACTION_CHAIN, attempt_core_test and EndingState.
    public sealed class CoreProgressionService
    {
        public static readonly string[] ReactionChain = {
            "reaction_n2", "reaction_nh3_from_n2", "reaction_no", "reaction_no2", "reaction_hno3_from_no2"
        };
        readonly GameState state;
        public CoreProgressionService(GameState state) { this.state = state ?? throw new ArgumentNullException(nameof(state)); }
        public int CompletedSteps
        {
            get
            {
                var progress = state.coreReactionChainProgress ?? new string[0];
                int count = 0;
                while (count < Math.Min(progress.Length, ReactionChain.Length) && progress[count] == ReactionChain[count]) count++;
                return count;
            }
        }
        public string NextReaction { get { return CompletedSteps < ReactionChain.Length ? ReactionChain[CompletedSteps] : ""; } }
        public void RecordCommittedReaction(string reactionId)
        {
            if (string.IsNullOrEmpty(NextReaction) || reactionId != NextReaction) return;
            state.coreReactionChainProgress = ReactionChain.Take(CompletedSteps + 1).ToArray();
        }
        public string TestFailure()
        {
            if (state.currentRegion != "planetary_core") return "planetary_core_required";
            if ((state.reconstructedMemories ?? new string[0]).Where(id => !string.IsNullOrEmpty(id)).Distinct().Count() < 8)
                return "eight_memories_required";
            if ((state.calmedShards ?? new string[0]).Where(id => !string.IsNullOrEmpty(id)).Distinct().Count() < 8)
                return "eight_master_keys_required";
            if (CompletedSteps != ReactionChain.Length) return "core_reaction_chain_required";
            return "";
        }
        public bool AttemptCoreTest()
        {
            if (state.coreTestPassed || TestFailure() != "") return false;
            state.coreTestPassed = true;
            return true;
        }
        public bool ChooseEnding(string choice)
        {
            if (!state.coreTestPassed || !string.IsNullOrEmpty(state.endingChoice) ||
                (choice != "sealed" && choice != "fused")) return false;
            state.endingChoice = choice;
            return true;
        }
    }
}
