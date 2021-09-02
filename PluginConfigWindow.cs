using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using BepInEx.Configuration;
using UnityEngine;

namespace Bulldozer
{
    public class PluginConfigWindow
    {
        public static bool visible;
        private static Rect _windowRect = new Rect(300f, 250f, 500f, 600f);

        private static int _rightColumnWidth;
        private static int _leftColumnWidth;
        public static bool NeedReinit = false;


        private static Texture2D _tooltipBg;
        private static Rect _screenRect;
        private static Texture2D _windowBg;

        private static int _loggedMessageCount = 0;
        private static Dictionary<string, int> previousSelections = new Dictionary<string, int>();

        public static void OnOpen()
        {
            visible = true;
        }

        public static void OnClose()
        {
            visible = false;
        }

        public static void OnGUI()
        {
            var origSkin = GUI.skin;
            var origBGColor = GUI.backgroundColor;
            GUI.backgroundColor = Color.black;
            // GUI.skin = null;

            Init();

            _windowRect = GUILayout.Window(1297895555, _windowRect, WindowFn, "Bulldozer options");
            GUI.skin = origSkin;
            GUI.backgroundColor = origBGColor;
        }

        private static void Init()
        {
            if (_tooltipBg == null && !NeedReinit)
            {
                return;
            }

            var background = new Texture2D(1, 1, TextureFormat.RGB24, false);
            background.SetPixel(0, 0, Color.black);
            background.Apply();
            _tooltipBg = background;
            var windowBackground = new Texture2D(1, 1, TextureFormat.RGB24, false);
            var color = new Color(0.15f, 0.15f, 0.15f, 0);
            windowBackground.SetPixel(0, 0, color);
            windowBackground.Apply();
            _windowBg = windowBackground;
            InitWindowRect();
            NeedReinit = false;
        }


        private static void WindowFn(int id)
        {
            GUILayout.BeginArea(new Rect(_windowRect.width - 25f, 0f, 25f, 30f));
            if (GUILayout.Button("X"))
            {
                OnClose();
                return;
            }

            GUILayout.EndArea();
            // GUILayout.BeginHorizontal();
            // GUILayout.BeginArea(new Rect(_windowRect.x, _windowRect.y + 30f, _windowRect.width, _windowRect.height - 30f), GUISt);
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
                        DrawCenteredLabel("");
                    }

                    lastSection = configDefinition.Section;

                    DrawSetting(configEntry);
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
                // GUILayout.EndHorizontal();
            }
            // GUILayout.EndArea();
            // GUILayout.EndHorizontal();

            GUI.DragWindow();
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

            if (!descriptionAdded)
            {
                //something went wrong, default to text field
                GUILayout.Label(new GUIContent(configEntry.BoxedValue.ToString(), "Current setting"));
            }

            GUILayout.EndHorizontal();
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
                Debug.LogWarning($"picker must only be used with enums");
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
            // GUILayout.BeginVertical("Box");
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
                Console.WriteLine($"updating selection to {names[index]}, was {names[previousSelection]} {updatedEnumValue}");
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
            var style = new GUIStyle
            {
                normal = new GUIStyleState { textColor = Color.white, background = _tooltipBg },
                wordWrap = true,
                alignment = TextAnchor.MiddleCenter
            };

            GUILayout.BeginHorizontal();
            // var result = GUILayout.Toggle(boolVal, new GUIContent(boolVal ? "Enabled" : "Disabled", "Click to toggle"), style ,GUILayout.ExpandWidth(true));
            var result = GUILayout.Toggle(boolVal, boolVal ? "Enabled" : "Disabled");
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

            _screenRect = new Rect(0, 0, Screen.width, Screen.height);

            _leftColumnWidth = Mathf.RoundToInt(_windowRect.width / 2.5f);
            _rightColumnWidth = (int)_windowRect.width - _leftColumnWidth - 115;
        }

        public static void DrawCenteredLabel(string text, params GUILayoutOption[] options)
        {
            GUILayout.BeginHorizontal(options);
            GUILayout.FlexibleSpace();
            GUILayout.Label(text);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
        }
    }
}