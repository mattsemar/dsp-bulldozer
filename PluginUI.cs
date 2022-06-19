using System;
using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Bulldozer
{
    public class UIElements : MonoBehaviour
    {
        private const string DEFAULT_TIP_MESSAGE_PT1 =
            "Adds foundation to entire planet. Any existing foundation decoration will be lost.\n";

        private const string DEFAULT_TIP_MESSAGE_LAT_PT1 =
            "Adds foundation to all locations in selected latitude range.\n";

        private const string DEFAULT_TIP_MESSAGE_VEINS_NO_ALTER = "Veins will not be raised or lowered";

        private const string DEFAULT_TIP_MESSAGE_DESTROY_FACTORY =
            "Destroy all factory machines";

        private const string DEFAULT_TIP_MESSAGE_DESTROY_FACTORY_IN_LAT =
            "Destroy all factory machines in selected latitude range";

        public static ManualLogSource logger;
        private static List<GameObject> gameObjectsToDestroy = new();

        public GameObject DrawEquatorCheck;
        public Sprite spriteChecked;
        public Sprite spriteUnChecked;

        public UIButton PaveActionButton;
        public RectTransform BulldozeButton;
        public Text countText;
        public Text buttonHotkeyTextComponent;
        public Transform CountTransform;
        public CheckboxControl drawEquatorCheckboxButton;
        private bool _alterVeinsField;
        private Transform _bulldozeIcon;
        private bool _destroyFactoryMachines;

        // use to track whether checkbox needs to be synced
        private bool _drawEquatorField = true;
        private Image _iconImage;

        private bool _techUnlocked;
        private bool _readyToGo;
        [NonSerialized] public Image AlterVeinsCheckBoxImage;
        [NonSerialized] public Image CheckBoxImage;
        [NonSerialized] public Image ConfigIconImage;

        private bool currentTipMessageIsDefault = true;
        [NonSerialized] public Image DestroyMachinesCheckBoxImage;
        private Text hover;
        private UIButton mainActionButton;
        public int initPercent;
        private RectTransform buttonOneRectTransform;
        private GameObject AlterVeinsCheck;
        private GameObject DestroyMachinesCheck;
        private GameObject ConfigButton;

        public bool TechUnlockedState
        {
            get => _techUnlocked;
            set
            {
                _techUnlocked = value;
                if (!_techUnlocked)
                {
                    mainActionButton.button.interactable = false;
                    mainActionButton.tips.tipText = "Research Universe Exploration 3 to Unlock";
                }
                else
                {
                    mainActionButton.button.interactable = true;
                    mainActionButton.tips.tipText = ConstructTipMessageDependentOnConfig();
                }
            }
        }

        public bool ReadyForAction
        {
            set
            {
                _readyToGo = value;
                if (_techUnlocked && !_readyToGo)
                {
                    mainActionButton.button.interactable = false;
                    mainActionButton.tips.tipText = $"Initializing ({initPercent}% complete)";
                }
                else if (_readyToGo && _techUnlocked)
                {
                    mainActionButton.button.interactable = true;
                    mainActionButton.tips.tipText = ConstructTipMessageDependentOnConfig();
                }
            }
        }

        public void Update()
        {
            if (_drawEquatorField != PluginConfig.addGuideLines.Value)
            {
                // value might have been updated in config manager plugin ui
                CheckBoxImage.sprite = PluginConfig.addGuideLines.Value ? spriteChecked : spriteUnChecked;
                _drawEquatorField = PluginConfig.addGuideLines.Value;
            }

            if (_alterVeinsField != PluginConfig.alterVeinState.Value)
            {
                // sync checkbox with externally changed value
                AlterVeinsCheckBoxImage.sprite = PluginConfig.alterVeinState.Value ? spriteChecked : spriteUnChecked;
                _alterVeinsField = PluginConfig.alterVeinState.Value;
            }

            if (_destroyFactoryMachines != PluginConfig.destroyFactoryAssemblers.Value)
            {
                // sync checkbox with externally changed value
                DestroyMachinesCheckBoxImage.sprite = PluginConfig.destroyFactoryAssemblers.Value ? spriteChecked : spriteUnChecked;
                _destroyFactoryMachines = PluginConfig.destroyFactoryAssemblers.Value;
            }

            if (!_techUnlocked && PluginConfig.disableTechRequirement.Value)
            {
                TechUnlockedState = true;
            }

            if (_destroyFactoryMachines)
            {
                if (mainActionButton != null && currentTipMessageIsDefault)
                {
                    currentTipMessageIsDefault = false;
                    if (PluginConfig.IsLatConstrained())
                    {
                        mainActionButton.tips.tipText = DEFAULT_TIP_MESSAGE_DESTROY_FACTORY_IN_LAT;
                    }
                    else
                    {
                        mainActionButton.tips.tipText = DEFAULT_TIP_MESSAGE_DESTROY_FACTORY;
                    }
                }
            }
            else if (mainActionButton != null && !currentTipMessageIsDefault)
            {
                currentTipMessageIsDefault = true;
                mainActionButton.tips.tipText = ConstructTipMessageDependentOnConfig();
            }
        }

        private string ConstructTipMessageDependentOnConfig()
        {
            var veinsAlterMessage = $"Will attempt to {PluginConfig.GetCurrentVeinsRaiseState()} all veins for planet.";
            if (PluginConfig.IsLatConstrained())
            {
                veinsAlterMessage = $"Will attempt to {PluginConfig.GetCurrentVeinsRaiseState()} veins in selected latitude range ({PluginConfig.GetLatRangeString()})";
            }

            var part2 = PluginConfig.alterVeinState.Value ? veinsAlterMessage : DEFAULT_TIP_MESSAGE_VEINS_NO_ALTER;
            currentTipMessageIsDefault = !PluginConfig.alterVeinState.Value;
            if (PluginConfig.IsLatConstrained())
            {
                return DEFAULT_TIP_MESSAGE_LAT_PT1 + part2;
            }

            return DEFAULT_TIP_MESSAGE_PT1 + part2;
        }

        private static void ResetButtonPos(RectTransform rectTransform)
        {
            var posStr = $"{rectTransform.anchoredPosition.x},{rectTransform.anchoredPosition.y}";
            if (PluginConfig.originalReformButtonPosition.Value == "0,0")
            {
                PluginConfig.originalReformButtonPosition.Value = posStr;
            }
            else if (posStr != PluginConfig.originalReformButtonPosition.Value)
            {
                var parts = PluginConfig.originalReformButtonPosition.Value.Split(',');
                try
                {
                    if (float.TryParse(parts[0].Trim(), out var resultx))
                    {
                        if (float.TryParse(parts[1].Trim(), out var resulty))
                        {
                            Log.Debug($"Setting button back to original {PluginConfig.originalReformButtonPosition.Value} {resultx}, {resulty}");
                            rectTransform.anchoredPosition = new Vector2(resultx, resulty);
                        }
                        else
                        {
                            Log.Debug($"Failed to parse yvalue {parts[1]}");
                        }
                    }
                    else
                    {
                        Log.Debug($"Failed to parse xvalue {parts[0]}");
                    }
                }
                catch (Exception e)
                {
                    // ignored
                }
            }
        }


        public void AddBulldozeComponents(RectTransform environmentModificationContainer, UIBuildMenu uiBuildMenu, GameObject foundationButton,
            GameObject reformAllButton, Action<int> action)
        {
            InitOnOffSprites();
            InitActionButton(foundationButton, action);
            InitDrawEquatorCheckbox(environmentModificationContainer, foundationButton);
            InitAlterVeinsCheckbox(environmentModificationContainer);
            InitDestroyMachinesCheckbox(environmentModificationContainer);
            InitConfigButton(environmentModificationContainer);
        }

        private void InitActionButton(GameObject buttonToCopy, Action<int> action)
        {
            buttonOneRectTransform = buttonToCopy.gameObject.GetComponent<RectTransform>();
            countText = null;
            ResetButtonPos(buttonOneRectTransform);
             
            if (GameMain.sandboxToolsEnabled)
                buttonOneRectTransform.anchoredPosition = new Vector2((float)(buttonOneRectTransform.anchoredPosition.x - buttonOneRectTransform.sizeDelta.x / 1.5), buttonOneRectTransform.anchoredPosition.y);
            BulldozeButton = CopyButton(buttonOneRectTransform, Vector2.right * (buttonOneRectTransform.sizeDelta.x), out countText,
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
            rect.anchoredPosition = new Vector2(GetCheckBoxXValue(), -90);
            drawEquatorCheckboxButton = rect.gameObject.AddComponent<CheckboxControl>();
            drawEquatorCheckboxButton.HoverText = "Add painted guidelines at various locations (equator, tropics, meridians configurable)";
            drawEquatorCheckboxButton.onClick += OnDrawEquatorCheckClick;
            gameObjectsToDestroy.Add(drawEquatorCheckboxButton.gameObject);

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
        }

        private void InitAlterVeinsCheckbox(RectTransform environmentModificationContainer)
        {
            AlterVeinsCheck = new GameObject("Repave");
            gameObjectsToDestroy.Add(AlterVeinsCheck);
            RectTransform rect = AlterVeinsCheck.AddComponent<RectTransform>();
            rect.SetParent(environmentModificationContainer.transform, false);

            rect.anchorMax = new Vector2(0, 1);
            rect.anchorMin = new Vector2(0, 1);
            rect.sizeDelta = new Vector2(20, 20);
            rect.pivot = new Vector2(0, 0.5f);
            rect.anchoredPosition = new Vector2(GetCheckBoxXValue(), -105);
            var repaveCheckboxButton = rect.gameObject.AddComponent<CheckboxControl>();
            repaveCheckboxButton.HoverText = "Check to attempt to raise or lower all veins. No foundation will be placed";

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

            AlterVeinsCheckBoxImage = repaveCheckboxButton.gameObject.AddComponent<Image>();
            AlterVeinsCheckBoxImage.color = new Color(0.8f, 0.8f, 0.8f, 1);
            gameObjectsToDestroy.Add(AlterVeinsCheckBoxImage.gameObject);

            AlterVeinsCheckBoxImage.sprite = PluginConfig.alterVeinState.Value ? spriteChecked : spriteUnChecked;
            repaveCheckboxButton.onClick += OnAlterVeinCheckClick;
        }

        private void InitDestroyMachinesCheckbox(RectTransform environmentModificationContainer)
        {
            DestroyMachinesCheck = new GameObject("DestroyMachines");
            gameObjectsToDestroy.Add(DestroyMachinesCheck);
            RectTransform rect = DestroyMachinesCheck.AddComponent<RectTransform>();
            rect.SetParent(environmentModificationContainer.transform, false);

            rect.anchorMax = new Vector2(0, 1);
            rect.anchorMin = new Vector2(0, 1);
            rect.sizeDelta = new Vector2(20, 20);
            rect.pivot = new Vector2(0, 0.5f);
            rect.anchoredPosition = new Vector2(GetCheckBoxXValue(), -120);
            var destroyMachinesButton = rect.gameObject.AddComponent<CheckboxControl>();
            destroyMachinesButton.HoverText = "Clear all factory machines. Skip adding foundation.";

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
            destroyMachinesButton.onClick += OnDestroyMachinesCheckClick;
        }

        private float GetCheckBoxXValue()
        {
            if (BulldozeButton != null && GameMain.sandboxToolsEnabled)
            {
                return 310;
            }

            return 350;
        }

        private void InitConfigButton(RectTransform environmentModificationContainer)
        {
            ConfigButton = new GameObject("Config");
            gameObjectsToDestroy.Add(ConfigButton);
            var rect = ConfigButton.AddComponent<RectTransform>();
            rect.SetParent(environmentModificationContainer.transform, false);

            rect.anchorMax = new Vector2(0, 1);
            rect.anchorMin = new Vector2(0, 1);
            rect.sizeDelta = new Vector2(20, 20);
            rect.pivot = new Vector2(0, 0.5f);
            rect.anchoredPosition = new Vector2(GetCheckBoxXValue() + 18, -120);
            var invokeConfig = rect.gameObject.AddComponent<CheckboxControl>();
            invokeConfig.HoverText = "Open config";

            if (countText != null)
            {
                var configHover = Instantiate(countText, environmentModificationContainer.transform, true);
                gameObjectsToDestroy.Add(configHover.gameObject);
                var copiedRectTransform = configHover.GetComponent<RectTransform>();
                var parentRect = environmentModificationContainer.GetComponent<RectTransform>();
                copiedRectTransform.anchorMin = new Vector2(0, 1);
                copiedRectTransform.anchorMax = new Vector2(0, 1);
                copiedRectTransform.sizeDelta = new Vector2(800, 20);
                copiedRectTransform.anchoredPosition = new Vector2(400, parentRect.transform.position.y - 115);
                invokeConfig.textObject = configHover;
            }

            gameObjectsToDestroy.Add(invokeConfig.gameObject);

            ConfigIconImage = invokeConfig.gameObject.AddComponent<Image>();
            ConfigIconImage.color = new Color(0.8f, 0.8f, 0.8f, 1);
            gameObjectsToDestroy.Add(ConfigIconImage.gameObject);
            var configImgGameObject = GameObject.Find("UI Root/Overlay Canvas/In Game/Game Menu/button-3-bg/button-3/icon");

            ConfigIconImage.sprite = configImgGameObject.GetComponent<Image>().sprite;
            invokeConfig.onClick += data => { PluginConfigWindow.visible = !PluginConfigWindow.visible; };
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
            copiedRectTransform.anchorMin = rectTransform.anchorMin;
            copiedRectTransform.anchorMax = rectTransform.anchorMax;
            copiedRectTransform.sizeDelta = rectTransform.sizeDelta;
            copiedRectTransform.anchoredPosition = rectTransform.anchoredPosition + positionDelta;


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
                mainActionButton.tips.tipTitle = "Bulldoze";
                mainActionButton.tips.tipText = ConstructTipMessageDependentOnConfig();
                mainActionButton.tips.offset = new Vector2(mainActionButton.tips.offset.x, mainActionButton.tips.offset.y + 100);
                mainActionButton.button.onClick.RemoveAllListeners();

                // mainActionButton.onClick += action;
                mainActionButton.button.onClick.AddListener(delegate { action(1); });
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

        public void Show(bool inittedThisTime = false)
        {
            if (GameMain.sandboxToolsEnabled)
            {
                if (!inittedThisTime)
                    buttonOneRectTransform.anchoredPosition = new Vector2((float)(buttonOneRectTransform.anchoredPosition.x - buttonOneRectTransform.sizeDelta.x / 1.5),
                        buttonOneRectTransform.anchoredPosition.y);
                RepositionElements();
            }
            else if (buttonOneRectTransform != null)
            {
                ResetButtonPos(buttonOneRectTransform);
                BulldozeButton.anchoredPosition = buttonOneRectTransform.anchoredPosition + Vector2.right * (5 + buttonOneRectTransform.GetComponent<RectTransform>().sizeDelta.x);
                RepositionElements();
            }

            BulldozeButton.gameObject.SetActive(true);
            PaveActionButton.gameObject.SetActive(true);
            CheckBoxImage.gameObject.SetActive(true);
            if (AlterVeinsCheckBoxImage.gameObject != null)
            {
                AlterVeinsCheckBoxImage.gameObject.SetActive(true);
            }

            if (DestroyMachinesCheckBoxImage.gameObject != null)
                DestroyMachinesCheckBoxImage.gameObject.SetActive(true);
            ConfigIconImage.gameObject.SetActive(true);
        }

        private void RepositionElements()
        {
            BulldozeButton.anchoredPosition = buttonOneRectTransform.anchoredPosition + Vector2.right * (5 + buttonOneRectTransform.GetComponent<RectTransform>().sizeDelta.x);
            {
                RectTransform drawEquatorRect = DrawEquatorCheck.GetComponent<RectTransform>();
                drawEquatorRect.anchoredPosition = new Vector2(GetCheckBoxXValue(), drawEquatorRect.anchoredPosition.y);
            }
            {
                RectTransform rect = AlterVeinsCheck.GetComponent<RectTransform>();
                rect.anchoredPosition = new Vector2(GetCheckBoxXValue(), -105);
            }
            {
                RectTransform rect = DestroyMachinesCheck.GetComponent<RectTransform>();
                rect.anchoredPosition = new Vector2(GetCheckBoxXValue(), -120);
            }
            {
                RectTransform rect = ConfigButton.GetComponent<RectTransform>();
                rect.anchoredPosition = new Vector2(GetCheckBoxXValue() + 19, -120);
            }
        }

        public void Hide()
        {
            if (GameMain.sandboxToolsEnabled && buttonOneRectTransform != null)
                ResetButtonPos(buttonOneRectTransform);
            BulldozeButton.gameObject.SetActive(false);
            PaveActionButton.gameObject.SetActive(false);
            CheckBoxImage.gameObject.SetActive(false);
            AlterVeinsCheckBoxImage.gameObject.SetActive(false);
            DestroyMachinesCheckBoxImage.gameObject.SetActive(false);
            ConfigIconImage.gameObject.SetActive(false);
        }

        public bool IsShowing()
        {
            if (BulldozeButton == null || BulldozeButton.gameObject == false)
                return false;
            return BulldozeButton.gameObject.activeSelf;
        }

        private void OnDrawEquatorCheckClick(PointerEventData data)
        {
            OnDrawEquatorCheckClickImpl();
        }

        private void OnDrawEquatorCheckClickImpl(bool explicitDisable = false)
        {
            if (explicitDisable)
                PluginConfig.addGuideLines.Value = false;
            else
                PluginConfig.addGuideLines.Value = !PluginConfig.addGuideLines.Value;
            CheckBoxImage.sprite = PluginConfig.addGuideLines.Value ? spriteChecked : spriteUnChecked;
            _drawEquatorField = PluginConfig.addGuideLines.Value;
            if (PluginConfig.addGuideLines.Value)
            {
                if (PluginConfig.alterVeinState.Value)
                {
                    OnAlterVeinCheckClickImpl(true);
                }

                if (PluginConfig.destroyFactoryAssemblers.Value)
                {
                    OnDestroyMachinesCheckClickImpl(true);
                }
            }
        }

        private void OnAlterVeinCheckClick(PointerEventData obj)
        {
            OnAlterVeinCheckClickImpl();
        }

        private void OnAlterVeinCheckClickImpl(bool explicitDisable = false)
        {
            if (explicitDisable)
                PluginConfig.alterVeinState.Value = false;
            else
                PluginConfig.alterVeinState.Value = !PluginConfig.alterVeinState.Value;
            AlterVeinsCheckBoxImage.sprite = PluginConfig.alterVeinState.Value ? spriteChecked : spriteUnChecked;
            _alterVeinsField = PluginConfig.alterVeinState.Value;
            // turn off the other check if this is clicked since it can be confusing
            if (PluginConfig.alterVeinState.Value)
            {
                if (PluginConfig.addGuideLines.Value)
                {
                    OnDrawEquatorCheckClickImpl(true);
                }

                if (PluginConfig.destroyFactoryAssemblers.Value)
                {
                    OnDestroyMachinesCheckClickImpl(true);
                }
            }
        }

        private void OnDestroyMachinesCheckClick(PointerEventData obj)
        {
            OnDestroyMachinesCheckClickImpl();
        }

        private void OnDestroyMachinesCheckClickImpl(bool explicitDisable = false)
        {
            if (explicitDisable)
                PluginConfig.destroyFactoryAssemblers.Value = false;
            else
                PluginConfig.destroyFactoryAssemblers.Value = !PluginConfig.destroyFactoryAssemblers.Value;

            DestroyMachinesCheckBoxImage.sprite = PluginConfig.destroyFactoryAssemblers.Value ? spriteChecked : spriteUnChecked;
            _destroyFactoryMachines = PluginConfig.destroyFactoryAssemblers.Value;
            if (PluginConfig.destroyFactoryAssemblers.Value)
            {
                // disable others
                if (PluginConfig.addGuideLines.Value)
                {
                    OnDrawEquatorCheckClickImpl(true);
                }

                if (PluginConfig.alterVeinState.Value)
                {
                    OnAlterVeinCheckClickImpl(true);
                }
            }
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