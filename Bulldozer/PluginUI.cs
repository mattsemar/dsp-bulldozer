using System;
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
        public GameObject CheckboxText;
        public Image CheckBoxImage;
        public Sprite spriteChecked;
        public Sprite spriteUnChecked;
        private GameObject newSeparator;

        public UIButton PaveActionButton;
        public RectTransform BulldozeButton;
        public Text countText;
        public Text buttonHotkeyTextComponent;
        public Transform CountTransform;
        private Image _iconImage;
        private Transform _bulldozeIcon;


        public void AddBulldozeComponents(RectTransform environmentModificationContainer, UIBuildMenu uiBuildMenu, GameObject button1, Action<int> action)
        {
            InitOnOffSprites();
            InitActionButton(button1, action);
            InitDrawEquatorCheckbox(environmentModificationContainer, button1);
        }

        private void InitActionButton(GameObject button1, Action<int> action)
        {
            countText = null;
            BulldozeButton = CopyButton(button1.GetComponent<RectTransform>(), Vector2.right * (5 + button1.GetComponent<RectTransform>().sizeDelta.x), out countText,
                Helper.GetSprite("bulldoze"), action);
            PaveActionButton = BulldozeButton.GetComponent<UIButton>();
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
            RectTransform rect = DrawEquatorCheck.AddComponent<RectTransform>();
            rect.SetParent(environmentModificationContainer.transform, false);

            rect.anchorMax = new Vector2(0, 1);
            rect.anchorMin = new Vector2(0, 1);
            rect.sizeDelta = new Vector2(20, 20);
            rect.pivot = new Vector2(0, 0.5f);
            rect.anchoredPosition = new Vector2(350, -100);
            Button drawEquatorCheckboxButton = rect.gameObject.AddComponent<Button>();


            drawEquatorCheckboxButton.onClick.AddListener(() =>
            {
                DrawEquatorField = !DrawEquatorField;
                CheckBoxImage.sprite = DrawEquatorField ? spriteChecked : spriteUnChecked;
            });


            CheckBoxImage = drawEquatorCheckboxButton.gameObject.AddComponent<Image>();
            CheckBoxImage.color = new Color(0.8f, 0.8f, 0.8f, 1);
        

            CheckBoxImage.sprite = DrawEquatorField ? spriteChecked : spriteUnChecked;
            var sepLine = GameObject.Find("UI Root/Overlay Canvas/In Game/Function Panel/Build Menu/reform-group/sep-line-left-0");
            try
            {
                newSeparator = Instantiate(sepLine, environmentModificationContainer.transform);
                newSeparator.tag = "Bulldozer plugin sep line";
                var button1Rect = button1.GetComponent<RectTransform>();
                newSeparator.GetComponent<RectTransform>().anchoredPosition = new Vector2(button1Rect.anchoredPosition.x + button1Rect.sizeDelta.x - 25,
                    sepLine.GetComponent<RectTransform>().anchoredPosition.y);
            }
            catch (Exception e)
            {
                Console.WriteLine($"exception in sep line {e.Message}");
            }
        }


        public void Unload()
        {
            try
            {
                if (CheckboxText != null) 
                    Destroy(CheckboxText.gameObject);
                Destroy(DrawEquatorCheck.gameObject);
                Destroy(spriteChecked);
                Destroy(spriteUnChecked);
                if (_iconImage != null)
                    Destroy(_iconImage.gameObject);
                if (CheckBoxImage != null)
                {
                    Destroy(CheckBoxImage.gameObject);
                }

                if (newSeparator != null)
                {
                    Destroy(newSeparator.gameObject);
                }

                if (BulldozeButton != null)
                {
                    Destroy(BulldozeButton);
                }

                if (PaveActionButton != null)
                {
                    Destroy(PaveActionButton.gameObject);
                }

                if (CountTransform != null)
                {
                    Destroy(CountTransform.gameObject);
                }

                if (_bulldozeIcon != null)
                {
                    Destroy(_bulldozeIcon.gameObject);
                }
                if (countText != null)
                    Destroy(countText.gameObject);
            }
            catch (Exception e)
            {
                Console.WriteLine($"failed to do unload {e.Message}");
                Console.WriteLine(e.StackTrace);
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
                _iconImage = _bulldozeIcon.GetComponentInChildren<Image>();
                if (_iconImage != null)
                {
                    _iconImage.sprite = newIcon;
                }
            }

            var buttonHotkeyText = copiedRectTransform.transform.Find("text");
            Console.WriteLine($"found button text {buttonHotkeyText}");
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
                copiedUiButton.tips.itemId = 0;
                copiedUiButton.transitions = new UIButton.Transition[] { };
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
        }

        public void Hide()
        {
            BulldozeButton.gameObject.SetActive(false);
            PaveActionButton.gameObject.SetActive(false);
            CheckBoxImage.gameObject.SetActive(false);
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