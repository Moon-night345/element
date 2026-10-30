using System;
using System.IO;
using System.Linq;
using UnityEngine;
using ElementalStarSea.Data;

namespace ElementalStarSea.EditorTools
{
    public static class SaveAcceptance
    {
        static string TestDirectory()
        {
            var directory = Path.GetFullPath(Path.Combine("Logs", "u13_save_test_" + Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(directory);
            return directory;
        }
        static void Write(SaveRepository repo, GameState state)
        {
            string reason; Require(repo.Write(state, out reason), "write: " + reason);
        }
        public static void LegacyAndAtomic()
        {
            string path = Path.Combine(TestDirectory(), "save.json");
            var state = new GameState { questStep = 4, terrainSeed = 179, hasPlayerPosition = true, playerWorldX = 75, playerWorldY = 1.2f };
            state.AddItem("resource_stone", 7);
            state.terrainChanges = new[] { new TerrainCellChange { x = 70, y = -1, block_id = "" } };
            File.WriteAllText(path, JsonUtility.ToJson(state));
            var repo = new SaveRepository(path, "legacy");
            Require(repo.Read().success && repo.Read().legacy, "legacy compatibility");
            Write(repo, state);
            Require(!repo.Read().legacy && repo.Read().state.CountItem("resource_stone") == 7 &&
                repo.Read().state.terrainChanges.Length == 1 && repo.Read().state.playerWorldX == 75, "new envelope lost snapshot fields");
            Require(repo.Inspect(path + ".bak1").legacy, "legacy original not retained as backup");
            string committed = File.ReadAllText(path);
            var interrupted = new SaveRepository(path, "legacy", () => { throw new IOException("Injected interruption before commit"); });
            state.AddItem("resource_stone"); string reason;
            Require(!interrupted.Write(state, out reason) && File.ReadAllText(path) == committed && repo.Read().state.CountItem("resource_stone") == 7,
                "interrupted write destroyed committed state");
            Require(File.Exists(path + ".tmp"), "test did not leave interrupted temp");
            Require(repo.Read().success && repo.Read().state.CountItem("resource_stone") == 7, "uncommitted temp was loaded");
            state.energy = float.NaN;
            Require(!repo.Write(state, out reason) && File.ReadAllText(path) == committed, "non-finite state committed");
        }
        public static void BackupRecovery()
        {
            string path = Path.Combine(TestDirectory(), "save.json");
            var repo = new SaveRepository(path, "a");
            var state = new GameState();
            for (int i = 1; i <= 3; i++) { state.questStep = i; Write(repo, state); }
            Require(repo.Read().state.questStep == 3 && repo.Inspect(path + ".bak1").state.questStep == 2 &&
                repo.Inspect(path + ".bak2").state.questStep == 1, "two-backup rotation");
            var envelope = JsonUtility.FromJson<SaveEnvelope>(File.ReadAllText(path));
            envelope.payload_json = envelope.payload_json.Replace("\"questStep\":3", "\"questStep\":9");
            File.WriteAllText(path, JsonUtility.ToJson(envelope));
            Require(!repo.Inspect(path).success && repo.Read().recovery == "bak1" && repo.Read().state.questStep == 2, "checksum / first backup");
            string damagedMain = File.ReadAllText(path);
            state.questStep = 4; Write(repo, state);
            Require(repo.Read().state.questStep == 4 && repo.Inspect(path + ".bak1").state.questStep == 2 &&
                Directory.GetFiles(Path.GetDirectoryName(path), "*.corrupt_*").Any(p => File.ReadAllText(p) == damagedMain),
                "recovery write rotated corruption over healthy backup");
            File.WriteAllText(path, "{"); File.WriteAllText(path + ".bak1", "{}");
            Require(repo.Read().recovery == "bak2" && repo.Read().state.questStep == 1, "second backup fallback");
            File.WriteAllText(path + ".bak2", "{}");
            Require(!repo.Read().success && repo.Read().exists && File.ReadAllText(path) == "{", "all-bad saves silently replaced");
            envelope.schema_version = 999;
            File.WriteAllText(path, JsonUtility.ToJson(envelope));
            string future = File.ReadAllText(path); string reason;
            Require(repo.Read().reason == "unsupported_schema" && !repo.Write(new GameState(), out reason) &&
                File.ReadAllText(path) == future, "future save overwritten");
        }
        public static void SlotIsolation()
        {
            var root = TestDirectory();
            var first = new SaveRepository(Path.Combine(root, "a", "save.json"), "a");
            var second = new SaveRepository(Path.Combine(root, "b", "save.json"), "b");
            var a = new GameState(); a.AddItem("resource_stone", 5); Write(first, a);
            var b = new GameState { laboratoryStage = 2 }; b.AddItem("element_cu", 11); Write(second, b);
            Require(first.Read().state.CountItem("resource_stone") == 5 && first.Read().state.laboratoryStage == 0 &&
                second.Read().state.CountItem("element_cu") == 11 && second.Read().state.CountItem("resource_stone") == 0,
                "slot cross-contamination");
            var wrong = new SaveRepository(Path.Combine(root, "a", "save.json"), "b");
            Require(!wrong.Read().success, "save identity mismatch ignored");
            string previous = GameState.CurrentSaveSlot;
            try
            {
                GameState.BeginNewWorld(); string created = GameState.CurrentSaveSlot;
                GameState.BeginNewWorld();
                Require(created != GameState.CurrentSaveSlot && first.Read().success, "new world replaced old slot");
                bool refused = false;
                try { GameState.SelectSaveSlot("../outside"); } catch (ArgumentException) { refused = true; }
                Require(refused, "unsafe slot ID accepted");
            }
            finally { GameState.SelectSaveSlot(previous); }
        }
        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
