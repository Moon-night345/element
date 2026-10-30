// 元素星海·裂核 — 程序化美术:纹理生成/天空/山峦/地形瓦片/装饰物
// 全部运行时生成,零外部资源。像素风:Point 采样;光晕:Bilinear+加色混合。
using System.Collections.Generic;
using UnityEngine;

namespace ElementalStarSea.Game
{
    // ───────────────────────── 程序化纹理 ─────────────────────────
    public static class TexGen
    {
        static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        static Texture2D NewTex(string id, int w, int h, FilterMode filter)
        {
            Texture2D t;
            if (Cache.TryGetValue(id, out t) && t != null) return t;
            t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.filterMode = filter;
            t.wrapMode = filter == FilterMode.Point ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            Cache[id] = t;
            return t;
        }

        /// <summary>垂直渐变(天空)。</summary>
        public static Texture2D Gradient(string id, Color top, Color bottom)
        {
            var t = NewTex("g_" + id, 8, 256, FilterMode.Bilinear);
            for (int y = 0; y < 256; y++)
            {
                var c = Color.Lerp(bottom, top, y / 255f);
                for (int x = 0; x < 8; x++) t.SetPixel(x, y, c);
            }
            t.Apply();
            return t;
        }

        /// <summary>噪点碎石纹理(地形)。</summary>
        public static Texture2D Speckle(string id, Color baseC, Color dark, Color light,
                                       int seed, int size = 64)
        {
            var t = NewTex("s_" + id, size, size, FilterMode.Point);
            var rng = new System.Random(seed);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float r = (float)rng.NextDouble();
                    var c = baseC;
                    if (r < 0.16f) c = dark;
                    else if (r > 0.90f) c = light;
                    else if (r > 0.84f) c = Color.Lerp(baseC, dark, 0.5f);
                    t.SetPixel(x, y, c);
                }
            t.Apply();
            return t;
        }

        /// <summary>柔和圆点(光晕/粒子)。</summary>
        public static Texture2D SoftCircle(string id, Color c, int size = 64)
        {
            var t = NewTex("c_" + id, size, size, FilterMode.Bilinear);
            var center = new Vector2(size * 0.5f, size * 0.5f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center) / (size * 0.5f);
                    float a = Mathf.Clamp01(1f - d);
                    c.a = a * a;
                    t.SetPixel(x, y, c);
                }
            t.Apply();
            return t;
        }

        /// <summary>实心圆(太阳/头盔)。</summary>
        public static Texture2D HardCircle(string id, Color c, int size = 64)
        {
            var t = NewTex("h_" + id, size, size, FilterMode.Bilinear);
            var center = new Vector2(size * 0.5f, size * 0.5f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center) / (size * 0.5f);
                    c.a = d < 0.94f ? 1f : Mathf.Clamp01((1f - d) * 12f);
                    t.SetPixel(x, y, c);
                }
            t.Apply();
            return t;
        }

        /// <summary>山峦剪影条带(带透明天空部分)。</summary>
        public static Texture2D Mountains(string id, Color c, int seed, int w = 512, int h = 160)
        {
            var t = NewTex("m_" + id, w, h, FilterMode.Bilinear);
            var rng = new System.Random(seed);
            float p1 = (float)rng.NextDouble() * 10f, p2 = (float)rng.NextDouble() * 10f;
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)w * Mathf.PI * 2f;
                float ridge = h * (0.42f
                    + 0.20f * Mathf.Sin(u * 2f + p1)
                    + 0.12f * Mathf.Sin(u * 5f + p2)
                    + 0.06f * Mathf.Sin(u * 11f));
                for (int y = 0; y < h; y++)
                    t.SetPixel(x, y, y < ridge ? c : Color.clear);
            }
            t.Apply();
            return t;
        }

        /// <summary>屏幕暗角(中心透明四角暗)。</summary>
        public static Texture2D Vignette()
        {
            var t = NewTex("vig", 256, 256, FilterMode.Bilinear);
            var center = new Vector2(128f, 128f);
            for (int y = 0; y < 256; y++)
                for (int x = 0; x < 256; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center) / 181f; // 0..~1
                    float a = Mathf.Clamp01((d - 0.55f) / 0.45f) * 0.42f;
                    t.SetPixel(x, y, new Color(0f, 0f, 0.02f, a));
                }
            t.Apply();
            return t;
        }

        public static Sprite SpriteOf(Texture2D t, float ppu = 64f)
        {
            return Sprite.Create(t, new Rect(0, 0, t.width, t.height),
                                 new Vector2(0.5f, 0.5f), ppu);
        }
    }

    // ───────────────────────── 区域调色板 ─────────────────────────
    public class RegionPalette
    {
        public Color skyTop, skyBottom, ground, groundDark, groundLight, accent, mountainFar, mountainNear;
        public string particle;   // dust/snow/ember/spore/sand/spark/mote/none
        public bool underground;
        public int topTile, fillTile;   // Kenney tilemap 索引

        public static RegionPalette Surface(Color skyT, Color skyB, Color g, Color acc, string particle,
                                            int topTile, int fillTile)
        {
            return new RegionPalette
            {
                skyTop = skyT, skyBottom = skyB,
                ground = g,
                groundDark = g * 0.66f,
                groundLight = Color.Lerp(g, Color.white, 0.22f),
                accent = acc,
                mountainFar = Color.Lerp(skyB, Color.black, 0.35f),
                mountainNear = Color.Lerp(skyB, Color.black, 0.55f),
                particle = particle, underground = false,
                topTile = topTile, fillTile = fillTile,
            };
        }

        public static RegionPalette Underground(int depth0, int total)
        {
            float t = total <= 1 ? 0f : depth0 / (float)(total - 1);
            var skyT = Color.Lerp(new Color(0.075f, 0.07f, 0.12f), new Color(0.16f, 0.05f, 0.05f), t);
            var skyB = Color.Lerp(new Color(0.11f, 0.10f, 0.17f), new Color(0.28f, 0.08f, 0.06f), t);
            var g = Color.Lerp(new Color(0.20f, 0.19f, 0.27f), new Color(0.30f, 0.13f, 0.10f), t);
            var acc = Color.Lerp(new Color(0.35f, 0.85f, 0.95f), new Color(1f, 0.45f, 0.30f), t);
            string particle = t > 0.85f ? "ember" : (depth0 >= 5 ? "mote" : "spore");
            return new RegionPalette
            {
                skyTop = skyT, skyBottom = skyB,
                ground = g, groundDark = g * 0.6f, groundLight = Color.Lerp(g, Color.white, 0.16f),
                accent = acc,
                mountainFar = skyB * 0.8f, mountainNear = skyB * 0.6f,
                particle = particle, underground = true,
                topTile = 120, fillTile = 121,
            };
        }

        /// <summary>瓦片染色:向区域主色调靠拢,保留素材纹理。</summary>
        public Color TileTint()
        {
            return Color.Lerp(Color.white, ground, 0.45f);
        }
    }

    // ───────────────────────── 场景美术构建 ─────────────────────────
    public static class WorldArt
    {
        // 程序天空整体压在背景画(sorting -70)之后,仅作背景画缺失时的兜底
        const int SkySort = -85, StarSort = -84, SunSort = -83,
                  MFarSort = -82, MNearSort = -81, GroundSort = -10, TopSort = -9, PropSort = -8;

        /// <summary>单个区域的天空+山峦。</summary>
        public static void BuildSky(Vector2 center, float width, RegionPalette pal, int seed)
        {
            // 天空渐变(精灵自然尺寸 1x32 单位,按目标尺寸换算缩放)
            var sky = new GameObject("sky");
            sky.transform.position = new Vector3(center.x, center.y + 6f, 0f);
            var sr = sky.AddComponent<SpriteRenderer>();
            sr.sprite = TexGen.SpriteOf(TexGen.Gradient(seed + "sky", pal.skyTop, pal.skyBottom), 8f);
            sr.sortingOrder = SkySort;
            const float skyH = 44f;
            sky.transform.localScale = new Vector3(width, skyH / 32f, 1f);

            if (pal.underground) return;

            // 星星(上半区)
            var starTex = TexGen.SoftCircle(seed + "star", Color.white, 16);
            var rng = new System.Random(seed * 977);
            for (int i = 0; i < 40; i++)
            {
                var s = new GameObject("star" + i);
                float x = center.x + ((float)rng.NextDouble() - 0.5f) * width;
                float y = center.y + 4f + (float)rng.NextDouble() * 18f;
                s.transform.position = new Vector3(x, y, 0f);
                var ssr = s.AddComponent<SpriteRenderer>();
                ssr.sprite = TexGen.SpriteOf(starTex, 16f);
                float sc = 0.06f + (float)rng.NextDouble() * 0.10f;
                s.transform.localScale = new Vector3(sc, sc, 1f);
                ssr.color = new Color(1f, 1f, 1f, 0.35f + (float)rng.NextDouble() * 0.5f);
                ssr.sortingOrder = StarSort;
            }

            // 太阳/辉光
            var sun = new GameObject("sun");
            sun.transform.position = new Vector3(center.x + width * 0.22f, center.y + 15f, 0f);
            var sunSr = sun.AddComponent<SpriteRenderer>();
            sunSr.sprite = TexGen.SpriteOf(TexGen.SoftCircle(seed + "sun",
                Color.Lerp(pal.accent, Color.white, 0.55f)), 64f);
            sun.transform.localScale = new Vector3(10f, 10f, 1f);
            sunSr.sortingOrder = SunSort;

            // 两层山峦(精灵自然尺寸 8x2.5 单位)
            MakeRidge("mfar", center, width, 26f, -1.5f, pal.mountainFar, seed * 31 + 7, MFarSort);
            MakeRidge("mnear", center, width, 19f, -3.5f, pal.mountainNear, seed * 53 + 11, MNearSort);
        }

        static void MakeRidge(string name, Vector2 center, float width, float height,
                              float baseY, Color c, int seed, int sort)
        {
            var go = new GameObject(name);
            go.transform.position = new Vector3(center.x, center.y + baseY + height * 0.5f, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = TexGen.SpriteOf(TexGen.Mountains(seed + name, c, seed), 64f);
            sr.sortingOrder = sort;
            go.transform.localScale = new Vector3(width / 8f, height / 2.5f, 1f);
        }

        /// <summary>地形:Godot 区域专属 128px 瓦片(每区域 4x4 变体,确定性随机铺放)。</summary>
        public static void BuildGround(Vector2 leftTop, float width, float thickness, string regionId, int seed)
        {
            float ws = ArtLib.MapTileWorld;
            int cols = Mathf.CeilToInt(width / ws);
            int rows = Mathf.CeilToInt(thickness / ws);
            var rng = new System.Random(seed * 9173 + 13);
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    int v1 = 1 + (int)(rng.NextDouble() * 4);
                    int v2 = 1 + (int)(rng.NextDouble() * 4);
                    var sp = ArtLib.Single("tile_" + regionId + "_" + v1.ToString("00") + "_" + v2.ToString("00"), 64f);
                    if (sp == null) continue;
                    var go = new GameObject("gt");
                    go.transform.position = new Vector3(leftTop.x + ws * (c + 0.5f),
                                                        leftTop.y - ws * (r + 0.5f), 0f);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = sp;
                    sr.sortingOrder = GroundSort;
                }
            }
        }

        /// <summary>立绘装置:按目标高度摆放一张 PNG(锚点为底部中心)。</summary>
        public static GameObject ArtProp(string artName, Vector2 bottomCenter, float worldHeight,
                                         int sort = 3, Color? tint = null)
        {
            var sp = ArtLib.Single(artName, 64f);
            if (sp == null) return null;
            var go = new GameObject("art_" + artName);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sp;
            float h = sp.bounds.size.y;
            if (h > 0.001f) go.transform.localScale = Vector3.one * (worldHeight / h);
            go.transform.position = new Vector3(bottomCenter.x,
                                                bottomCenter.y + worldHeight * 0.5f - 0.15f, 0f);
            sr.sortingOrder = sort;
            if (tint.HasValue) sr.color = tint.Value;
            return go;
        }

        static readonly int[] SurfaceProps = { 144, 125, 146, 147, 148, 68, 65, 68, 84 };
        static readonly int[] UndergroundProps = { 68, 148, 67, 68 };

        /// <summary>装饰物:Kenney 植物/树/蘑菇/石堆/指示牌,按种子确定性散布。</summary>
        public static void BuildProps(Vector2 center, float width, RegionPalette pal, int seed)
        {
            var rng = new System.Random(seed * 131);
            var pool = pal.underground ? UndergroundProps : SurfaceProps;
            int count = pal.underground ? 8 : 7;
            for (int i = 0; i < count; i++)
            {
                int idx = pool[(int)(rng.NextDouble() * pool.Length)];
                float x = center.x + ((float)rng.NextDouble() - 0.5f) * width * 0.86f;
                var sp = ArtLib.Tile("tilemap", idx, ArtLib.TilemapCols);
                if (sp == null) continue;
                bool big = idx == 146 || idx == 65;             // 松树/门牌放大
                float s = big ? 2.2f : 1.3f;
                var go = new GameObject("prop" + idx);
                go.transform.position = new Vector3(x, center.y + ArtLib.TileWorld * s * 0.5f - 0.12f, 0f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sp;
                go.transform.localScale = new Vector3(s, s, 1f);
                sr.color = pal.underground
                    ? Color.Lerp(Color.white, pal.groundLight, 0.35f)
                    : Color.white;
                sr.sortingOrder = PropSort;
            }
        }
    }
}
