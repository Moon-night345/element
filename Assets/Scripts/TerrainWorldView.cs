using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using ElementalStarSea.Data;

namespace ElementalStarSea.Game
{
    public sealed class TerrainWorldView : MonoBehaviour
    {
        TerrainRuntimeService model;
        Tilemap tilemap;
        readonly Dictionary<string, Tile> tiles = new Dictionary<string, Tile>();
        public void Bind(TerrainRuntimeService service)
        {
            model = service;
            gameObject.AddComponent<Grid>();
            var child = new GameObject("SolidTiles");
            child.transform.SetParent(transform, false);
            tilemap = child.AddComponent<Tilemap>();
            child.AddComponent<TilemapRenderer>().sortingOrder = -10;
            // Physics geometry is also available to future Rigidbody2D actors.
            child.AddComponent<TilemapCollider2D>();
            var positions = new List<Vector3Int>();
            var values = new List<TileBase>();
            foreach (var cell in model.Cells)
            {
                positions.Add(new Vector3Int(cell.Key.x, cell.Key.y, 0));
                values.Add(TileFor(cell.Key));
            }
            tilemap.SetTiles(positions.ToArray(), values.ToArray());
            model.CellChanged += Refresh;
        }
        Tile TileFor(Vector2Int p)
        {
            var definition = model.Definition(model.BlockAt(p));
            if (definition == null) return null;
            string region = string.IsNullOrEmpty(definition.region_id) ? model.RegionAt(p) : definition.region_id;
            string key = region + ":" + definition.id;
            Tile tile;
            if (tiles.TryGetValue(key, out tile)) return tile;
            tile = ScriptableObject.CreateInstance<Tile>();
            var sprite = ArtLib.Single("tile_" + region + "_" + (definition.icon_x + 1).ToString("00") +
                "_" + (Mathf.Min(3, definition.icon_y) + 1).ToString("00"), 128f);
            tile.sprite = sprite != null ? sprite : SpriteUtil.Square();
            tile.transform = Matrix4x4.Scale(new Vector3(1f / tile.sprite.bounds.size.x, 1f / tile.sprite.bounds.size.y, 1f));
            tile.colliderType = Tile.ColliderType.Grid;
            if (sprite == null) tile.color = new Color(0.35f, 0.32f, 0.28f);
            tiles[key] = tile;
            return tile;
        }
        void Refresh(Vector2Int p) { tilemap.SetTile(new Vector3Int(p.x, p.y, 0), TileFor(p)); }
        void OnDestroy()
        {
            if (model != null) model.CellChanged -= Refresh;
            foreach (var tile in tiles.Values) if (tile != null) Destroy(tile);
        }
    }
}
