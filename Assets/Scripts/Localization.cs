// 元素星海·裂核 — 本地化查表
using System.Collections.Generic;
using UnityEngine;

namespace ElementalStarSea.Data
{
    public static class Localization
    {
        static Dictionary<string, string> _table = new Dictionary<string, string>();

        public static bool Loaded { get; private set; }

        public static void Load(LocalizationDatabase db)
        {
            _table.Clear();
            if (db != null && db.entries != null)
                foreach (var e in db.entries)
                    _table[e.key] = e.text;
            Loaded = true;
        }

        /// <summary>取本地化文本;缺失时回退为 key 本身(便于发现问题)。</summary>
        public static string Tr(string key)
        {
            if (string.IsNullOrEmpty(key)) return "";
            string text;
            return _table.TryGetValue(key, out text) ? text : key;
        }

        public static string TrFormat(string key, params object[] args)
        {
            return string.Format(Tr(key), args);
        }
    }
}
