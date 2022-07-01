using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace Bulldozer
{
    public class PluginConfigWindow
    {
        public static bool visible;
        private static Rect _windowRect = new((Screen.width / 3) * 2, 100f, 500f, 820f);

        private static int _leftColumnWidth;
        public static bool NeedReinit;

        private static Texture2D _tooltipBg;
        private static Dictionary<Color, Texture2D> _customColorTextures = new();

        private static readonly Dictionary<string, int> previousSelections = new();
        private static string _savedGUISkin;
        private static GUISkin _savedGUISkinObj;
        private static Color _savedColor;
        private static Color _savedBackgroundColor;
        private static Color _savedContentColor;
        private static GUISkin _mySkin;
        public static void OnOpen()
        {
            visible = true;
        }

        public static void OnClose()
        {
            visible = false;
            RestoreGuiSkinOptions();
        }

        public static void OnGUI()
        {
            if (RegionColorWindow.visible)
                RegionColorWindow.OnGUI();
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                visible = false;
                return;
            }

            Init();

            _windowRect = GUILayout.Window(1297895555, _windowRect, WindowFn, "Bulldozer options");

        }

        private static void SaveCurrentGuiOptions()
        {
            _savedBackgroundColor = GUI.backgroundColor;
            _savedContentColor = GUI.contentColor;
            _savedColor = GUI.color;
            GUI.backgroundColor = Color.white;
            GUI.contentColor = Color.white;
            GUI.color = Color.white;

            if (_mySkin == null || NeedReinit)
            {
                _savedGUISkin = JsonUtility.ToJson(GUI.skin);
                _savedGUISkinObj = GUI.skin;
                _mySkin = ScriptableObject.CreateInstance<GUISkin>();
                JsonUtility.FromJsonOverwrite(_savedGUISkin, _mySkin);
                GUI.skin = _mySkin;
                GUI.skin.button.normal.textColor = Color.white;
                GUI.skin.textArea.normal.textColor = Color.white;
                GUI.skin.textField.normal.textColor = Color.white;
                GUI.skin.toggle.normal.textColor = Color.white;
                GUI.skin.toggle.onNormal.textColor = Color.white;
                GUI.skin.button.normal.textColor = Color.white;
                GUI.skin.button.onNormal.textColor = Color.white;
                GUI.skin.button.onActive.textColor = Color.white;
                GUI.skin.button.active.textColor = Color.white;
                GUI.skin.label.hover.textColor = Color.white;
                GUI.skin.label.onNormal.textColor = Color.white;
                GUI.skin.label.normal.textColor = Color.white;
            }
            else
            {
                _savedGUISkinObj = GUI.skin;
                GUI.skin = _mySkin;
            }
        }

        private static void RestoreGuiSkinOptions()
        {
            GUI.skin = _savedGUISkinObj;
            GUI.backgroundColor = _savedBackgroundColor;
            GUI.contentColor = _savedContentColor;
            GUI.color = _savedColor;
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


        private static void WindowFn(int id)
        {
            SaveCurrentGuiOptions();


            GUILayout.BeginArea(new Rect(_windowRect.width - 25f, 0f, 25f, 30f));
            if (GUILayout.Button("X"))
            {
                OnClose();
                return;
            }

            GUILayout.EndArea();
            GUILayout.BeginVertical(GUI.skin.box);
            {
                DrawCenteredLabel("Hover over labels to see descriptions");
                DrawResetAllButton();
                var lastSection = "";

                foreach (var configDefinition in PluginConfig.PluginConfigFile.Keys)
                {
                    // edit config to tweak performance config settings 
                    if (configDefinition.Section == "UIOnly")
                    {
                        continue;
                    }

                    var configEntry = PluginConfig.PluginConfigFile[configDefinition];
                    if (configEntry.Description.Tags.Contains("configEditOnly"))
                    {
                        continue;
                    }

                    if (configDefinition.Section != lastSection)
                    {
                        // DrawCenteredLabel(configDefinition.Section);
                        if (configDefinition.Section == "CustomColors")
                            DrawCenteredLabel("0-15 are built in colors, 16-31 are user defined");
                        // else
                        //     DrawCenteredLabel("");
                    }
                    if (lastSection == "CustomColors" && configDefinition.Section != lastSection)
                    {
                        var button = GUILayout.Button("Edit Regional Colors");
                        if (button)
                            RegionColorWindow.Toggle();
                    }


                    DrawSetting(configEntry);

                    lastSection = configDefinition.Section;
                }
            }
            GUILayout.EndVertical();
            if (GUI.tooltip != null)
            {
                // GUILayout.BeginHorizontal(GUI.skin.textArea,  GUILayout.Height(250f));

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
            RestoreGuiSkinOptions();
        }

        private static void DrawResetAllButton()
        {
            GUILayout.BeginVertical();
            var clicked = GUILayout.Button(new GUIContent("Reset", "Reset all settings to their default values"), GUILayout.MaxWidth(_windowRect.width / 5));
            if (clicked)
            {
                PluginConfig.ResetConfigWindowOptionsToDefault();
                previousSelections.Clear();
            }

            GUILayout.EndVertical();
        }

        private static void DrawSetting(ConfigEntryBase configEntry)
        {
            GUILayout.BeginHorizontal();
            DrawSettingName(configEntry);
            var descriptionAdded = false;
            if (configEntry.SettingType.IsEnum)
            {
                descriptionAdded = AddPicker(configEntry, configEntry.SettingType, configEntry.BoxedValue);
            }

            if (!descriptionAdded && configEntry.SettingType == typeof(bool))
            {
                descriptionAdded = DrawBoolField(configEntry);
            }

            if (!descriptionAdded && configEntry.SettingType == typeof(int))
            {
                descriptionAdded = DrawRangeField(configEntry);
            }

            if (!descriptionAdded && configEntry.SettingType == typeof(float))
            {
                descriptionAdded = DrawFloatRangeField(configEntry);
            }

            if (!descriptionAdded)
            {
                //something went wrong, default to text field
                GUILayout.Label(new GUIContent(configEntry.BoxedValue.ToString(), "Current setting"));
            }

            GUILayout.EndHorizontal();
        }

        private static bool DrawRangeField(ConfigEntryBase configEntry)
        {
            bool addColorIndicator = configEntry.Description.Tags.Contains("color");
            if (configEntry.Description.AcceptableValues is not AcceptableValueRange<int> acceptableValueRange)
            {
                return false;
            }

            GUILayout.BeginHorizontal();
            AcceptableValueRange<int> acceptableValues = acceptableValueRange;
            var converted = (float)Convert.ToDouble(configEntry.BoxedValue, CultureInfo.InvariantCulture);
            var leftValue = (float)Convert.ToDouble(acceptableValues.MinValue, CultureInfo.InvariantCulture);
            var rightValue = (float)Convert.ToDouble(acceptableValues.MaxValue, CultureInfo.InvariantCulture);

            var result = GUILayout.HorizontalSlider(converted, leftValue, rightValue, GUILayout.MinWidth(200));
            if (Math.Abs(result - converted) > Mathf.Abs(rightValue - leftValue) / 1000)
            {
                var newValue = Convert.ChangeType(result, configEntry.SettingType, CultureInfo.InvariantCulture);
                configEntry.BoxedValue = newValue;
            }

            var strVal = configEntry.BoxedValue.ToString();
            var strResult = GUILayout.TextField(strVal, GUILayout.Width(50));
            if (addColorIndicator)
            {
                var customColor = GetColorByIndex((int)configEntry.BoxedValue);
                var customColorTexture = GetTextureForColor(customColor);
                GUILayout.Label(customColorTexture);
            }

            GUILayout.EndHorizontal();
            if (strResult != strVal)
            {
                try
                {
                    var resultVal = (float)Convert.ToDouble(strResult, CultureInfo.InvariantCulture);
                    var clampedResultVal = Mathf.Clamp(resultVal, leftValue, rightValue);
                    configEntry.BoxedValue = (int)clampedResultVal;
                }
                catch (FormatException)
                {
                    // Ignore user typing in bad data
                }
            }

            return true;
        }

        private static bool DrawFloatRangeField(ConfigEntryBase configEntry)
        {
            if (configEntry.Description.AcceptableValues is not AcceptableValueRange<float> acceptableValueRange)
            {
                return false;
            }

            GUILayout.BeginHorizontal();
            AcceptableValueRange<float> acceptableValues = acceptableValueRange;
            var converted = (float)configEntry.BoxedValue;
            var leftValue = acceptableValues.MinValue;
            var rightValue = acceptableValues.MaxValue;

            var result = GUILayout.HorizontalSlider(converted, leftValue, rightValue, GUILayout.MinWidth(200));
            if (Math.Abs(result - converted) > Mathf.Abs(rightValue - leftValue) / 1000)
            {
                var newValue = Convert.ChangeType(Mathf.Round(result / 0.36f) * 0.36f, configEntry.SettingType, CultureInfo.InvariantCulture);
                configEntry.BoxedValue = newValue;
            }

            var strVal = ((float)configEntry.BoxedValue).ToString("F2", CultureInfo.CurrentCulture);
            var strResult = GUILayout.TextField(strVal, GUILayout.Width(50));

            GUILayout.EndHorizontal();
            if (strResult != strVal)
            {
                try
                {
                    var resultVal = float.Parse(strResult, NumberStyles.Any, CultureInfo.CurrentCulture);
                    var clampedResultVal = Mathf.Clamp(resultVal, leftValue, rightValue);
                    configEntry.BoxedValue = clampedResultVal;
                }
                catch (FormatException)
                {
                    // Ignore user typing in bad data
                }
            }

            return true;
        }

        public static Color GetColorByIndex(int index)
        {
            if (index < 0 || index > 31)
            {
                Log.logger.LogWarning($"color index out of bounds {index}");
                index = 3;
            }
            Color[] reformColors = Configs.builtin.reformColors;
            if (index < 16)
            {
                return reformColors[index];
            }

            return GameMain.mainPlayer.factory.platformSystem.reformCustomColors[index - 16];
        }

        public static Texture2D GetTextureForColor(Color color)
        {
            if (_customColorTextures.ContainsKey(color))
            {
                return _customColorTextures[color];
            }

            var texture = new Texture2D(15, 15, TextureFormat.RGB24, false);
            var colorBlock = new Color[texture.width * texture.height];
            for (int i = 0; i < colorBlock.Length; i++)
            {
                colorBlock[i] = color;
            }
            texture.SetPixels(0, 0, texture.width, texture.height, colorBlock);
            texture.Apply();
            _customColorTextures[color] = texture;
            return texture;
        }

        private static void DrawSettingName(ConfigEntryBase setting)
        {
            var guiContent = new GUIContent(setting.Definition.Key, setting.Description.Description);
            GUILayout.Label(guiContent, GUILayout.Width(_leftColumnWidth), GUILayout.MaxWidth(_leftColumnWidth));
            GUILayout.FlexibleSpace();
        }


        private static bool AddPicker(ConfigEntryBase entry, Type enumType, object currentValue)
        {
            if (!enumType.IsEnum)
            {
                Debug.LogWarning("picker must only be used with enums");
                return false;
            }

            var names = Enum.GetNames(enumType);
            var selectedName = Enum.GetName(enumType, currentValue);
            var index = -1;
            for (var i = 0; i < names.Length; i++)
            {
                var name = names[i];
                if (selectedName == name)
                {
                    index = i;
                }
            }

            if (index == -1)
            {
                Console.WriteLine($"did not find index of {currentValue} for {enumType}");
                return false;
            }

            var guiContents = names.Select(n => GetGuiContent(enumType, n, entry.Description.Description, selectedName == n));
            GUILayout.BeginVertical("Box");
            if (!previousSelections.ContainsKey(entry.Definition.Key))
            {
                previousSelections[entry.Definition.Key] = index;
            }

            var previousSelection = previousSelections[entry.Definition.Key];
            index = GUILayout.Toolbar(previousSelection, guiContents.ToArray());

            if (previousSelections[entry.Definition.Key] != index)
            {
                var updatedEnumValue = Enum.Parse(enumType, names[index], true);
                entry.BoxedValue = updatedEnumValue;
                previousSelections[entry.Definition.Key] = index;
            }

            GUILayout.EndVertical();
            return true;
        }

        private static GUIContent GetGuiContent(Type enumType, string sourceValue, string parentDescription, bool currentlySelected)
        {
            var enumMember = enumType.GetMember(sourceValue).FirstOrDefault();
            var attr = enumMember?.GetCustomAttributes(typeof(DescriptionAttribute), false).Cast<DescriptionAttribute>().FirstOrDefault();
            var currentlySelectedIndicator = currentlySelected ? "<b>(selected)</b> " : "";
            var label = currentlySelected ? $"<b>{sourceValue}</b>" : sourceValue;
            if (attr != null)
            {
                return new GUIContent(label, $"<b>{parentDescription}</b>\n\n{currentlySelectedIndicator}{attr.Description}");
            }

            return new GUIContent(label);
        }

        private static bool DrawBoolField(ConfigEntryBase setting)
        {
            if (setting.SettingType != typeof(bool))
            {
                return false;
            }

            var boolVal = (bool)setting.BoxedValue;

            GUILayout.BeginHorizontal();
            // var result = GUILayout.Toggle(boolVal, new GUIContent(boolVal ? "Enabled" : "Disabled", "Click to toggle"), style ,GUILayout.ExpandWidth(true));
            var result = GUILayout.Toggle(boolVal, boolVal ? "Enabled" : "Disabled");
            // var result = GUILayout.Toggle(boolVal, boolVal ? "Enabled" : "Disabled", style);
            if (result != boolVal)
            {
                setting.BoxedValue = result;
            }

            GUILayout.EndHorizontal();
            return true;
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

        public static void DrawCenteredLabel(string text, params GUILayoutOption[] options)
        {
            GUILayout.BeginHorizontal(options);
            GUILayout.FlexibleSpace();
            GUILayout.Label(text);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }


        [HarmonyPrefix]
        [HarmonyPatch(typeof(UIGame), "On_E_Switch")]
        public static bool UIGame_On_E_Switch_Prefix()
        {
            if (visible)
            {
                visible = false;
            }

            return true;
        }
    }
}