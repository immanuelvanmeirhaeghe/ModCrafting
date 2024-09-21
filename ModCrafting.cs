using Enums;
using ModCrafting.Data.Enums;
using ModCrafting.Data.Interfaces;
using ModCrafting.Data.Modding;
using ModCrafting.Managers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using UnityEngine;
using UnityEngine.UI;

namespace ModCrafting
{

    /// <summary>
    /// ModCrafting is a mod for Green Hell, that allows a player
    ///  to craft any game item without the needed materials and
    ///  to destroy any selected item within player range or pointed at with the mouse.
    /// Press Keypad9 (default) or the key configurable in ModAPI to open the mod screen.
    /// When enabled, press KeypadMinus (default) or the key configurable in ModAPI to delete mouse target.
    /// </summary>
    public class ModCrafting : MonoBehaviour, IYesNoDialogOwner
    {
        private static ModCrafting Instance;
        private static readonly string ModName = nameof(ModCrafting);
        private static readonly string RuntimeConfigurationFile = Path.Combine(Application.dataPath.Replace("GH_Data", "Mods"), "RuntimeConfiguration.xml");
        public string ModCraftingScreenTitle = $"{ModName} created by [Dragon Legion] Immaanuel#4300";

        public ModCrafting()
        {
            useGUILayout = true;
            Instance = this;
        }

        public static ModCrafting Get()
        {
            return Instance;
        }

        private static float ModCraftingScreenTotalWidth { get; set; } = 700f;
        private static float ModCraftingScreenTotalHeight { get; set; } = 500f;
        private static float ModCraftingScreenMinWidth { get; set; } = 700f;
        private static float ModCraftingScreenMinHeight { get; set; } = 50f;
        private static float ModCraftingScreenMaxWidth { get; set; } = Screen.width;
        private static float ModCraftingScreenMaxHeight { get; set; } = Screen.height;
        private static float ModCraftingScreenStartPositionX { get; set; } = Screen.width / 2f;
        private static float ModCraftingScreenStartPositionY { get; set; } = Screen.height / 2f;
        private static bool IsModCraftingScreenMinimized { get; set; } = false;
        private static int ModCraftingScreenId { get; set; }
        private bool ShowModCraftingScreen { get; set; } = false;
        private bool ShowModCraftingInfo { get; set; } = false;

        public IConfigurableMod SelectedMod { get; set; } = default;
        public Vector2 ModCraftingInfoScrollViewPosition { get; set; } = default;

        public static Rect ModCraftingScreen = new Rect(ModCraftingScreenStartPositionX, ModCraftingScreenStartPositionY, ModCraftingScreenTotalWidth, ModCraftingScreenTotalHeight);

        private static ItemsManager LocalItemsManager;
        private static ConstructionController LocalConstructionController;
        private static HUDManager LocalHUDManager;
        private static Player LocalPlayer;
        private static InventoryBackpack LocalInventoryBackpack;
        private static StylingManager LocalStylingManager;

        public string SearchItemKeyWord = string.Empty;
        public Vector2 FilteredItemsScrollViewPosition;
        public string SelectedItemToCraftItemName;
        public int SelectedItemToCraftIndex;
        public ItemID SelectedItemToCraftItemID;
        public Item SelectedItemToCraft;
        public GameObject SelectedGameObjectToDestroy = null;
        public List<GameObject> ItemsInPlayerActionRange;
        public Vector2 ItemsInRangeScrollViewPosition { get; private set; }
        public int SelectedGameObjectToDestroyIndex { get; private set; }
        public string SelectedGameObjectToDestroyName = string.Empty;
        
        public Item SelectedItemToDestroy = null;
        public string SelectedFilterName;
        public int SelectedFilterIndex;
        public ItemFilter SelectedFilter = ItemFilter.All;
        public string ItemCountToCraft = "1";
        public bool ShouldAddToBackpackOption = true;
        public List<Item> CraftedItems = new List<Item>();

        public bool DestroyTargetOption { get; set; } = false;

        public bool IsModActiveForMultiplayer { get; private set; } = false;
        public bool IsModActiveForSingleplayer => ReplTools.AmIMaster();

        public string ItemDestroyedMessage(string item)
            => $"{item} destroyed!";
        public string ItemNotDestroyedMessage(string item)
            => $"{item} cannot be destroyed!";
        public string ItemNotSelectedMessage()
            => $"Not any item selected to destroy!";
        public string ItemNotCraftedMessage()
            => $"Item could not be crafted!";
        public string ItemCraftedMessage(string item, int count)
            => $"{count} x {item} crafted!";

        public string OnlyForSinglePlayerOrHostMessage()
            => $"Only available for single player or when host. Host can activate using ModManager.";
        public string PermissionChangedMessage(string permission, string reason)
            => $"Permission to use mods and cheats in multiplayer was {permission} because {reason}.";
        private string HUDBigInfoMessage(string message, MessageType messageType, Color? headcolor = null)
            => $"<color=#{(headcolor != null ? ColorUtility.ToHtmlStringRGBA(headcolor.Value) : ColorUtility.ToHtmlStringRGBA(Color.red))}>{messageType}</color>\n{message}";
        private void OnlyForSingleplayerOrWhenHostBox()
        {
            using (new GUILayout.HorizontalScope(GUI.skin.box))
            {
                GUILayout.Label(OnlyForSinglePlayerOrHostMessage(), LocalStylingManager.ColoredCommentLabel(Color.yellow));
            }
        }

        public KeyCode ShortcutKey { get; set; } = KeyCode.Keypad9;
        public KeyCode DeleteShortcutKey { get; set; } = KeyCode.KeypadMinus;
        public bool AlsoDestroyCraftedItems { get; private set; }

        private void ModManager_onPermissionValueChanged(bool optionValue)
        {
            string reason = optionValue ? "the game host allowed usage" : "the game host did not allow usage";
            IsModActiveForMultiplayer = optionValue;

            ShowHUDBigInfo(
                          (optionValue ?
                            HUDBigInfoMessage(PermissionChangedMessage($"granted", $"{reason}"), MessageType.Info, LocalStylingManager.DefaultEnabledColor)
                            : HUDBigInfoMessage(PermissionChangedMessage($"revoked", $"{reason}"), MessageType.Info, Color.yellow))
                            );
        }

        private void ShowHUDBigInfo(string text)
        {
            string header = $"{ModName} Info";
            string textureName = HUDInfoLogTextureType.Count.ToString();

            HUDBigInfo bigInfo = (HUDBigInfo)LocalHUDManager.GetHUD(typeof(HUDBigInfo));
            HUDBigInfoData.s_Duration = 2f;
            HUDBigInfoData bigInfoData = new HUDBigInfoData
            {
                m_Header = header,
                m_Text = text,
                m_TextureName = textureName,
                m_ShowTime = Time.time
            };
            bigInfo.AddInfo(bigInfoData);
            bigInfo.Show(true);
        }

        public void ShowHUDInfoLog(string itemID, string localizedTextKey)
        {
            Localization localization = GreenHellGame.Instance.GetLocalization();
            var messages = ((HUDMessages)LocalHUDManager.GetHUD(typeof(HUDMessages)));
            messages.AddMessage($"{localization.Get(localizedTextKey)}  {localization.Get(itemID)}");
        }

        public KeyCode GetShortcutKey(string buttonID)
        {
            var ConfigurableModList = GetModList();
            if (ConfigurableModList != null && ConfigurableModList.Count > 0)
            {
                SelectedMod = ConfigurableModList.Find(cfgMod => cfgMod.ID == ModName);
                return SelectedMod.ConfigurableModButtons.Find(cfgButton => cfgButton.ID == buttonID).ShortcutKey;
            }
            else
            {
                return KeyCode.Keypad8;
            }
        }

        private List<IConfigurableMod> GetModList()
        {
            List<IConfigurableMod> modList = new List<IConfigurableMod>();
            try
            {
                if (File.Exists(RuntimeConfigurationFile))
                {
                    using (XmlReader configFileReader = XmlReader.Create(new StreamReader(RuntimeConfigurationFile)))
                    {
                        while (configFileReader.Read())
                        {
                            configFileReader.ReadToFollowing("Mod");
                            do
                            {
                                string gameID = GameID.GreenHell.ToString();
                                string modID = configFileReader.GetAttribute(nameof(IConfigurableMod.ID));
                                string uniqueID = configFileReader.GetAttribute(nameof(IConfigurableMod.UniqueID));
                                string version = configFileReader.GetAttribute(nameof(IConfigurableMod.Version));

                                var configurableMod = new ConfigurableMod(gameID, modID, uniqueID, version);

                                configFileReader.ReadToDescendant("Button");
                                do
                                {
                                    string buttonID = configFileReader.GetAttribute(nameof(IConfigurableModButton.ID));
                                    string buttonKeyBinding = configFileReader.ReadElementContentAsString();

                                    configurableMod.AddConfigurableModButton(buttonID, buttonKeyBinding);

                                } while (configFileReader.ReadToNextSibling("Button"));

                                if (!modList.Contains(configurableMod))
                                {
                                    modList.Add(configurableMod);
                                }

                            } while (configFileReader.ReadToNextSibling("Mod"));
                        }
                    }
                }
                return modList;
            }
            catch (Exception exc)
            {
                HandleException(exc, nameof(GetModList));
                modList = new List<IConfigurableMod>();
                return modList;
            }
        }

        protected virtual void Awake()
        {
            Instance = this;
        }

        protected virtual void OnDestroy()
        {
            Instance = null;
        }

        protected virtual void Start()
        {
            ModManager.ModManager.onPermissionValueChanged += ModManager_onPermissionValueChanged;
            InitData();
            ShortcutKey = GetShortcutKey(nameof(ShortcutKey));
            DeleteShortcutKey = GetShortcutKey(nameof(DeleteShortcutKey));
        }

        private void HandleException(Exception exc, string methodName)
        {
            string info = $"[{ModName}:{methodName}] throws exception:\n{exc}";
            ModAPI.Log.Write(info);
            ShowHUDBigInfo(HUDBigInfoMessage(exc.Message, MessageType.Error, Color.red));
        }

        protected virtual void Update()
        {
            if (Input.GetKeyDown(ShortcutKey))
            {
                if (!ShowModCraftingScreen)
                {
                    InitData();
                    EnableCursor(true);
                }
                ToggleShowUI(0);
                if (!ShowModCraftingScreen)
                {
                    EnableCursor(false);
                }
            }

            if (Input.GetKeyDown(DeleteShortcutKey))
            {
                DestroyTarget();
            }
        }

        private void ToggleShowUI(int controlId)
        {
            switch (controlId)
            {
                case 0:
                    ShowModCraftingScreen = !ShowModCraftingScreen;
                    return;
                case 3:
                    ShowModCraftingInfo = !ShowModCraftingInfo;
                    return;
                default:
                    ShowModCraftingInfo = !ShowModCraftingInfo;
                    ShowModCraftingScreen = !ShowModCraftingScreen;
                    return;
            }
        }

        protected virtual void EnableCursor(bool blockPlayer = false)
        {
            CursorManager.Get().ShowCursor(blockPlayer, false);

            if (blockPlayer)
            {
                LocalPlayer.BlockMoves();
                LocalPlayer.BlockRotation();
                LocalPlayer.BlockInspection();
            }
            else
            {
                LocalPlayer.UnblockMoves();
                LocalPlayer.UnblockRotation();
                LocalPlayer.UnblockInspection();
            }
        }

        protected virtual void OnGUI()
        {
            if (ShowModCraftingScreen)
            {
                InitData();
                InitSkinUI();
                ShowModCraftingWindow();
            }
        }

        protected virtual void ShowModCraftingWindow()
        {
            ModCraftingScreenId = GetHashCode();            
            ModCraftingScreen = GUILayout.Window(ModCraftingScreenId, ModCraftingScreen, InitModCraftingScreen, ModCraftingScreenTitle,
                GUI.skin.window,
                GUILayout.ExpandWidth(true),
                GUILayout.MinWidth(ModCraftingScreenMinWidth),
                GUILayout.MaxWidth(ModCraftingScreenMaxWidth),
                GUILayout.ExpandHeight(true),
                GUILayout.MinHeight(ModCraftingScreenMinHeight),
                GUILayout.MaxHeight(ModCraftingScreenMaxHeight));
        }

        protected virtual void InitData()
        {
            LocalItemsManager = ItemsManager.Get();
            LocalConstructionController = ConstructionController.Get();
            LocalHUDManager = HUDManager.Get();
            LocalPlayer = Player.Get();
            LocalInventoryBackpack = InventoryBackpack.Get();
            LocalStylingManager = StylingManager.Get();
        }

        protected virtual void InitSkinUI()
        {
            GUI.skin = ModAPI.Interface.Skin;
        }

        protected virtual void InitModCraftingScreen(int windowID)
        {
            ModCraftingScreenStartPositionX = ModCraftingScreen.x;
            ModCraftingScreenStartPositionY = ModCraftingScreen.y;
            ModCraftingScreenTotalWidth = ModCraftingScreen.width;

            using (new GUILayout.VerticalScope(GUI.skin.box))
            {
                ModCraftingScreenMenuBox();

                if (!IsModCraftingScreenMinimized)
                {
                    ModCraftingManagerBox();

                    ConstructionsManagerBox();

                    ItemsFilterBox();
                    
                    DestroyItemsBox();
                    
                    CraftItemsBox();

                }
            }
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 10000f));
        }

        protected virtual void ModCraftingScreenMenuBox()
        {
            string CollapseButtonText = IsModCraftingScreenMinimized ? "O" : "-";
            if (GUI.Button(new Rect(ModCraftingScreen.width - 40f, 0f, 20f, 20f), CollapseButtonText, GUI.skin.button))
            {
                CollapseModCraftingWindow();
            }

            if (GUI.Button(new Rect(ModCraftingScreen.width - 20f, 0f, 20f, 20f), "X", GUI.skin.button))
            {
                CloseWindow();
            }
        }

        protected virtual void CollapseModCraftingWindow()
        {
            if (!IsModCraftingScreenMinimized)
            {
                ModCraftingScreen = new Rect(ModCraftingScreen.x, ModCraftingScreen.y, ModCraftingScreenTotalWidth, ModCraftingScreenMinHeight);
                IsModCraftingScreenMinimized = true;
            }
            else
            {
                ModCraftingScreen = new Rect(ModCraftingScreen.x, ModCraftingScreen.y, ModCraftingScreenTotalWidth, ModCraftingScreenTotalHeight);
                IsModCraftingScreenMinimized = false;
            }
            ShowModCraftingWindow();
        }

        protected virtual void CloseWindow()
        {
            ShowModCraftingScreen = false;
            EnableCursor(false);
        }

        protected virtual void ModCraftingManagerBox()
        {
            if (IsModActiveForSingleplayer || IsModActiveForMultiplayer)
            {
                using (new GUILayout.VerticalScope(GUI.skin.box))
                {
                    GUILayout.Label($"{ModName} Manager", LocalStylingManager.ColoredHeaderLabel(LocalStylingManager.DefaultHeaderColor));
                    GUILayout.Label($"{ModName} Options", LocalStylingManager.ColoredSubHeaderLabel(LocalStylingManager.DefaultHeaderColor));

                    if (GUILayout.Button($"Mod Info", GUI.skin.button))
                    {
                        ToggleShowUI(3);
                    }
                    if (ShowModCraftingInfo)
                    {
                        ModCraftingInfoBox();
                    }

                    MultiplayerOptionBox();

                    ModShortcutsInfoBox();
                }
            }
            else
            {
                OnlyForSingleplayerOrWhenHostBox();
            }
        }

        protected virtual void ModCraftingInfoBox()
        {
            using (new GUILayout.VerticalScope(GUI.skin.box))
            {
                ModCraftingInfoScrollViewPosition = GUILayout.BeginScrollView(ModCraftingInfoScrollViewPosition, GUI.skin.scrollView, GUILayout.MinHeight(150f));

                GUILayout.Label("Mod Info", LocalStylingManager.ColoredSubHeaderLabel(LocalStylingManager.DefaultHighlightColor));

                using (new GUILayout.HorizontalScope(GUI.skin.box))
                {
                    GUILayout.Label($"{nameof(IConfigurableMod.GameID)}:", LocalStylingManager.FormFieldNameLabel);
                    GUILayout.Label($"{SelectedMod.GameID}", LocalStylingManager.FormFieldValueLabel);
                }
                using (new GUILayout.HorizontalScope(GUI.skin.box))
                {
                    GUILayout.Label($"{nameof(IConfigurableMod.ID)}:", LocalStylingManager.FormFieldNameLabel);
                    GUILayout.Label($"{SelectedMod.ID}", LocalStylingManager.FormFieldValueLabel);
                }
                using (new GUILayout.HorizontalScope(GUI.skin.box))
                {
                    GUILayout.Label($"{nameof(IConfigurableMod.UniqueID)}:", LocalStylingManager.FormFieldNameLabel);
                    GUILayout.Label($"{SelectedMod.UniqueID}", LocalStylingManager.FormFieldValueLabel);
                }
                using (new GUILayout.HorizontalScope(GUI.skin.box))
                {
                    GUILayout.Label($"{nameof(IConfigurableMod.Version)}:", LocalStylingManager.FormFieldNameLabel);
                    GUILayout.Label($"{SelectedMod.Version}", LocalStylingManager.FormFieldValueLabel);
                }

                GUILayout.Label("Buttons Info", LocalStylingManager.ColoredSubHeaderLabel(LocalStylingManager.DefaultHighlightColor));

                foreach (var configurableModButton in SelectedMod.ConfigurableModButtons)
                {
                    using (new GUILayout.HorizontalScope(GUI.skin.box))
                    {
                        GUILayout.Label($"{nameof(IConfigurableModButton.ID)}:", LocalStylingManager.FormFieldNameLabel);
                        GUILayout.Label($"{configurableModButton.ID}", LocalStylingManager.FormFieldValueLabel);
                    }
                    using (new GUILayout.HorizontalScope(GUI.skin.box))
                    {
                        GUILayout.Label($"{nameof(IConfigurableModButton.KeyBinding)}:", LocalStylingManager.FormFieldNameLabel);
                        GUILayout.Label($"{configurableModButton.KeyBinding}", LocalStylingManager.FormFieldValueLabel);
                    }
                }

                GUILayout.EndScrollView();
            }
        }

        protected virtual void MultiplayerOptionBox()
        {
            try
            {
                using (new GUILayout.VerticalScope(GUI.skin.box))
                {
                    GUILayout.Label("Multiplayer Options", LocalStylingManager.ColoredSubHeaderLabel(LocalStylingManager.DefaultHeaderColor));

                    string multiplayerOptionMessage = string.Empty;
                    if (IsModActiveForSingleplayer || IsModActiveForMultiplayer)
                    {
                        if (IsModActiveForSingleplayer)
                        {
                            multiplayerOptionMessage = $"you are the game host";
                        }
                        if (IsModActiveForMultiplayer)
                        {
                            multiplayerOptionMessage = $"the game host allowed usage";
                        }
                        GUILayout.Label(PermissionChangedMessage($"granted", multiplayerOptionMessage), LocalStylingManager.ColoredFieldValueLabel(LocalStylingManager.DefaultEnabledColor));
                    }
                    else
                    {
                        if (!IsModActiveForSingleplayer)
                        {
                            multiplayerOptionMessage = $"you are not the game host";
                        }
                        if (!IsModActiveForMultiplayer)
                        {
                            multiplayerOptionMessage = $"the game host did not allow usage";
                        }
                        GUILayout.Label(PermissionChangedMessage($"revoked", $"{multiplayerOptionMessage}"), LocalStylingManager.ColoredFieldValueLabel(Color.yellow));
                    }
                }
            }
            catch (Exception exc)
            {
                HandleException(exc, nameof(MultiplayerOptionBox));
            }
        }

        protected virtual void ModShortcutsInfoBox()
        {
            using (new GUILayout.VerticalScope(GUI.skin.box))
            {
                GUILayout.Label("Mod shortcut options", LocalStylingManager.ColoredSubHeaderLabel(Color.yellow));

                GUILayout.Label($"To destroy the target on mouse pointer, press [{DeleteShortcutKey}]", LocalStylingManager.TextLabel);
            }
        }

        protected virtual void ConstructionsManagerBox()
        {
            try
            {
                using (new GUILayout.VerticalScope(GUI.skin.box))
                {
                    GUILayout.Label($"Constructions Manager", LocalStylingManager.ColoredHeaderLabel(Color.yellow));
                    GUILayout.Label($"Constructions Options", LocalStylingManager.ColoredSubHeaderLabel(Color.yellow));

                    DestroyTargetOptionBox();
                }
            }
            catch (Exception exc)
            {
                HandleException(exc, nameof(ConstructionsManagerBox));
            }
        }

        protected virtual void DestroyTargetOptionBox()
        {
            bool _destroyTargetOption = DestroyTargetOption;
            DestroyTargetOption = GUILayout.Toggle(DestroyTargetOption, $"Use [{DeleteShortcutKey}] to destroy target?", GUI.skin.toggle);
            if (_destroyTargetOption != DestroyTargetOption)
            {
                ShowHUDBigInfo(HUDBigInfoMessage($"Destroy target with [{DeleteShortcutKey}] has been {(DestroyTargetOption ? "enabled" : "disabled")} ", MessageType.Info, Color.green));
            }
        }

        protected virtual void DestroyTarget()
        {
            try
            {
                if (IsModActiveForSingleplayer || IsModActiveForMultiplayer)
                {
                    if (DestroyTargetOption)
                    {
                        if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out RaycastHit hitInfo))
                        {
                            DestroyOnHit(hitInfo);
                        }
                    }
                }
            }
            catch (Exception exc)
            {
                HandleException(exc, nameof(DestroyTarget));
            }
        }

        protected virtual void DestroyOnHit(RaycastHit hitInfo)
        {
            try
            {
                if (IsModActiveForSingleplayer || IsModActiveForMultiplayer)
                {
                    if (DestroyTargetOption)
                    {
                        SelectedGameObjectToDestroy = hitInfo.collider.transform.gameObject;
                        if (SelectedGameObjectToDestroy != null)
                        {
                            var localization = GreenHellGame.Instance.GetLocalization();
                            SelectedItemToDestroy = SelectedGameObjectToDestroy?.GetComponent<Item>();
                            if (SelectedItemToDestroy != null && Item.Find(SelectedItemToDestroy.GetInfoID()) != null)
                            {
                                SelectedGameObjectToDestroyName = localization.Get(SelectedItemToDestroy.GetInfoID().ToString()) ?? SelectedItemToDestroy?.GetName();
                            }
                            else
                            {
                                SelectedGameObjectToDestroyName = localization.Get(SelectedGameObjectToDestroy?.name) ?? SelectedGameObjectToDestroy?.name;
                            }

                            ShowConfirmDestroyDialog(SelectedGameObjectToDestroyName);
                        }
                    }
                }
            }
            catch (Exception exc)
            {
                HandleException(exc, nameof(DestroyOnHit));
            }
        }

        protected virtual void ShowConfirmDestroyDialog(string itemToDestroyName)
        {
            try
            {
                EnableCursor(true);
                string description = $"Are you sure you want to destroy {itemToDestroyName}?";
                YesNoDialog destroyYesNoDialog = GreenHellGame.GetYesNoDialog();
                destroyYesNoDialog.Show(this, DialogWindowType.YesNo, $"{ModName} Info", description, true, false);
                destroyYesNoDialog.gameObject.SetActive(true);
            }
            catch (Exception exc)
            {
                HandleException(exc, nameof(ShowConfirmDestroyDialog));
            }
        }

        protected virtual void DestroySelectedItem()
        {
            try
            {                
                if (AlsoDestroyCraftedItems && CraftedItems != null)
                {
                    List<Item> toDestroy = GetCraftedItems(SelectedFilter);
                    if (toDestroy != null)
                    {
                        foreach (Item craftedItem in toDestroy)
                        {
                          Destroy(craftedItem);
                        }
                        toDestroy.Clear();
                    }
                    else
                    {
                        ShowHUDBigInfo(HUDBigInfoMessage(ItemNotSelectedMessage(), MessageType.Warning,LocalStylingManager.DefaultAttentionColor));
                    }
                    AlsoDestroyCraftedItems = false;
                }
                else
                {
                    if (SelectedGameObjectToDestroy != null)
                    {
                        if (SelectedItemToDestroy != null || IsDestroyable(SelectedGameObjectToDestroy))
                        {
                            if (SelectedItemToDestroy != null && !SelectedItemToDestroy.IsPlayer() && !SelectedItemToDestroy.IsAI() && !SelectedItemToDestroy.IsHumanAI())
                            {
                                LocalItemsManager.AddItemToDestroy(SelectedItemToDestroy);
                            }
                            else
                            {
                                Destroy(SelectedGameObjectToDestroy);
                            }
                            ShowHUDBigInfo(HUDBigInfoMessage(ItemDestroyedMessage(SelectedGameObjectToDestroyName), MessageType.Info, LocalStylingManager.DefaultEnabledColor));
                        }
                        else
                        {
                            ShowHUDBigInfo(HUDBigInfoMessage(ItemNotDestroyedMessage(SelectedGameObjectToDestroyName), MessageType.Warning, LocalStylingManager.DefaultAttentionColor));
                        }
                    }
                }            
            }
            catch (Exception exc)
            {
                if (AlsoDestroyCraftedItems)
                {
                    AlsoDestroyCraftedItems = false;
                }
                HandleException(exc, nameof(DestroySelectedItem));
            }
        }

        protected virtual bool IsDestroyable(GameObject go)
        {
            try
            {
                if (go == null || string.IsNullOrEmpty(go.name))
                {
                    return false;
                }
                return true;
            }
            catch (Exception exc)
            {
                HandleException(exc, nameof(IsDestroyable));
                return false;
            }
        }

        protected virtual void DestroyItemsBox()
        {
            //DestroySelectedItemInPlayerRangeBox();
            DestroyFilteredItemsBox();
        }

        protected virtual void DestroyFilteredItemsBox()
        {
            try
            {
                using (new GUILayout.VerticalScope(GUI.skin.box))
                {
                    using ( new GUILayout.HorizontalScope(GUI.skin.box))
                    {
                        GUILayout.Label($"Click to destroy {SelectedFilter.ToString().ToLower()} crafted using this mod.", LocalStylingManager.TextLabel);                     

                        if (GUILayout.Button($"Destroy", GUI.skin.button, GUILayout.Width(150f)))
                        {
                            AlsoDestroyCraftedItems = true;
                            ShowConfirmDestroyDialog(SelectedFilter.ToString().ToLower());
                        }
                    }
                }
            }
            catch (Exception exc)
            {
                HandleException(exc, nameof(DestroyFilteredItemsBox));
            }
        }

        protected virtual void CraftItemsBox()
        {
            ItemsScrollViewBox();
        }

        protected virtual string[] GetFilters()
        {
            string[] filters = Enum.GetNames(typeof(ItemFilter));
            return filters;
        }

        protected virtual string[] GetFilteredItemNames(ItemFilter filter)
        {
            List<string> filteredItemNames = new List<string>();
            List<ItemInfo> allInfos = LocalItemsManager.GetAllInfos().Values.ToList();
            List<ItemInfo> filteredInfos = new List<ItemInfo>();
            switch (filter)
            {
                case ItemFilter.Keyword:
                    filteredInfos = allInfos.Where(info => info.GetNameToDisplayLocalized().ToLower().Contains(SearchItemKeyWord.Trim().ToLower())
                                                                                        || info.m_ID.ToString().ToLower().Contains(SearchItemKeyWord.Trim().ToLower())).ToList();
                    break;
                case ItemFilter.Medical:
                    filteredInfos = GetMedical();
                    break;
                case ItemFilter.Unique:
                    filteredInfos = GetUnique();
                    break;
                case ItemFilter.Resources:
                    filteredInfos = GetResources();
                    break;
                case ItemFilter.Food:
                    filteredInfos = allInfos.Where(info => info.IsSeed() || info.IsMeat() || info.IsFood() || info.IsConsumable()).ToList();
                    break;
                case ItemFilter.Construction:
                    filteredInfos = allInfos.Where(info => info.IsConstruction()).ToList();
                    break;
                case ItemFilter.Tools:
                    filteredInfos = allInfos.Where(info => info.IsTool()).ToList();
                    break;
                case ItemFilter.Weapons:
                    filteredInfos = allInfos.Where(info => info.IsWeapon()).ToList();
                    break;
                case ItemFilter.Armor:
                    filteredInfos = allInfos.Where(info => info.IsArmor()).ToList();
                    break;
                case ItemFilter.All:
                default:
                    filteredInfos = allInfos;
                    break;
            }
            if (filteredInfos != null)
            {
                foreach (ItemInfo filteredInfo in filteredInfos)
                {
                    string filteredItemName = filteredInfo.m_ID.ToString();
                    filteredItemNames.Add(filteredItemName.Replace("_", " "));
                }
            }
            return filteredItemNames.OrderBy(itemName => itemName).ToArray();
        }

        protected virtual List<ItemInfo> GetMedical() => new List<ItemInfo>
            {
                LocalItemsManager.GetInfo(ItemID.Molineria_leaf),
                LocalItemsManager.GetInfo(ItemID.lily_dressing),
                LocalItemsManager.GetInfo(ItemID.ash_dressing),
                LocalItemsManager.GetInfo(ItemID.Goliath_dressing),
                LocalItemsManager.GetInfo(ItemID.Ficus_Dressing),
                LocalItemsManager.GetInfo(ItemID.Honey_Dressing),
                LocalItemsManager.GetInfo(ItemID.Tabaco_Dressing),
                LocalItemsManager.GetInfo(ItemID.Campfire_ash),
                LocalItemsManager.GetInfo(ItemID.Goliath_birdeater_ash),
                LocalItemsManager.GetInfo(ItemID.Plantain_lily_leaf),
                LocalItemsManager.GetInfo(ItemID.Ficus_leaf),
                LocalItemsManager.GetInfo(ItemID.Tobacco_Leaf),
                LocalItemsManager.GetInfo(ItemID.Charcoal),
                LocalItemsManager.GetInfo(ItemID.Bone),
                LocalItemsManager.GetInfo(ItemID.Fish_Bone),
                LocalItemsManager.GetInfo(ItemID.copa_hongo),
                LocalItemsManager.GetInfo(ItemID.lily_flower),
                LocalItemsManager.GetInfo(ItemID.Albahaca_Leaf),
                LocalItemsManager.GetInfo(ItemID.coca_leafs),
                LocalItemsManager.GetInfo(ItemID.Leaf_Bandage),
                LocalItemsManager.GetInfo(ItemID.Ants),
                LocalItemsManager.GetInfo(ItemID.Ant_Head),
                LocalItemsManager.GetInfo(ItemID.Ayahasca_Recepie),
                LocalItemsManager.GetInfo(ItemID.coffee_instant),
                LocalItemsManager.GetInfo(ItemID.Guanabana_Fruit),
                LocalItemsManager.GetInfo(ItemID.Honeycomb),
                LocalItemsManager.GetInfo(ItemID.indigo_blue_leptonia),
                LocalItemsManager.GetInfo(ItemID.Maggot),
                LocalItemsManager.GetInfo(ItemID.Maggots),
                LocalItemsManager.GetInfo(ItemID.molineria_flowers),
                LocalItemsManager.GetInfo(ItemID.Painkillers),
                LocalItemsManager.GetInfo(ItemID.Phallus_indusiatus),
                LocalItemsManager.GetInfo(ItemID.plantain_lilly_flowers),
                LocalItemsManager.GetInfo(ItemID.Turtle_shell),
                LocalItemsManager.GetInfo(ItemID.Coconut_Bowl),
                LocalItemsManager.GetInfo(ItemID.Gerronema_retiarium),
                LocalItemsManager.GetInfo(ItemID.Gerronema_viridilucens),
                LocalItemsManager.GetInfo(ItemID.marasmius_haematocephalus),
                LocalItemsManager.GetInfo(ItemID.military_bed_toUse),
                LocalItemsManager.GetInfo(ItemID.monstera_deliciosa_fruit),
                LocalItemsManager.GetInfo(ItemID.PoisonDartFrog_Alive),
                LocalItemsManager.GetInfo(ItemID.psychotria_viridis),
                LocalItemsManager.GetInfo(ItemID.psychotria_viridis_berries),
                LocalItemsManager.GetInfo(ItemID.Quassia_Amara_flowers),
                LocalItemsManager.GetInfo(ItemID.QuestItem_BioHazmatSuit),
                LocalItemsManager.GetInfo(ItemID.QuestItem_Cure_Vial),
                LocalItemsManager.GetInfo(ItemID.tobacco_flowers),
                LocalItemsManager.GetInfo(ItemID.Tobacco_Torch),

            };

        protected virtual List<Item> GetCraftedItems(ItemFilter filter)
        {
            List<Item> filteredItems;
            switch (filter)
            {
                case ItemFilter.Keyword:
                    filteredItems = CraftedItems.Where(craftedItem => craftedItem.m_Info.GetNameToDisplayLocalized().ToLower().Contains(SearchItemKeyWord.Trim().ToLower())
                                                                                        || craftedItem.m_Info.m_ID.ToString().ToLower().Contains(SearchItemKeyWord.Trim().ToLower())).ToList();
                    break;
                case ItemFilter.Medical:
                    filteredItems = CraftedItems.Where(craftedItem => GetMedical().Contains(craftedItem.m_Info)).ToList();
                    break;
                case ItemFilter.Unique:
                    filteredItems = CraftedItems.Where(craftedItem => GetUnique().Contains(craftedItem.m_Info)).ToList();
                    break;
                case ItemFilter.Resources:
                    filteredItems = CraftedItems.Where(craftedItem => GetResources().Contains(craftedItem.m_Info)).ToList();
                    break;
                case ItemFilter.Food:
                    filteredItems = CraftedItems.Where(craftedItem => craftedItem.m_Info.IsSeed() || craftedItem.m_Info.IsMeat() || craftedItem.m_Info.IsFood() || craftedItem.m_Info.IsConsumable()).ToList();
                    break;
                case ItemFilter.Construction:
                    filteredItems = CraftedItems.Where(craftedItem => craftedItem.m_Info.IsConstruction()).ToList();
                    break;
                case ItemFilter.Tools:
                    filteredItems = CraftedItems.Where(craftedItem => craftedItem.m_Info.IsTool()).ToList();
                    break;
                case ItemFilter.Weapons:
                    filteredItems = CraftedItems.Where(craftedItem => craftedItem.m_Info.IsWeapon()).ToList();
                    break;
                case ItemFilter.Armor:
                    filteredItems = CraftedItems.Where(craftedItem => craftedItem.m_Info.IsArmor()).ToList();
                    break;
                case ItemFilter.All:
                default:
                    filteredItems = CraftedItems;
                    break;
            }
            return filteredItems;
        }

        protected virtual List<ItemInfo> GetUnique()
        {
            List<ItemInfo> allInfos = LocalItemsManager.GetAllInfos().Values.ToList();
            List<ItemInfo> uniques = allInfos.Where(info => info.m_ID.IsQuestItem() || info.IsReadableItem()).ToList();
            List<ItemID> uids = new List<ItemID>
            {
                ItemID.Pot,
                ItemID.Bidon,
                ItemID.Wooden_Spoon,
                ItemID.GrapplingHook,
                ItemID.grappling_hook_gun,
                ItemID.Grappling_Hook,
                ItemID.grappling_hook_gun_Survi,
                ItemID.Grappling_Hook_Survi,
                ItemID.Machete,
                ItemID.Radio,
                ItemID.william_ball,
                ItemID.Rusted_Axe,
                ItemID.Rusted_Machete,
                ItemID.moneybag
            };
            foreach (ItemID iD in uids)
            {
                ItemInfo info = LocalItemsManager.GetInfo(iD);
                if (!uniques.Contains(info))
                {
                    uniques.Add(info);
                }
            }
            return uniques;
        }

        protected virtual List<ItemInfo> GetResources() => new List<ItemInfo>
            {
                LocalItemsManager.GetInfo(ItemID.Log),
                LocalItemsManager.GetInfo(ItemID.Bamboo_Log),
                LocalItemsManager.GetInfo(ItemID.Long_Stick),
                LocalItemsManager.GetInfo(ItemID.Bamboo_Long_Stick),
                LocalItemsManager.GetInfo(ItemID.Stick),
                LocalItemsManager.GetInfo(ItemID.Bamboo_Stick),
                LocalItemsManager.GetInfo(ItemID.Small_Stick),
                LocalItemsManager.GetInfo(ItemID.Stone),
                LocalItemsManager.GetInfo(ItemID.Big_Stone),
                LocalItemsManager.GetInfo(ItemID.Obsidian_Stone),
                LocalItemsManager.GetInfo(ItemID.iron_ore_stone),
                LocalItemsManager.GetInfo(ItemID.Rope),
                LocalItemsManager.GetInfo(ItemID.Bone),
                LocalItemsManager.GetInfo(ItemID.mud_to_build),
                LocalItemsManager.GetInfo(ItemID.Dry_leaf),
                LocalItemsManager.GetInfo(ItemID.Banana_Leaf),
                LocalItemsManager.GetInfo(ItemID.Palm_Leaf),
                LocalItemsManager.GetInfo(ItemID.Bird_feather),
                LocalItemsManager.GetInfo(ItemID.Bird_Nest),
                LocalItemsManager.GetInfo(ItemID.Wood_Resin),
                LocalItemsManager.GetInfo(ItemID.Campfire_ash),
                LocalItemsManager.GetInfo(ItemID.Can_big),
                LocalItemsManager.GetInfo(ItemID.Can_big_open),
                LocalItemsManager.GetInfo(ItemID.Can_small),
                LocalItemsManager.GetInfo(ItemID.Can_small_open),
                LocalItemsManager.GetInfo(ItemID.Charcoal),
                LocalItemsManager.GetInfo(ItemID.Dryed_Liane),
                LocalItemsManager.GetInfo(ItemID.Fiber),
                LocalItemsManager.GetInfo(ItemID.Ficus_leaf),
                LocalItemsManager.GetInfo(ItemID.Fish_Bone),
                LocalItemsManager.GetInfo(ItemID.Molineria_leaf),
                LocalItemsManager.GetInfo(ItemID.Small_leaf_pile),
                LocalItemsManager.GetInfo(ItemID.Tobacco_Leaf),
                LocalItemsManager.GetInfo(ItemID.Turtle_shell)
            };

        protected virtual void ItemsFilterBox()
        {
            using (new GUILayout.VerticalScope(GUI.skin.box))
            {
                string[] filters = GetFilters();
                if (filters != null)
                {
                    int filtersCount = filters.Length;
                    int _SelectedFilterIndex = SelectedFilterIndex;

                    GUILayout.Label("Choose an item filter.", LocalStylingManager.TextLabel);
                   
                    SelectedFilterIndex = GUILayout.SelectionGrid(SelectedFilterIndex, filters, filtersCount, LocalStylingManager.ColoredSelectedGridButton(_SelectedFilterIndex != SelectedFilterIndex));

                    using (new GUILayout.HorizontalScope(GUI.skin.box))
                    {
                        GUILayout.Label("Click to activate selected filter: ", LocalStylingManager.TextLabel);
                        if (GUILayout.Button($"Apply filter", GUI.skin.button, GUILayout.Width(150f)))
                        {
                            OnClickApplyFilterButton();
                        }
                    }

                    GUILayout.Label("If you want to search for items on keyword, first choose Keyword as filter and click [Apply filter]. Then, start typing in the keyword to filter on below: ", LocalStylingManager.ColoredCommentLabel(LocalStylingManager.DefaultAttentionColor));

                    using (new GUILayout.HorizontalScope(GUI.skin.box))
                    {
                        GUILayout.Label("Keyword to filter on: ", LocalStylingManager.FormFieldNameLabel);
                        SearchItemKeyWord = GUILayout.TextField(SearchItemKeyWord, LocalStylingManager.FormInputTextField);
                    }                   
                }
            }
        }

        protected virtual void ItemsScrollViewBox()
        {
            using (new GUILayout.VerticalScope(GUI.skin.box))
            {
                using (new GUILayout.HorizontalScope(GUI.skin.box))
                {
                    GUILayout.Label($"Items filtered on:", LocalStylingManager.ColoredFieldNameLabel(LocalStylingManager.DefaultHighlightColor));
                    GUILayout.Label($"{SelectedFilter}", LocalStylingManager.ColoredFieldValueLabel(LocalStylingManager.DefaultHighlightColor));
                }

                FilteredItemsScrollView();

                string[] filteredItemNames = GetFilteredItemNames(SelectedFilter);
                SelectedItemToCraftItemName = filteredItemNames[SelectedItemToCraftIndex].Replace(" ", "_");
                using (new GUILayout.HorizontalScope(GUI.skin.box))
                {
                    GUILayout.Label("Select item to craft: ", LocalStylingManager.ColoredFieldNameLabel(LocalStylingManager.DefaultHighlightColor));
                    GUILayout.Label($"{SelectedItemToCraftItemName}", LocalStylingManager.ColoredFieldValueLabel(LocalStylingManager.DefaultHighlightColor));
                }

                using (new GUILayout.HorizontalScope(GUI.skin.box))
                {
                    ShouldAddToBackpackOption = GUILayout.Toggle(ShouldAddToBackpackOption, "Add to backpack?", GUI.skin.toggle);

                    GUILayout.Label("Craft how many?: ", LocalStylingManager.FormFieldNameLabel);

                    ItemCountToCraft = GUILayout.TextField(ItemCountToCraft,LocalStylingManager.FormInputTextField, GUILayout.Width(50f));

                    if (GUILayout.Button($"Craft selected", GUI.skin.button, GUILayout.Width(150f)))
                    {
                        OnClickCraftSelectedItemButton();
                    }
                }
            }
        }

        protected virtual void OnClickApplyFilterButton()
        {
            string[] filters = GetFilters();
            if (filters != null)
            {
                SelectedFilterName = filters[SelectedFilterIndex];
                SelectedFilter = (ItemFilter)Enum.Parse(typeof(ItemFilter), SelectedFilterName);
            }
        }

        protected virtual void FilteredItemsScrollView()
        {
            FilteredItemsScrollViewPosition = GUILayout.BeginScrollView(FilteredItemsScrollViewPosition, GUI.skin.scrollView, GUILayout.MinHeight(300f));

            string[] filteredItemNames = GetFilteredItemNames(SelectedFilter);
            if (filteredItemNames != null)
            {
                int _SelectedItemToCraftIndex = SelectedItemToCraftIndex;
                SelectedItemToCraftItemName = filteredItemNames[SelectedItemToCraftIndex].Replace(" ", "_");
                SelectedItemToCraftIndex = GUILayout.SelectionGrid(SelectedItemToCraftIndex, filteredItemNames, 3, LocalStylingManager.ColoredSelectedGridButton(_SelectedItemToCraftIndex != SelectedItemToCraftIndex));
            }
            GUILayout.EndScrollView();
        }

        protected virtual void OnClickCraftSelectedItemButton()
        {
            try
            {
                string[] filteredItemNames = GetFilteredItemNames(SelectedFilter);
                if (filteredItemNames != null)
                {
                    SelectedItemToCraftItemName = filteredItemNames[SelectedItemToCraftIndex].Replace(" ", "_");
                    if (!string.IsNullOrEmpty(SelectedItemToCraftItemName))
                    {
                        SelectedItemToCraftItemID = (ItemID)Enum.Parse(typeof(ItemID), SelectedItemToCraftItemName);
                        CraftSelectedItem(SelectedItemToCraftItemID);
                    }
                }
            }
            catch (Exception exc)
            {
                HandleException(exc, nameof(OnClickCraftSelectedItemButton));
            }
        }
                
        protected virtual void CraftSelectedItem(ItemID itemID)
        {
            try
            {
                if (string.IsNullOrEmpty(ItemCountToCraft) || !int.TryParse(ItemCountToCraft, out int CountToCraft))
                {
                    CountToCraft = 1;
                    ItemCountToCraft = "1";
                }

                if (SelectedFilter == ItemFilter.Construction)
                {
                    CountToCraft = 1;
                    ItemCountToCraft = "1";
                    ShouldAddToBackpackOption = false;
                }

                for (int i = 0; i < CountToCraft; i++)
                {
                    if (ShouldAddToBackpackOption)
                    {
                        LocalPlayer.AddItemToInventory(itemID.ToString());
                        SelectedItemToCraft = LocalInventoryBackpack.FindItem(itemID);
                    }
                    else
                    {
                        SelectedItemToCraft = LocalItemsManager.CreateItem(itemID, true, LocalPlayer.transform.position + LocalPlayer.transform.forward * 1f, LocalPlayer.transform.rotation);
                    }

                    if (SelectedItemToCraft != null)
                    {
                        CraftedItems.Add(SelectedItemToCraft);
                    }
                }

                ShowHUDBigInfo(
                       HUDBigInfoMessage(
                           ItemCraftedMessage(LocalItemsManager.GetInfo(SelectedItemToCraftItemID).GetNameToDisplayLocalized(), CountToCraft),
                           MessageType.Info,
                           LocalStylingManager.DefaultEnabledColor
                       )
                );
            }
            catch (Exception exc)
            {
                HandleException(exc, nameof(CraftSelectedItem));
            }
        }

        public void OnYesFromDialog()
        {
            DestroySelectedItem();
            EnableCursor(false);
        }

        public void OnNoFromDialog()
        {
            SelectedGameObjectToDestroy = null;
            EnableCursor(false);
        }

        public void OnOkFromDialog()
        {
            OnYesFromDialog();
        }

        public void OnCloseDialog()
        {
            EnableCursor(false);
        }

    }

}