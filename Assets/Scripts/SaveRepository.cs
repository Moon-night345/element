using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ElementalStarSea.Data
{
    [Serializable] public sealed class SaveEnvelope
    {
        public int schema_version;
        public string save_id, saved_at_utc, payload_json, checksum;
    }
    public sealed class SaveReadResult
    {
        public bool success, exists, legacy;
        public string reason = "", recovery = "";
        public GameState state;
    }
    public sealed class SaveSlotInfo
    {
        public string id, region, status;
        public int stage;
        public bool loadable;
    }

    // Godot SaveRepository semantics, with a Unity JsonUtility payload envelope.
    // The checksum is over the exact UTF-8 payload JSON, not envelope formatting.
    public sealed class SaveRepository
    {
        public const int SchemaVersion = 1;
        static readonly object Gate = new object();
        readonly string path, saveId;
        readonly Action beforeCommit;
        readonly bool forceSnapshotHistory;
        string HistoryDirectory { get { return path + ".history"; } }
        public SaveRepository(string path, string saveId, Action beforeCommit = null, bool forceSnapshotHistory = false)
        {
            this.path = Path.GetFullPath(path);
            this.saveId = saveId;
            this.beforeCommit = beforeCommit;
            this.forceSnapshotHistory = forceSnapshotHistory;
        }
        string[] HistoryFiles()
        {
            if (!Directory.Exists(HistoryDirectory)) return new string[0];
            return Directory.GetFiles(HistoryDirectory, "*.json")
                .Where(f => Regex.IsMatch(Path.GetFileName(f), "^[0-9]{19}_[0-9a-f]{32}\\.json$"))
                .OrderByDescending(f => Path.GetFileName(f), StringComparer.Ordinal).ToArray();
        }
        bool UseSnapshotHistory()
        {
            // Append-only commits never replace existing files or change EFS/ACLs.
            // Once committed history exists, it remains authoritative if attributes change.
            return forceSnapshotHistory || HistoryFiles().Length > 0 ||
                (File.GetAttributes(Path.GetDirectoryName(path)) & FileAttributes.Encrypted) != 0;
        }
        void CommitSnapshot(string text)
        {
            Directory.CreateDirectory(HistoryDirectory);
            var previous = HistoryFiles();
            long last = previous.Length == 0 ? 0 : long.Parse(Path.GetFileName(previous[0]).Substring(0, 19));
            long sequence = Math.Max(DateTime.UtcNow.Ticks, checked(last + 1));
            string destination = Path.Combine(HistoryDirectory, sequence.ToString("D19", System.Globalization.CultureInfo.InvariantCulture) +
                "_" + Guid.NewGuid().ToString("N") + ".json");
            string temporary = destination + ".tmp";
            WriteFlushed(temporary, text);
            if (!Inspect(temporary).success) throw new IOException("temporary_verification_failed");
            if (beforeCommit != null) beforeCommit();
            // Same-directory rename to an absent filename is the commit point.
            // Readers ignore all .tmp files, even valid ones left by interruptions.
            File.Move(temporary, destination);
        }
        static void WriteFlushed(string destination, string text)
        {
            using (var file = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                byte[] bytes = new UTF8Encoding(false).GetBytes(text);
                file.Write(bytes, 0, bytes.Length); file.Flush(true);
            }
        }
        static string Digest(string text)
        {
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant();
        }
        public bool Write(GameState state, out string reason)
        {
            lock (Gate)
            {
                reason = "";
                try
                {
                    if (!ValidState(state)) { reason = "invalid_state"; return false; }
                    string payload = JsonUtility.ToJson(state);
                    string text = JsonUtility.ToJson(new SaveEnvelope { schema_version = SchemaVersion, save_id = saveId,
                        saved_at_utc = DateTime.UtcNow.ToString("o"), payload_json = payload, checksum = Digest(payload) }, true);
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    // A future format must never be obscured by a new generation.
                    if (Inspect(path).reason == "unsupported_schema" ||
                        HistoryFiles().Take(1).Any(f => Inspect(f).reason == "unsupported_schema"))
                    { reason = "unsupported_schema"; return false; }
                    if (UseSnapshotHistory()) { CommitSnapshot(text); return true; }
                    string temporary = path + ".tmp";
                    WriteFlushed(temporary, text);
                    if (!Inspect(temporary).success) { reason = "temporary_verification_failed"; return false; }
                    if (beforeCommit != null) beforeCommit();
                    if (File.Exists(path))
                    {
                        var current = Inspect(path);
                        if (current.reason == "unsupported_schema") { reason = current.reason; return false; }
                        if (current.success)
                        {
                            if (Inspect(path + ".bak1").success) File.Copy(path + ".bak1", path + ".bak2", true);
                            File.Replace(temporary, path, path + ".bak1");
                        }
                        else
                        {
                            // Never rotate a damaged main file over the healthy recovery chain.
                            File.Replace(temporary, path, path + ".corrupt_" + Guid.NewGuid().ToString("N"));
                        }
                    }
                    else File.Move(temporary, path);
                    return true;
                }
                catch (Exception error) { reason = error.GetType().Name + ": " + error.Message; return false; }
            }
        }
        public SaveReadResult Read()
        {
            lock (Gate)
            {
                var main = Inspect(path);
                if (main.reason == "unsupported_schema") return main;
                var history = HistoryFiles();
                if (history.Length > 0)
                {
                    // Current + two recovery generations. Legacy main/backups are the
                    // initial recovery chain while fewer than three snapshots exist.
                    var candidates = history.Concat(new[] { path, path + ".bak1", path + ".bak2" }).Take(3).ToArray();
                    SaveReadResult latest = null;
                    for (int i = 0; i < candidates.Length; i++)
                    {
                        var candidate = Inspect(candidates[i]);
                        if (i == 0) latest = candidate;
                        if (candidate.reason == "unsupported_schema") return candidate;
                        if (!candidate.success) continue;
                        candidate.recovery = i == 0 ? "" : "history" + i;
                        return candidate;
                    }
                    latest.exists = true;
                    return latest;
                }
                if (main.success) return main;
                foreach (var suffix in new[] { ".bak1", ".bak2" })
                {
                    var candidate = Inspect(path + suffix);
                    if (!candidate.success) continue;
                    candidate.exists = true; candidate.recovery = suffix.Substring(1); return candidate;
                }
                main.exists = File.Exists(path) || File.Exists(path + ".bak1") || File.Exists(path + ".bak2");
                return main;
            }
        }
        public SaveReadResult Inspect(string file)
        {
            var result = new SaveReadResult { exists = File.Exists(file), reason = "not_found" };
            if (!result.exists) return result;
            try
            {
                string text = File.ReadAllText(file, Encoding.UTF8).Trim();
                if (!text.StartsWith("{") || !text.EndsWith("}")) { result.reason = "invalid_json"; return result; }
                string payload;
                if (Regex.IsMatch(text, "\"(?:schema_version|payload_json|checksum)\"\\s*:"))
                {
                    var envelope = JsonUtility.FromJson<SaveEnvelope>(text);
                    if (envelope == null) { result.reason = "invalid_envelope"; return result; }
                    if (envelope.schema_version != SchemaVersion) { result.reason = "unsupported_schema"; return result; }
                    if (envelope.save_id != saveId || string.IsNullOrEmpty(envelope.payload_json) ||
                        envelope.checksum != Digest(envelope.payload_json)) { result.reason = "checksum_or_id_mismatch"; return result; }
                    payload = envelope.payload_json;
                }
                else
                {
                    // Only accept the old complete Unity GameState shape; {} is not a new game.
                    if (!Regex.IsMatch(text, "\"inventoryIds\"\\s*:") || !Regex.IsMatch(text, "\"inventoryCounts\"\\s*:") ||
                        !Regex.IsMatch(text, "\"currentRegion\"\\s*:"))
                    { result.reason = "invalid_legacy_shape"; return result; }
                    payload = text; result.legacy = true;
                }
                var state = JsonUtility.FromJson<GameState>(payload);
                if (!ValidState(state)) { result.reason = "invalid_state"; return result; }
                result.state = state; result.success = true; result.reason = "";
            }
            catch (Exception error) { result.reason = "read_failed: " + error.GetType().Name; }
            return result;
        }
        static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        static bool ValidState(GameState s)
        {
            if (s != null && !CombatRuntimeService.Valid(s.combat)) return false;
            if (s != null && s.radiationMedkits < 0) return false;
            if (s != null && s.unlockedAbilityIds != null && (s.unlockedAbilityIds.Any(string.IsNullOrEmpty) || s.unlockedAbilityIds.Distinct().Count() != s.unlockedAbilityIds.Length)) return false;
            if (s == null || !Finite(s.hp) || !Finite(s.energy) || !Finite(s.radiation) || !Finite(s.radiationProtection) ||
                s.laboratoryStage < 0 || s.laboratoryStage > 3 || s.questStep < 0 || string.IsNullOrEmpty(s.currentRegion) ||
                s.inventoryIds == null || s.inventoryCounts == null || s.inventoryIds.Length != s.inventoryCounts.Length ||
                s.inventoryIds.Any(string.IsNullOrEmpty) || s.inventoryIds.Distinct().Count() != s.inventoryIds.Length ||
                s.inventoryCounts.Any(n => n <= 0) || (s.hasPlayerPosition && (!Finite(s.playerWorldX) || !Finite(s.playerWorldY)))) return false;
            var deltas = s.terrainChanges ?? new TerrainCellChange[0];
            var agent = s.agent;
            if (agent != null && (agent.event_order < 0 || agent.clock_ms < 0 || agent.last_dialogue_ms > agent.clock_ms ||
                agent.shown == null || agent.recent_event_ids == null || agent.seen_materials == null || agent.cooldowns == null ||
                agent.recent_event_ids.Length > 64 || agent.shown.Any(string.IsNullOrEmpty) || agent.recent_event_ids.Any(string.IsNullOrEmpty) ||
                agent.shown.Distinct().Count() != agent.shown.Length || agent.recent_event_ids.Distinct().Count() != agent.recent_event_ids.Length ||
                agent.cooldowns.Any(c => c == null || string.IsNullOrEmpty(c.key) || c.time_ms < 0 || c.time_ms > agent.clock_ms) ||
                agent.cooldowns.Select(c => c.key).Distinct().Count() != agent.cooldowns.Length ||
                agent.repeated_mining < 0 || agent.analysis_failures < 0 || agent.unknown_hint_level < 0)) return false;
            var sources = s.resourceSources ?? new ResourceSourceState[0];
            var rust = agent == null ? null : agent.rust;
            if (rust != null && (rust.exposure_count < 0 || rust.failed_attempts < 0 || rust.stall_hint_level < 0 || rust.stall_hint_level > 3 ||
                rust.last_progress_ms < 0 || rust.last_progress_ms > agent.clock_ms || rust.known_properties == null ||
                rust.known_properties.Any(string.IsNullOrEmpty) || rust.known_properties.Distinct().Count() != rust.known_properties.Length ||
                (rust.mature && (!rust.analysis_completed || !rust.corrosion_understood || !rust.resistance_acquired)))) return false;
            var drops = s.worldDrops ?? new WorldDropState[0];
            var crystal = agent == null ? null : agent.crystal;
            if (crystal != null && (crystal.unknown_material_count < 0 || crystal.analysis_hint_level < 0 || crystal.analysis_hint_level > 3 ||
                crystal.last_progress_ms < 0 || crystal.last_progress_ms > agent.clock_ms || crystal.known_facts == null ||
                crystal.known_facts.Any(string.IsNullOrEmpty) || crystal.known_facts.Distinct().Count() != crystal.known_facts.Length ||
                (crystal.mature && (!crystal.analysis_completed || !crystal.carbonate_known || !crystal.analyzer_used)))) return false;
            var salt = agent == null ? null : agent.salt;
            var sand = agent == null ? null : agent.sand;
            if (sand != null && (sand.unknown_count < 0 || sand.last_progress_ms < 0 || sand.last_progress_ms > agent.clock_ms ||
                sand.analysis_hint_level < 0 || sand.analysis_hint_level > 3 || sand.failure_count < 0 || sand.failure_hint_level < 0 || sand.failure_hint_level > 3 ||
                (sand.mature && (!sand.analysis_completed || !sand.cross_region_link)) || (sand.cross_region_link && !sand.analysis_completed))) return false;
            if (s.carbonFuelBurned < 0) return false;
            var grotto = agent == null ? null : agent.grotto;
            if (grotto != null && (grotto.unknown_count < 0 || grotto.gas_count < 0 || grotto.failure_count < 0 ||
                grotto.analysis_hint_level < 0 || grotto.analysis_hint_level > 3 || grotto.gas_hint_level < 0 || grotto.gas_hint_level > 3 ||
                grotto.failure_hint_level < 0 || grotto.failure_hint_level > 3 || grotto.last_progress_ms < 0 || grotto.last_progress_ms > agent.clock_ms ||
                grotto.known_facts == null || grotto.known_facts.Any(string.IsNullOrEmpty) || grotto.known_facts.Distinct().Count() != grotto.known_facts.Length ||
                (grotto.mature && (!grotto.oxygen_observed || !grotto.analysis_completed || !grotto.organic_completed || !grotto.gas_observed || !grotto.storage_understood)))) return false;
            var geode = agent == null ? null : agent.geode;
            if (geode != null && (geode.unknown_count < 0 || geode.failure_count < 0 || geode.analysis_hint_level < 0 || geode.analysis_hint_level > 3 ||
                geode.failure_hint_level < 0 || geode.failure_hint_level > 3 || geode.last_progress_ms < 0 || geode.last_progress_ms > agent.clock_ms ||
                geode.known_facts == null || geode.known_facts.Any(string.IsNullOrEmpty) || geode.known_facts.Distinct().Count() != geode.known_facts.Length ||
                (geode.mature && (!geode.crystal_observed || !geode.analysis_completed || !geode.crystallization_observed || !geode.condition_compared)))) return false;
            var mantle = agent == null ? null : agent.mantle;
            // Mature is sticky in Godot, while later formal successes replace three flags.
            // Do not reject a valid mature save merely because a later NH3 event cleared them.
            var core = agent == null ? null : agent.core_shell;
            if (core != null && core.failure_count < 0) return false;
            var planetary = agent == null ? null : agent.planetary;
            if (planetary != null && (planetary.failure_count < 0 || (planetary.mature && (!planetary.analysis_confirmed || !planetary.condition_selected || !planetary.category_selected || !planetary.device_used)))) return false;
            if (mantle != null && (mantle.failure_count < 0 || (mantle.mature && (!mantle.heat_observed || !mantle.sulfur_observed || !mantle.thermal_processed || !mantle.industrial_chain_observed || !mantle.crystallization_observed)))) return false;
            var radiation = agent == null ? null : agent.radiation;
            if (radiation != null && (radiation.unknown_count < 0 || radiation.failure_count < 0 ||
                (radiation.mature && (!radiation.contamination_analyzed || !radiation.oxidation_observed || !radiation.gas_handled || !radiation.purification_verified)))) return false;
            var magnetic = agent == null ? null : agent.magnetic;
            if (magnetic != null && (magnetic.unknown_count < 0 || magnetic.failure_count < 0 || magnetic.analysis_hint_level < 0 || magnetic.analysis_hint_level > 3 ||
                magnetic.failure_hint_level < 0 || magnetic.failure_hint_level > 3 || magnetic.last_progress_ms < 0 || magnetic.last_progress_ms > agent.clock_ms || magnetic.known_facts == null ||
                magnetic.known_facts.Any(string.IsNullOrEmpty) || magnetic.known_facts.Distinct().Count() != magnetic.known_facts.Length ||
                (magnetic.mature && (!magnetic.electricity_observed || !magnetic.analysis_completed || !magnetic.electrolysis_observed || !magnetic.material_condition_linked)))) return false;
            var mine = agent == null ? null : agent.mine;
            if (mine != null && (mine.unknown_count < 0 || mine.failure_count < 0 || mine.analysis_hint_level < 0 || mine.analysis_hint_level > 3 ||
                mine.failure_hint_level < 0 || mine.failure_hint_level > 3 || mine.last_progress_ms < 0 || mine.last_progress_ms > agent.clock_ms ||
                mine.known_facts == null || mine.analyzed_ids == null || mine.displacement_ids == null ||
                new[] { mine.known_facts, mine.analyzed_ids, mine.displacement_ids }.Any(a => a.Any(string.IsNullOrEmpty) || a.Distinct().Count() != a.Length) ||
                (mine.mature && (!mine.analysis_completed || !mine.metallurgy_observed || !mine.material_difference)))) return false;
            var ember = agent == null ? null : agent.ember;
            if (ember != null && (ember.unknown_material_count < 0 || ember.analysis_hint_level < 0 || ember.analysis_hint_level > 3 ||
                ember.failure_count < 0 || ember.failure_hint_level < 0 || ember.failure_hint_level > 3 ||
                ember.known_facts == null || ember.known_facts.Any(string.IsNullOrEmpty) ||
                ember.known_facts.Distinct().Count() != ember.known_facts.Length || ember.last_progress_ms < 0 || ember.last_progress_ms > agent.clock_ms ||
                (ember.mature && (!ember.analysis_completed || !ember.temperature_knowledge || !ember.thermal_completed || !ember.condensation_used)))) return false;
            if (salt != null && (salt.unknown_material_count < 0 || salt.analysis_hint_level < 0 || salt.analysis_hint_level > 3 ||
                salt.last_progress_ms < 0 || salt.last_progress_ms > agent.clock_ms || salt.known_facts == null ||
                salt.known_facts.Any(string.IsNullOrEmpty) || salt.known_facts.Distinct().Count() != salt.known_facts.Length ||
                salt.separation_used != (salt.filtration_used || salt.distillation_used) ||
                (salt.mature && (!salt.analysis_completed || !salt.solution_property_known || !salt.separation_used)))) return false;
            if (sources.Any(r => r == null || string.IsNullOrEmpty(r.id) || string.IsNullOrEmpty(r.item_id) ||
                    r.capacity <= 0 || r.remaining < 0 || r.remaining > r.capacity) ||
                sources.Select(r => r.id).Distinct().Count() != sources.Length ||
                drops.Any(d => d == null || string.IsNullOrEmpty(d.instance_id) || string.IsNullOrEmpty(d.item_id) ||
                    string.IsNullOrEmpty(d.source_block_id) || string.IsNullOrEmpty(d.region_id) || d.amount <= 0 || !Finite(d.x) || !Finite(d.y)) ||
                drops.Select(d => d.instance_id).Distinct().Count() != drops.Length) return false;
            return deltas.All(d => d != null) && deltas.Select(d => d.x + ":" + d.y).Distinct().Count() == deltas.Length;
        }
        public void Delete()
        {
            lock (Gate)
            {
                foreach (var suffix in new[] { "", ".tmp", ".bak1", ".bak2" })
                    if (File.Exists(path + suffix)) File.Delete(path + suffix);
                // Exact owned snapshot files only; never recursively delete a directory.
                foreach (var file in HistoryFiles()) File.Delete(file);
                if (Directory.Exists(HistoryDirectory))
                    foreach (var file in Directory.GetFiles(HistoryDirectory, "*.json.tmp")
                        .Where(f => Regex.IsMatch(Path.GetFileName(f), "^[0-9]{19}_[0-9a-f]{32}\\.json\\.tmp$"))) File.Delete(file);
            }
        }
    }
}
