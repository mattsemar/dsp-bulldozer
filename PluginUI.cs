using System;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Bulldozer
{
    public class UIElements : MonoBehaviour
    {
        public GameObject DrawEquatorCheck;
        public bool DrawEquatorField = true;

        public GameObject CheckboxText;

        public Image CheckBoxImage;

        public Sprite spriteChecked;
        public Sprite spriteUnChecked;
        private GameObject newSeparator;


        public void AddDrawEquatorCheckbox(RectTransform environmentModificationContainer, GameObject button1)
        {
            InitOnOffSprites();

            InitDrawEquatorCheckbox(environmentModificationContainer, button1);
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

            Console.WriteLine("initialized checkbox rect");

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

            Console.WriteLine("initialized button");

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

            countText = null;
            BulldozeButton = CopyButton(button1.GetComponent<RectTransform>(), Vector2.right * (5 + button1.GetComponent<RectTransform>().sizeDelta.x), out countText,
                Helper.GetSprite("bulldoze"));
            Console.WriteLine($"returned text {countText?.text}");
            var newUiButton = BulldozeButton.GetComponent<UIButton>();
            if (newUiButton != null)
            {
                newUiButton.data = 44;
                 
            }

            CheckboxText = new GameObject("drawEquatorLineCheckboxText");
            RectTransform rectTxt = CheckboxText.AddComponent<RectTransform>();
            
            rectTxt.SetParent(environmentModificationContainer.transform.parent, false);
            
            rectTxt.anchorMax = new Vector2(0, 0.5f);
            rectTxt.anchorMin = new Vector2(0, 0.5f);
            rectTxt.sizeDelta = new Vector2(100, 20);
            rectTxt.pivot = new Vector2(0, 0.5f);
            rectTxt.anchoredPosition = new Vector2(20, 0);
            Text text = rectTxt.gameObject.AddComponent<Text>();
            text.text = "Display /sec";
         
        }

        public RectTransform BulldozeButton;
        public Text countText;
        public Text buttonHotkeyTextComponent;

        public void Unload()
        {
            try
            {
                Destroy(CheckboxText.gameObject);
                Destroy(DrawEquatorCheck.gameObject);
                Destroy(spriteChecked);
                Destroy(spriteUnChecked);
                if (newSeparator != null)
                {
                    Destroy(newSeparator.gameObject);
                }

                if (BulldozeButton != null)
                {
                    Destroy(BulldozeButton.gameObject);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"failed to do unload {e.Message}");
                Console.WriteLine(e.StackTrace);
            }
        }

        public RectTransform CopyButton(RectTransform rectTransform, Vector2 positionDelta, out Text countComponent, Sprite newIcon)
        {
            var copied = Instantiate(rectTransform, rectTransform.transform.parent, false);
            var copiedRectTransform = copied.GetComponent<RectTransform>();
            var originalRectTransform = rectTransform.GetComponent<RectTransform>();

            copiedRectTransform.anchorMin = originalRectTransform.anchorMin;
            copiedRectTransform.anchorMax = originalRectTransform.anchorMax;
            copiedRectTransform.sizeDelta = originalRectTransform.sizeDelta;
            copiedRectTransform.anchoredPosition = originalRectTransform.anchoredPosition + positionDelta;


            var icon = copiedRectTransform.transform.Find("icon");
            Console.WriteLine($"found icon {icon}");
            if (icon != null)
            {
                var image = icon.GetComponentInChildren<Image>();
                if (image != null)
                {
                    image.sprite = newIcon;
                }
            }

            var count = copiedRectTransform.transform.Find("count");
            Console.WriteLine($"found count {count}");
            if (count != null)
            {
                countComponent = count.GetComponentInChildren<Text>();
                if (countComponent != null)
                {
                    countComponent.text = "100";
                    return copied;
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

            // var componentInChildren = copiedRectTransform.GetComponentInChildren<UIButton>();
            // if (componentInChildren != null)
            // {
            //     Destroy(componentInChildren);
            // }

            countComponent = null;
            return copied;
        }
    }

    public class ActionButton
    {
        public Action<UIButton> Action;
        public string Name;
        public string TipText;
        public string TipTitle;
        public RectTransform TriggerButton;
        public UIButton uiButton;
        public int YOffset;

        public ActionButton(string name, string tipTitle,
            string tipText, int offset,
            Action<UIButton> action, ref Image progressImage)
        {
            Name = $"{name}-trigger-button";
            TipTitle = tipTitle;
            TipText = tipText;
            YOffset = offset;
            Action = action;
            var parent = GameObject.Find("Game Menu").GetComponent<RectTransform>();
            var prefab = GameObject.Find("Game Menu/button-1-bg").GetComponent<RectTransform>();
            var referencePosition = prefab.localPosition;
            TriggerButton = Object.Instantiate(prefab, parent, true);

            TriggerButton.gameObject.name = Name;

            uiButton = TriggerButton.GetComponent<UIButton>();
            uiButton.tips.tipTitle = tipTitle;
            uiButton.tips.tipText = tipText;
            uiButton.tips.delay = 0f;
            TriggerButton.transform.Find("button-1/icon").GetComponent<Image>().sprite =
                Helper.GetSprite($"{name}-trigger-icon");
            TriggerButton.localScale = new Vector3(0.35f, 0.35f, 0.35f);
            TriggerButton.localPosition = new Vector3(referencePosition.x + 155f, referencePosition.y + offset,
                referencePosition.z);
            uiButton.OnPointerDown(null);
            uiButton.OnPointerEnter(null);

            void UnityAction()
            {
                try
                {
                    Console.WriteLine($"[START] performing action for button {name}");
                    action(uiButton);
                    Console.WriteLine($"[END] performing action for button {name}");
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Got exception {e.Message} trying to do handler for {name}. {e.StackTrace}");
                }
            }

            uiButton.button.onClick.AddListener(UnityAction);

            var prefabProgress = GameObject.Find("tech-progress").GetComponent<Image>();
            progressImage = Object.Instantiate(prefabProgress, parent, true);
            progressImage.gameObject.name = $"{name}-trigger-image";
            progressImage.fillAmount = 0;
            progressImage.type = Image.Type.Filled;
            progressImage.rectTransform.localScale = new Vector3(3.0f, 3.0f, 3.0f);
            progressImage.rectTransform.localPosition = new Vector3(referencePosition.x + 155.5f,
                referencePosition.y + YOffset, referencePosition.z);

            // Switch from circle-thin to round-50px-border
            var sprite = Resources.Load<Sprite>("UI/Textures/Sprites/round-50px-border");
            progressImage.sprite = Object.Instantiate(sprite);
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