﻿using System;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.UI;

namespace Bulldozer
{
    public class UIElements : MonoBehaviour
    {
        public static ManualLogSource logger;

        public GameObject DrawEquatorCheck;

        // use to track whether checkbox needs to be synced
        private bool _drawEquatorField = true;
        private bool _repaveField = true;
        private bool _destroyFactoryMachines = false;
        [NonSerialized] public Image CheckBoxImage;
        [NonSerialized] public Image RepaveCheckBoxImage;
        [NonSerialized] public Image DestroyMachinesCheckBoxImage;
        public Sprite spriteChecked;
        public Sprite spriteUnChecked;
        private GameObject _newSeparator;
        private static List<GameObject> gameObjectsToDestroy = new List<GameObject>();

        public UIButton PaveActionButton;
        public RectTransform BulldozeButton;
        public Text countText;
        public Text buttonHotkeyTextComponent;
        public Transform CountTransform;
        private Image _iconImage;
        private Transform _bulldozeIcon;
        public CheckboxControl drawEquatorCheckboxButton;
        private Text hover;
        private UIButton mainActionButton;

        private const string DEFAULT_TIP_MESSAGE =
            "Adds foundation to entire planet. Any existing foundation colors will be lost.\nCurrently selected options for burying veins and foundation type will be used.\nGame may lag a bit after invocation. Press again to halt.";

        private const string DEFAULT_TIP_MESSAGE_DESTROY_FACTORY =
            "Destroy all factory machines.\nGame may lag a bit after invocation. Press again to halt.";

        private bool currentTipMessageIsDefault = true;


        public void AddBulldozeComponents(RectTransform environmentModificationContainer, UIBuildMenu uiBuildMenu, GameObject button1, Action<int> action)
        {
            InitOnOffSprites();
            InitActionButton(button1, action);
            InitDrawEquatorCheckbox(environmentModificationContainer, button1);
            InitRepaveCheckbox(environmentModificationContainer);
            InitDestroyMachinesCheckbox(environmentModificationContainer);
        }

        private void InitActionButton(GameObject button1, Action<int> action)
        {
            countText = null;
            BulldozeButton = CopyButton(button1.GetComponent<RectTransform>(), Vector2.right * (5 + button1.GetComponent<RectTransform>().sizeDelta.x), out countText,
                Helper.GetSprite("bulldoze"), action);
            gameObjectsToDestroy.Add(BulldozeButton.gameObject);
            PaveActionButton = BulldozeButton.GetComponent<UIButton>();
            gameObjectsToDestroy.Add(countText.gameObject);
        }

        private void InitOnOffSprites()
        {
            Texture2D texOff = Resources.Load<Texture2D>("ui/textures/sprites/icons/checkbox-off");
            Texture2D texOn = Resources.Load<Texture2D>("ui/textures/sprites/icons/checkbox-on");
            spriteChecked = Sprite.Create(texOn, new Rect(0, 0, texOn.width, texOn.height), new Vector2(0.5f, 0.5f));
            spriteUnChecked = Sprite.Create(texOff, new Rect(0, 0, texOff.width, texOff.height), new Vector2(0.5f, 0.5f));
        }

        private void InitDrawEquatorCheckbox(RectTransform environmentModificationContainer, GameObject button1)
        {
            DrawEquatorCheck = new GameObject("Draw equator line");
            gameObjectsToDestroy.Add(DrawEquatorCheck);
            RectTransform rect = DrawEquatorCheck.AddComponent<RectTransform>();
            rect.SetParent(environmentModificationContainer.transform, false);

            rect.anchorMax = new Vector2(0, 1);
            rect.anchorMin = new Vector2(0, 1);
            rect.sizeDelta = new Vector2(20, 20);
            rect.pivot = new Vector2(0, 0.5f);
            rect.anchoredPosition = new Vector2(350, -90);
            drawEquatorCheckboxButton = rect.gameObject.AddComponent<CheckboxControl>();
            drawEquatorCheckboxButton.HoverText = "Add a green line around equator and blue lines at meridian points";
            drawEquatorCheckboxButton.onClick += data =>
            {
                PluginConfig.addGuideLines.Value = !PluginConfig.addGuideLines.Value;
                CheckBoxImage.sprite = PluginConfig.addGuideLines.Value ? spriteChecked : spriteUnChecked;
                _drawEquatorField = PluginConfig.addGuideLines.Value;
            };

            if (countText != null)
            {
                hover = Instantiate(countText, environmentModificationContainer.transform, true);
                gameObjectsToDestroy.Add(hover.gameObject);
                var copiedRectTransform = hover.GetComponent<RectTransform>();
                var parentRect = environmentModificationContainer.GetComponent<RectTransform>();
                copiedRectTransform.anchorMin = new Vector2(0, 1);
                copiedRectTransform.anchorMax = new Vector2(0, 1);
                copiedRectTransform.sizeDelta = new Vector2(500, 20);
                copiedRectTransform.anchoredPosition = new Vector2(620, parentRect.transform.position.y - 115);
                drawEquatorCheckboxButton.textObject = hover;
            }

            gameObjectsToDestroy.Add(drawEquatorCheckboxButton.gameObject);

            CheckBoxImage = drawEquatorCheckboxButton.gameObject.AddComponent<Image>();
            CheckBoxImage.color = new Color(0.8f, 0.8f, 0.8f, 1);
            gameObjectsToDestroy.Add(CheckBoxImage.gameObject);

            CheckBoxImage.sprite = PluginConfig.addGuideLines.Value ? spriteChecked : spriteUnChecked;
            var sepLine = GameObject.Find("UI Root/Overlay Canvas/In Game/Function Panel/Build Menu/reform-group/sep-line-left-0");
            try
            {
                _newSeparator = Instantiate(sepLine, environmentModificationContainer.transform);
                _newSeparator.tag = "Bulldozer plugin sep line";
                var button1Rect = button1.GetComponent<RectTransform>();
                _newSeparator.GetComponent<RectTransform>().anchoredPosition = new Vector2(button1Rect.anchoredPosition.x + button1Rect.sizeDelta.x - 25,
                    sepLine.GetComponent<RectTransform>().anchoredPosition.y);
                gameObjectsToDestroy.Add(_newSeparator.gameObject);
            }
            catch (Exception e)
            {
                logger.LogWarning($"exception in sep line {e.Message}");
            }
        }

        private void InitRepaveCheckbox(RectTransform environmentModificationContainer)
        {
            var repaveCheck = new GameObject("Repave");
            gameObjectsToDestroy.Add(repaveCheck);
            RectTransform rect = repaveCheck.AddComponent<RectTransform>();
            rect.SetParent(environmentModificationContainer.transform, false);

            rect.anchorMax = new Vector2(0, 1);
            rect.anchorMin = new Vector2(0, 1);
            rect.sizeDelta = new Vector2(20, 20);
            rect.pivot = new Vector2(0, 0.5f);
            rect.anchoredPosition = new Vector2(350, -105);
            var repaveCheckboxButton = rect.gameObject.AddComponent<CheckboxControl>();
            repaveCheckboxButton.HoverText = "Uncheck to skip paving already paved areas. If unchecked veins will not be repaved";

            if (countText != null)
            {
                var repaveHover = Instantiate(countText, environmentModificationContainer.transform, true);
                gameObjectsToDestroy.Add(repaveHover.gameObject);
                var copiedRectTransform = repaveHover.GetComponent<RectTransform>();
                var parentRect = environmentModificationContainer.GetComponent<RectTransform>();
                copiedRectTransform.anchorMin = new Vector2(0, 1);
                copiedRectTransform.anchorMax = new Vector2(0, 1);
                copiedRectTransform.sizeDelta = new Vector2(500, 20);
                copiedRectTransform.anchoredPosition = new Vector2(700, parentRect.transform.position.y - 115);
                repaveCheckboxButton.textObject = repaveHover;
            }

            gameObjectsToDestroy.Add(repaveCheckboxButton.gameObject);

            RepaveCheckBoxImage = repaveCheckboxButton.gameObject.AddComponent<Image>();
            RepaveCheckBoxImage.color = new Color(0.8f, 0.8f, 0.8f, 1);
            gameObjectsToDestroy.Add(RepaveCheckBoxImage.gameObject);

            RepaveCheckBoxImage.sprite = PluginConfig.repaveAll.Value ? spriteChecked : spriteUnChecked;
            repaveCheckboxButton.onClick += data =>
            {
                // RepaveField = !RepaveField;
                PluginConfig.repaveAll.Value = !PluginConfig.repaveAll.Value;
                RepaveCheckBoxImage.sprite = PluginConfig.repaveAll.Value ? spriteChecked : spriteUnChecked;
                _repaveField = PluginConfig.repaveAll.Value;
            };
        }

        private void InitDestroyMachinesCheckbox(RectTransform environmentModificationContainer)
        {
            var destroyCheck = new GameObject("DestroyMachines");
            gameObjectsToDestroy.Add(destroyCheck);
            RectTransform rect = destroyCheck.AddComponent<RectTransform>();
            rect.SetParent(environmentModificationContainer.transform, false);

            rect.anchorMax = new Vector2(0, 1);
            rect.anchorMin = new Vector2(0, 1);
            rect.sizeDelta = new Vector2(20, 20);
            rect.pivot = new Vector2(0, 0.5f);
            rect.anchoredPosition = new Vector2(350, -120);
            var destroyMachinesButton = rect.gameObject.AddComponent<CheckboxControl>();
            destroyMachinesButton.HoverText = "Clear all factory machines. Skip adding foundation. Edit property FlattenWithFactoryTearDown to do both";

            if (countText != null)
            {
                var destroyHover = Instantiate(countText, environmentModificationContainer.transform, true);
                gameObjectsToDestroy.Add(destroyHover.gameObject);
                var copiedRectTransform = destroyHover.GetComponent<RectTransform>();
                var parentRect = environmentModificationContainer.GetComponent<RectTransform>();
                copiedRectTransform.anchorMin = new Vector2(0, 1);
                copiedRectTransform.anchorMax = new Vector2(0, 1);
                copiedRectTransform.sizeDelta = new Vector2(800, 20);
                copiedRectTransform.anchoredPosition = new Vector2(700, parentRect.transform.position.y - 115);
                destroyMachinesButton.textObject = destroyHover;
            }

            gameObjectsToDestroy.Add(destroyMachinesButton.gameObject);

            DestroyMachinesCheckBoxImage = destroyMachinesButton.gameObject.AddComponent<Image>();
            DestroyMachinesCheckBoxImage.color = new Color(0.8f, 0.8f, 0.8f, 1);
            gameObjectsToDestroy.Add(DestroyMachinesCheckBoxImage.gameObject);

            DestroyMachinesCheckBoxImage.sprite = PluginConfig.destroyFactoryAssemblers.Value ? spriteChecked : spriteUnChecked;
            destroyMachinesButton.onClick += data =>
            {
                PluginConfig.destroyFactoryAssemblers.Value = !PluginConfig.destroyFactoryAssemblers.Value;
                DestroyMachinesCheckBoxImage.sprite = PluginConfig.destroyFactoryAssemblers.Value ? spriteChecked : spriteUnChecked;
                _destroyFactoryMachines = PluginConfig.destroyFactoryAssemblers.Value;
            };
        }

        public void Update()
        {
            if (_drawEquatorField != PluginConfig.addGuideLines.Value)
            {
                // value might have been updated in config manager plugin ui
                CheckBoxImage.sprite = PluginConfig.addGuideLines.Value ? spriteChecked : spriteUnChecked;
                _drawEquatorField = PluginConfig.addGuideLines.Value;
            }

            if (_repaveField != PluginConfig.repaveAll.Value)
            {
                // sync checkbox with externally changed value
                RepaveCheckBoxImage.sprite = PluginConfig.repaveAll.Value ? spriteChecked : spriteUnChecked;
                _repaveField = PluginConfig.repaveAll.Value;
            }

            if (_destroyFactoryMachines != PluginConfig.destroyFactoryAssemblers.Value)
            {
                // sync checkbox with externally changed value
                DestroyMachinesCheckBoxImage.sprite = PluginConfig.destroyFactoryAssemblers.Value ? spriteChecked : spriteUnChecked;
                _destroyFactoryMachines = PluginConfig.destroyFactoryAssemblers.Value;
            }

            if (_destroyFactoryMachines)
            {
                if (mainActionButton != null && currentTipMessageIsDefault)
                {
                    currentTipMessageIsDefault = false;
                    mainActionButton.tips.tipText = DEFAULT_TIP_MESSAGE_DESTROY_FACTORY;
                }
            }
            else if (mainActionButton != null && !currentTipMessageIsDefault)
            {
                currentTipMessageIsDefault = true;
                mainActionButton.tips.tipText = DEFAULT_TIP_MESSAGE;
            }
        }

        public void Unload()
        {
            try
            {
                while (gameObjectsToDestroy.Count > 0)
                {
                    Destroy(gameObjectsToDestroy[0]);
                    gameObjectsToDestroy.RemoveAt(0);
                }
            }
            catch (Exception e)
            {
                logger.LogWarning($"failed to do unload {e.Message}");
                logger.LogWarning(e.StackTrace);
            }
        }

        public RectTransform CopyButton(RectTransform rectTransform, Vector2 positionDelta, out Text countComponent, Sprite newIcon, Action<int> action)
        {
            var copied = Instantiate(rectTransform, rectTransform.transform.parent, false);
            var copiedRectTransform = copied.GetComponent<RectTransform>();
            var originalRectTransform = rectTransform.GetComponent<RectTransform>();

            copiedRectTransform.anchorMin = originalRectTransform.anchorMin;
            copiedRectTransform.anchorMax = originalRectTransform.anchorMax;
            copiedRectTransform.sizeDelta = originalRectTransform.sizeDelta;
            copiedRectTransform.anchoredPosition = originalRectTransform.anchoredPosition + positionDelta;


            _bulldozeIcon = copiedRectTransform.transform.Find("icon");
            if (_bulldozeIcon != null)
            {
                gameObjectsToDestroy.Add(_bulldozeIcon.gameObject);
                _iconImage = _bulldozeIcon.GetComponentInChildren<Image>();
                if (_iconImage != null)
                {
                    _iconImage.sprite = newIcon;
                    gameObjectsToDestroy.Add(_iconImage.gameObject);
                }
            }

            var buttonHotkeyText = copiedRectTransform.transform.Find("text");
            if (buttonHotkeyText != null)
            {
                buttonHotkeyTextComponent = buttonHotkeyText.GetComponentInChildren<Text>();
                if (buttonHotkeyTextComponent != null)
                {
                    buttonHotkeyTextComponent.text = "";
                }
            }

            mainActionButton = copiedRectTransform.GetComponentInChildren<UIButton>();
            if (mainActionButton != null)
            {
                originalRectTransform.GetComponentInChildren<UIButton>();
                mainActionButton.tips.tipTitle = "Bulldoze";
                mainActionButton.tips.tipText = DEFAULT_TIP_MESSAGE;
                mainActionButton.tips.offset = new Vector2(mainActionButton.tips.offset.x, mainActionButton.tips.offset.y + 100);
                mainActionButton.onClick += action;
            }

            CountTransform = copiedRectTransform.transform.Find("count");
            if (CountTransform != null)
            {
                countComponent = CountTransform.GetComponentInChildren<Text>();
                if (countComponent != null)
                {
                    countComponent.text = "0";
                    return copied;
                }
            }


            countComponent = null;
            return copied;
        }

        public void Show()
        {
            BulldozeButton.gameObject.SetActive(true);
            PaveActionButton.gameObject.SetActive(true);
            CheckBoxImage.gameObject.SetActive(true);
            if (RepaveCheckBoxImage.gameObject != null)
                RepaveCheckBoxImage.gameObject.SetActive(true);
            if (DestroyMachinesCheckBoxImage.gameObject != null)
                DestroyMachinesCheckBoxImage.gameObject.SetActive(true);
        }

        public void Hide()
        {
            BulldozeButton.gameObject.SetActive(false);
            PaveActionButton.gameObject.SetActive(false);
            CheckBoxImage.gameObject.SetActive(false);
            DestroyMachinesCheckBoxImage.gameObject.SetActive(false);
        }
    }


    public class Helper
    {
        public static Sprite GetSprite(string spriteName)
        {
            var readTex = BulldozeIcon.GetIconTexture(spriteName);

            return Sprite.Create(readTex, new Rect(0f, 0, readTex.width, readTex.height),
                new Vector2(readTex.width, readTex.height), 1000);
        }
    }
}