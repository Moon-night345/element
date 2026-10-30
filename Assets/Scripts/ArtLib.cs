// 元素星海·裂核 — 素材运行时加载(Godot 仓库移植资产 + Kenney CC0 瓦片)
// PNG/WAV 放在 Assets/StreamingAssets/{Art,Audio}/,运行时载入并缓存。
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace ElementalStarSea.Game
{
    public static class ArtLib
    {
        public const int TilePx = 18;
        public const float TileWorld = 1.5f;      // Kenney 瓦片世界尺寸
        public const int TilemapCols = 20;
        public const int CharactersCols = 12;
        public const float MapTileWorld = 2f;     // Godot 区域地形瓦(128px)世界尺寸
        public const float ChemPpu = 64f;         // 角色 128px → 2 单位高

        static readonly Dictionary<string, Texture2D> Sheets = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>();

        static Texture2D LoadTex(string artPath)
        {
            var path = Path.Combine(Application.streamingAssetsPath, "Art/" + artPath);
            if (!File.Exists(path)) return null;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(path));
            tex.filterMode = FilterMode.Point;
            return tex;
        }

        public static Texture2D Sheet(string name)
        {
            Texture2D tex;
            if (Sheets.TryGetValue(name, out tex) && tex != null) return tex;
            tex = LoadTex(name + ".png");
            if (tex != null) tex.wrapMode = TextureWrapMode.Clamp;
            Sheets[name] = tex;
            return tex;
        }

        /// <summary>整张 PNG 作为精灵(背景画/角色帧/装置立绘),ppu 决定世界尺寸。</summary>
        public static Sprite Single(string artName, float ppu)
        {
            var key = artName + "@" + ppu.ToString("0.##");
            Sprite sp;
            if (Sprites.TryGetValue(key, out sp) && sp != null) return sp;
            var tex = LoadTex(artName + ".png");
            if (tex == null) return null;
            sp = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                               new Vector2(0.5f, 0.5f), ppu);
            sp.name = key;
            Sprites[key] = sp;
            return sp;
        }

        /// <summary>前缀枚举帧:chem_walk_00.png.. → 精灵数组(按序)。</summary>
        public static Sprite[] Frames(string prefix, float ppu)
        {
            var dir = Path.Combine(Application.streamingAssetsPath, "Art");
            if (!Directory.Exists(dir)) return new Sprite[0];
            var files = Directory.GetFiles(dir, prefix + "_*.png")
                .Select(Path.GetFileNameWithoutExtension)
                .OrderBy(n => n)
                .ToArray();
            var list = new List<Sprite>();
            foreach (var n in files)
            {
                var sp = Single(n, ppu);
                if (sp != null) list.Add(sp);
            }
            return list.ToArray();
        }

        /// <summary>Kenney 瓦片精灵(带边缘外扩防接缝)。</summary>
        public static Sprite Tile(string sheet, int index, int cols)
        {
            var key = sheet + "_" + index;
            Sprite sp;
            if (Sprites.TryGetValue(key, out sp) && sp != null) return sp;
            var tex = Sheet(sheet);
            if (tex == null) return null;
            int col = index % cols;
            int row = index / cols;
            sp = Sprite.Create(tex, new Rect(col * TilePx, tex.height - (row + 1) * TilePx,
                                             TilePx, TilePx),
                               new Vector2(0.5f, 0.5f), TilePx / TileWorld,
                               4u);
            sp.name = key;
            Sprites[key] = sp;
            return sp;
        }

        public static Sprite CharFrame(int index)
        {
            return Tile("characters", index, CharactersCols);
        }

        public static AudioClip Clip(string name)
        {
            AudioClip clip;
            if (Clips.TryGetValue(name, out clip) && clip != null) return clip;
            var path = Path.Combine(Application.streamingAssetsPath, "Audio/" + name + ".wav");
            if (!File.Exists(path)) return null;
            clip = WavLoader.Load(path);
            Clips[name] = clip;
            return clip;
        }
    }
}
