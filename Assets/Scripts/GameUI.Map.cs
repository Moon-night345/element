using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
namespace ElementalStarSea.Game
{
    public partial class GameUI
    {
        GameObject _mapPanel;
        Transform _mapRegions;
        Text _mapDetails;
        public Action OnMap;
        public void ShowRegionMap(Data.RegionDef[] regions, Data.GameState state, Func<string, string> status)
        {
            HideAllPanels();
            if (_mapPanel == null)
            {
                _mapPanel = MakePanel("MapPanel", new Vector2(1040, 620));
                MakePanelText(_mapPanel, "Title", new Vector2(0, 264), 960, 44, TextAnchor.MiddleCenter, 26, Accent).text = "区域地图";
                MakePanelText(_mapPanel, "ListTitle", new Vector2(-260, 215), 440, 28, TextAnchor.MiddleLeft, 16, Accent).text = "探索区域 · 滚轮浏览";
                _mapRegions = MakeScrollGrid(_mapPanel, "MapRegions", new Vector2(-260, -16), 440, 420);
                var grid = _mapRegions.GetComponent<GridLayoutGroup>(); grid.constraintCount = 1; grid.cellSize = new Vector2(424, 46);
                _mapDetails = MakePanelText(_mapPanel, "Details", new Vector2(250, 44), 440, 360, TextAnchor.UpperLeft, 19);
                MakePanelText(_mapPanel, "TravelHint", new Vector2(250, -183), 440, 76, TextAnchor.UpperLeft, 16, new Color(.8f, .78f, .7f)).text = "通过场景中的气闸、电梯前往区域。\n地图显示探索与进入条件，不直接传送。";
                MakePanelButton(_mapPanel, "CloseMap", new Vector2(0, -267), 220, 46, "返回游戏 (M)").onClick.AddListener(CloseAllPanels);
            }
            _mapPanel.SetActive(true); ClearChildren(_mapRegions);
            Action<Data.RegionDef> select = region => {
                bool here = state.currentRegion == region.id, visited = state.HasVisitedRegion(region.id);
                _mapDetails.text = Data.Localization.Tr(region.name_key) + "\n\n" + (here ? "● 你在这里" : visited ? "已探索" : "尚未探索") +
                    "\n\n" + (region.depth > 0 ? "地下第 " + region.depth + " 层" : "地表区域") + "\n\n进入条件\n" + status(region.id);
            };
            foreach (var region in regions)
            {
                var captured = region; bool here = state.currentRegion == region.id;
                var label = (here ? "● " : state.HasVisitedRegion(region.id) ? "✓ " : "○ ") + Data.Localization.Tr(region.name_key);
                var button = MakeGridButton(_mapRegions, label, () => select(captured), here ? new Color(.28f, .21f, .1f) : (Color?)null);
                button.name = "MapNode_" + region.id;
            }
            select(regions.First(r => r.id == state.currentRegion));
        }
    }
}
