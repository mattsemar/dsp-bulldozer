using System;
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
        public bool DrawEquatorField = true;
        public bool RepaveField = true;
        public Image CheckBoxImage;
        public Image RepaveCheckBoxImage;
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


        public void AddBulldozeComponents(RectTransform environmentModificationContainer, UIBuildMenu uiBuildMenu, GameObject button1, Action<int> action)
        {
            InitOnOffSprites();
            InitActionButton(button1, action);
            InitDrawEquatorCheckbox(environmentModificationContainer, button1);
            InitRepaveCheckbox(environmentModificationContainer, button1);
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
            rect.anchoredPosition = new Vector2(350, -100);
            drawEquatorCheckboxButton = rect.gameObject.AddComponent<CheckboxControl>();
            drawEquatorCheckboxButton.HoverText = "Add a green line around equator and blue lines at meridian points";
            drawEquatorCheckboxButton.onClick += data =>
            {
                DrawEquatorField = !DrawEquatorField;
                CheckBoxImage.sprite = DrawEquatorField ? spriteChecked : spriteUnChecked;
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

            CheckBoxImage.sprite = DrawEquatorField ? spriteChecked : spriteUnChecked;
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

        private void InitRepaveCheckbox(RectTransform environmentModificationContainer, GameObject button1)
        {
            var repaveCheck = new GameObject("Repave");
            gameObjectsToDestroy.Add(repaveCheck);
            RectTransform rect = repaveCheck.AddComponent<RectTransform>();
            rect.SetParent(environmentModificationContainer.transform, false);

            rect.anchorMax = new Vector2(0, 1);
            rect.anchorMin = new Vector2(0, 1);
            rect.sizeDelta = new Vector2(20, 20);
            rect.pivot = new Vector2(0, 0.5f);
            rect.anchoredPosition = new Vector2(350, -120);
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

            RepaveCheckBoxImage.sprite = RepaveField ? spriteChecked : spriteUnChecked;
            repaveCheckboxButton.onClick += data =>
            {
                RepaveField = !RepaveField;
                RepaveCheckBoxImage.sprite = RepaveField ? spriteChecked : spriteUnChecked;
            };
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

            var copiedUiButton = copiedRectTransform.GetComponentInChildren<UIButton>();
            if (copiedUiButton != null)
            {
                originalRectTransform.GetComponentInChildren<UIButton>();
                copiedUiButton.tips.tipTitle = "Bulldoze";
                copiedUiButton.tips.tipText =
                    "Adds foundation to entire planet. Any existing foundation colors will be lost.\nCurrently selected options for burying veins and foundation type will be used.\nGame may lag a bit after invocation. Press again to halt.";
                copiedUiButton.tips.offset = new Vector2(copiedUiButton.tips.offset.x, copiedUiButton.tips.offset.y + 100);
                copiedUiButton.onClick += action;
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
        }

        public void Hide()
        {
            BulldozeButton.gameObject.SetActive(false);
            PaveActionButton.gameObject.SetActive(false);
            CheckBoxImage.gameObject.SetActive(false);
            RepaveCheckBoxImage.gameObject.SetActive(false);
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