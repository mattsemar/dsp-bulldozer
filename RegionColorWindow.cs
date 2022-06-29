using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Bulldozer
{
    public class RegionColorWindow
    {
        public static bool visible;
        private static Rect _windowRect = new(Screen.width / 3, 100f, 500f, 820f);

        private static int _leftColumnWidth;
        public static bool NeedReinit;

        private static Texture2D _tooltipBg;

        private static int _loggedMessageCount = 0;
        private static RegionalColors _regionalColors = RegionalColors.Instance;
        private static Pager<RegionColorConfig> _pager;
        private static bool lastOverLapCheckFailed;
        private static bool _overlapCheckDirty;
        private static bool _windowStateDirty;
        private static DateTime _lastOverlapCheck = DateTime.Now;

        public static void OnOpen()
        {
            visible = true;
        }

        public static void OnClose()
        {
            visible = false;
            _windowStateDirty = true;
        }

        public static void Toggle()
        {
            if (visible)
                OnClose();
            else
                OnOpen();
        }

        public static void OnGUI()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                visible = false;
                _windowStateDirty = true;
                return;
            }

            Init();

            _windowRect = GUILayout.Window(1297895055, _windowRect, DrawRegionColorSection, "Region Colors");
        }

        private static void Init()
        {
            if (_tooltipBg == null && !NeedReinit)
            {
                return;
            }

            var background = new Texture2D(1, 1, TextureFormat.RGB24, false);
            background.SetPixel(0, 0, Color.white);
            background.Apply();
            _tooltipBg = background;

            InitWindowRect();
            NeedReinit = false;
        }


        public static void DrawRegionColorSection(int id)
        {
            if (_pager == null)
            {
                _pager = new Pager<RegionColorConfig>(RegionalColors.Instance.GetRegions(), 3);
            }
            else if (_windowStateDirty)
            {
                _windowStateDirty = false;
                _pager = new Pager<RegionColorConfig>(RegionalColors.Instance.GetRegions(), 3);
            }


            GUILayout.BeginArea(new Rect(_windowRect.width - 25f, 0f, 25f, 30f));
            if (GUILayout.Button("X"))
            {
                OnClose();
                return;
            }

            GUILayout.EndArea();
            GUILayout.BeginVertical(GUI.skin.box);
            {
                DrawCenteredLabel( new GUIContent("Regions", "Set min equal to max to include all longitude values"));
                DrawPreviousButton();
                foreach (var regionalColor in _pager.GetPage())
                {
                    DrawColorConfig(regionalColor);
                    GUILayout.Space(25);
                }
                DrawNextButton();
            }
            DrawAddButton();
            GUILayout.EndVertical();
            CheckOverlap();
            if (lastOverLapCheckFailed)
            {
                GUI.tooltip += "\r\nSome regions are overlapping. The first region in list will determine color for overlapping points";
            }

            if (GUI.tooltip != null)
            {
                GUI.skin = null;
                var style = new GUIStyle
                {
                    normal = new GUIStyleState { textColor = Color.white },
                    wordWrap = true,
                    alignment = TextAnchor.MiddleCenter,
                    stretchHeight = true,
                    stretchWidth = true
                };

                var height = style.CalcHeight(new GUIContent(GUI.tooltip), _windowRect.width) + 10;
                var y = (int)(_windowRect.height - height * 1.25);
                GUI.Box(new Rect(0, y, _windowRect.width, height * 1.25f), GUI.tooltip, style);
            }

            GUI.DragWindow();
        }

        private static void DrawPreviousButton()
        {
            GUILayout.BeginHorizontal();

            var buttonPressed = GUILayout.Button(new GUIContent("Previous", "Load previous page"));
            if (buttonPressed)
            {
                if (!_pager.IsFirst())
                {
                    _pager.Previous();
                }
            }


            GUILayout.EndHorizontal();
        }

        private static void DrawNextButton()
        {
            GUILayout.BeginHorizontal();

            var buttonPressed = GUILayout.Button(new GUIContent("Next", "Load next page"));
            if (buttonPressed && _pager.HasNext())
            {
                _pager.Next();
            }


            GUILayout.EndHorizontal();
        }

        private static void CheckOverlap()
        {
            if (_overlapCheckDirty && (DateTime.Now - _lastOverlapCheck).TotalSeconds > 2
                || (DateTime.Now - _lastOverlapCheck).TotalSeconds > 15)
            {
                _overlapCheckDirty = false;
                lastOverLapCheckFailed = false;
                for (int i = -90; i < 90; i++)
                {
                    for (int j = -180; j < 180; j++)
                    {
                        int matchCount = 0;
                        List<int> regionMatchingIndex = new List<int>();

                        var regions = _regionalColors.GetRegions();
                        for (int ndx = 0; ndx < regions.Count; ndx++)
                        {
                            var region = regions[ndx];
                            if (region.ContainsPosition(i, j))
                            {
                                matchCount++;
                                regionMatchingIndex.Add(ndx);
                            }
                        }

                        if (matchCount > 1)
                        {
                            lastOverLapCheckFailed = true;
                            // var matchingIndexes = string.Join(",", regionMatchingIndex);
                            // Log.Debug($"overlap on points {i}, {j}, {matchingIndexes}");
                        }
                    }
                }

                _lastOverlapCheck = DateTime.Now;
            }
        }

        private static void DrawAddButton()
        {
            var pressed = GUILayout.Button("Add New Region");
            if (pressed)
            {
                _regionalColors.Create();
                _regionalColors.Save();
                _windowStateDirty = true;
            }
        }

        private static void DrawColorConfig(RegionColorConfig regionalColor)
        {
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            DrawSetting("Min Latitude", -90, 90, ref regionalColor.minLatitude);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            DrawSetting("Max Latitude", -90, 90, ref regionalColor.maxLatitude, regionalColor.minLatitude);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            DrawSetting("Min Longitude", -180, 180, ref regionalColor.minLongitude);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            DrawSetting("Max Longitude", -180, 180, ref regionalColor.maxLongitude, regionalColor.minLongitude);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label(new GUIContent("Current Color"), GUILayout.Width(_leftColumnWidth), GUILayout.MaxWidth(_leftColumnWidth));
            GUILayout.FlexibleSpace();

            DrawCustomColor(regionalColor.colorIndex, ref regionalColor.colorIndex);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            DrawMirrorToggle(regionalColor);
            DrawDeleteButton(regionalColor);
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
        }

        private static void DrawMirrorToggle(RegionColorConfig regionalColor)
        {
            var minCoord = Math.Min(regionalColor.minLatitude, regionalColor.maxLatitude);
            var maxCoord = Math.Max(regionalColor.minLatitude, regionalColor.maxLatitude);
            var tooltip = $"Enable latitude mirroring. So the above would also include from [{-maxCoord} - {-minCoord}]";
            if (minCoord > 0)
            {
                minCoord = Math.Min(-regionalColor.minLatitude, -regionalColor.maxLatitude);
                maxCoord = Math.Max(-regionalColor.minLatitude, -regionalColor.maxLatitude);
                tooltip = $"Enable latitude mirroring. So the above would also include from [{maxCoord} - {minCoord}]";
            }

            var result = GUILayout.Toggle(regionalColor.mirror, new GUIContent("Mirror", tooltip));
            if (result != regionalColor.mirror)
            {
                regionalColor.mirror = result;
                _regionalColors.Save();
                _overlapCheckDirty = true;
            }
        }

        private static void DrawDeleteButton(RegionColorConfig regionalColor)
        {
            var pressed = GUILayout.Button("Delete Color");
            if (pressed)
            {
                _regionalColors.Remove(regionalColor);
                _regionalColors.Save();
                _windowStateDirty = true;
            }
        }

        private static void DrawCustomColor(int regionalColorColorIndex, ref int currentVal)
        {
            GUILayout.BeginHorizontal();

            var converted = (float)Convert.ToDouble(regionalColorColorIndex, CultureInfo.InvariantCulture);
            var leftValue = (float)Convert.ToDouble(0, CultureInfo.InvariantCulture);
            var rightValue = (float)Convert.ToDouble(31, CultureInfo.InvariantCulture);

            var result = GUILayout.HorizontalSlider(converted, leftValue, rightValue, GUILayout.MinWidth(200));
            int newResult = regionalColorColorIndex;
            if (Math.Abs(result - converted) > Mathf.Abs(rightValue - leftValue) / 1000)
            {
                newResult = (int)result;
            }

            var strVal = newResult.ToString();
            var strResult = GUILayout.TextField(strVal, GUILayout.Width(50));

            var customColor = PluginConfigWindow.GetColorByIndex(newResult);
            var customColorTexture = PluginConfigWindow.GetTextureForColor(customColor);
            GUILayout.Label(customColorTexture);


            GUILayout.EndHorizontal();
            if (strResult != strVal)
            {
                try
                {
                    var resultVal = (float)Convert.ToDouble(strResult, CultureInfo.InvariantCulture);
                    var clampedResultVal = Mathf.Clamp(resultVal, leftValue, rightValue);
                    newResult = (int)clampedResultVal;
                }
                catch (FormatException)
                {
                    // Ignore user typing in bad data
                }
            }

            if (newResult != currentVal)
            {
                currentVal = newResult;
                _regionalColors.Save();
            }
        }

        private static void DrawSetting(string title, float minVal, float maxVal, ref float current, float actualMin = -1)
        {
            GUILayout.Label(title);

            var result = GUILayout.HorizontalSlider(current, minVal, maxVal, GUILayout.MinWidth(200));
            float newVal = current;
            if (Math.Abs(result - current) > Mathf.Abs(maxVal - minVal) / 1000)
            {
                newVal = Mathf.Round(result / 0.36f) * 0.36f;
            }

            var strResult = GUILayout.TextField(newVal.ToString("F2", CultureInfo.CurrentCulture), GUILayout.Width(50));
            try
            {
                var updatedFromTextBox = float.Parse(strResult, NumberStyles.Any);
                if (updatedFromTextBox >= minVal && updatedFromTextBox <= maxVal)
                {
                    newVal = updatedFromTextBox;
                }
            }
            catch (Exception e)
            {
                Log.Debug($"invalid text entered {e.Message}");
            }

            if (actualMin > newVal && actualMin != -1)
                newVal = actualMin;
            if (newVal != current)
            {
                current = newVal;
                RegionalColors.Instance.Save();
                _overlapCheckDirty = true;
            }
        }


        private static void InitWindowRect()
        {
            var width = Mathf.Min(Screen.width, 650);
            var height = Screen.height < 560 ? Screen.height : 560;
            var offsetX = Mathf.RoundToInt((Screen.width - width) / 2f);
            var offsetY = Mathf.RoundToInt((Screen.height - height) / 2f);
            _windowRect = new Rect(offsetX, offsetY, width, height);

            _leftColumnWidth = Mathf.RoundToInt(_windowRect.width / 2.5f);
        }

        public static void DrawCenteredLabel(GUIContent text, params GUILayoutOption[] options)
        {
            GUILayout.BeginHorizontal(options);
            GUILayout.FlexibleSpace();
            GUILayout.Label(text);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }
    }
}