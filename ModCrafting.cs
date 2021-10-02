using Enums;
using ModCrafting.Enums;
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
    /// ModCrafting is a mod for Green Hell, that allows a player to craft any game item
    /// without the needed materials and to destroy any item pointed at with the mouse.
    /// Press Keypad1 (default) or the key configurable in ModAPI to open the mod screen.
    /// </summary>
    public class ModCrafting : MonoBehaviour, IYesNoDialogOwner
    {
        private static ModCrafting Instance;

        private static readonly string ModName = nameof(ModCrafting);
        private static readonly float ModScreenTotalWidth = 850f;
        private static readonly float ModScreenTotalHeight = 500f;
        private static readonly float ModScreenMinWidth = 800f;
        private static readonly float ModScreenMaxWidth = 850f;
        private static readonly float ModScreenMinHeight = 50f;
        private static readonly float ModScreenMaxHeight = 550f;
        private static float ModScreenStartPositionX { get; set; } =  Screen.width / 7f;
        private static float ModScreenStartPositionY { get; set; } = Screen.height / 7f;
        private static bool IsMinimized { get; set; } = false;

        private Color DefaultGuiColor = GUI.color;
        private bool ShowUI = false;

        private static ItemsManager LocalItemsManager;
        private static ConstructionController LocalConstructionController;
        private static HUDManager LocalHUDManager;
        private static Player LocalPlayer;
        private static InventoryBackpack LocalInventoryBackpack;

        public static Rect ModCraftingScreen = new Rect(ModScreenStartPositionX, ModScreenStartPositionY, ModScreenTotalWidth, ModScreenTotalHeight);
        public static string SearchItemKeyWord = string.Empty;
        public static Vector2 FilteredItemsScrollViewPosition;
        public static string SelectedItemToCraftItemName;
        public static int SelectedItemToCraftIndex;
        public static ItemID SelectedItemToCraftItemID;
        public static Item SelectedItemToCraft;
        public static GameObject SelectedGameObjectToDestroy = null;
        public static string SelectedGameObjectToDestroyName = string.Empty;
        public static List<string> DestroyableObjectNames { get; set; } = new List<string> {
                                                                                "tree", "plant", "leaf", "stone", "seat", "bag", "beam", "corrugated", "anaconda",
                                                                                "metal", "board", "cardboard", "plank", "plastic", "small", "tarp", "oil", "sock",
                                                                                "cartel", "military", "tribal", "village", "ayahuasca", "gas", "boat", "ship",
                                                                                "bridge", "chair", "stove", "barrel", "tank", "jerrycan", "microwave",
                                                                                "sprayer", "shelf", "wind", "air", "bottle", "trash", "lab", "table", "diving",
                                                                                "roof", "floor", "hull", "frame", "cylinder", "wire", "wiretap", "generator"
                                                                        };
        public static Item SelectedItemToDestroy = null;
        public static string SelectedFilterName;
        public static int SelectedFilterIndex;
        public static ItemFilter SelectedFilter = ItemFilter.All;
        public static string ItemCountToCraft = "1";
        public static bool ShouldAddToBackpackOption = true;
        public static List<Item> CraftedItems = new List<Item>();

        public bool DestroyTargetOption { get; private set; } = false;

        public bool IsModActiveForMultiplayer { get; private set; } = false;
        public bool IsModActiveForSingleplayer => ReplTools.AmIMaster();

        public static string ItemDestroyedMessage(string item)
            => $"{item} destroyed!";
        public static string ItemNotDestroyedMessage(string item)
            => $"{item} cannot be destroyed!";
        public static string ItemNotSelectedMessage()
            => $"Not any item selected to destroy!";
        public static string ItemNotCraftedMessage()
            => $"Item could not be crafted!";
        public static string ItemCraftedMessage(string item, int count)
            => $"{count} x {item} crafted!";
        public static string OnlyForSinglePlayerOrHostMessage()
            => $"Only available for single player or when host. Host can activate using ModManager.";
        public static string PermissionChangedMessage(string permission, string reason)
            => $"Permission to use mods and cheats in multiplayer was {permission} because {reason}.";
        public static string HUDBigInfoMessage(string message, MessageType messageType, Color? headcolor = null)
            => $"<color=#{ (headcolor != null ? ColorUtility.ToHtmlStringRGBA(headcolor.Value) : ColorUtility.ToHtmlStringRGBA(Color.red))  }>{messageType}</color>\n{message}";

        private void HandleException(Exception exc, string methodName)
        {
            string info = $"[{ModName}:{methodName}] throws exception:\n{exc.Message}";
            ModAPI.Log.Write(info);
            ShowHUDBigInfo(HUDBigInfoMessage(info, MessageType.Error, Color.red));
        }

        private static readonly string RuntimeConfigurationFile = Path.Combine(Application.dataPath.Replace("GH_Data", "Mods"), "RuntimeConfiguration.xml");
        private static KeyCode ModKeybindingId { get; set; } = KeyCode.Keypad1;
        private static KeyCode ModDeleteKeybindingId { get; set; } = KeyCode.Delete;
        private KeyCode GetConfigurableKey(string buttonId)
        {
            KeyCode configuredKeyCode = default;
            string configuredKeybinding = string.Empty;

            try
            {
                if (File.Exists(RuntimeConfigurationFile))
                {
                    using (var xmlReader = XmlReader.Create(new StreamReader(RuntimeConfigurationFile)))
                    {
                        while (xmlReader.Read())
                        {
                            if (xmlReader["ID"] == ModName)
                            {
                                if (xmlReader.ReadToFollowing(nameof(Button)) && xmlReader["ID"] == buttonId)
                                {
                                    configuredKeybinding = xmlReader.ReadElementContentAsString();
                                }
                            }
                        }
                    }
                }

                configuredKeybinding = configuredKeybinding?.Replace("NumPad", "Keypad").Replace("Oem", "");

                configuredKeyCode = (KeyCode)(!string.IsNullOrEmpty(configuredKeybinding)
                                                            ? Enum.Parse(typeof(KeyCode), configuredKeybinding)
                                                            : GetType().GetProperty(buttonId)?.GetValue(this));
                return configuredKeyCode;
            }
            catch (Exception exc)
            {
                HandleException(exc, nameof(GetConfigurableKey));
                configuredKeyCode = (KeyCode)(GetType().GetProperty(buttonId)?.GetValue(this));
                return configuredKeyCode;
            }
        }

        public void Start()
        {
            ModManager.ModManager.onPermissionValueChanged += ModManager_onPermissionValueChanged;
            ModKeybindingId = GetConfigurableKey(nameof(ModKeybindingId));
            ModDeleteKeybindingId = GetConfigurableKey(nameof(ModDeleteKeybindingId));
        }

        private void ModManager_onPermissionValueChanged(bool optionValue)
        {
            string reason = optionValue ? "the game host allowed usage" : "the game host did not allow usage";
            IsModActiveForMultiplayer = optionValue;

            ShowHUDBigInfo(
                          (optionValue ?
                            HUDBigInfoMessage(PermissionChangedMessage($"granted", $"{reason}"), MessageType.Info, Color.green)
                            : HUDBigInfoMessage(PermissionChangedMessage($"revoked", $"{reason}"), MessageType.Info, Color.yellow))
                            );
        }

        public ModCrafting()
        {
            useGUILayout = true;
            Instance = this;
        }

        public static ModCrafting Get()
        {
            return Instance;
        }

        public void ShowHUDBigInfo(string text)
        {
            string header = $"{ModName} Info";
            string textureName = HUDInfoLogTextureType.Count.ToString();
            HUDBigInfo hudBigInfo = (HUDBigInfo)LocalHUDManager.GetHUD(typeof(HUDBigInfo));
            HUDBigInfoData.s_Duration = 2f;
            HUDBigInfoData hudBigInfoData = new HUDBigInfoData
            {
                m_Header = header,
                m_Text = text,
                m_TextureName = textureName,
                m_ShowTime = Time.time
            };
            hudBigInfo.AddInfo(hudBigInfoData);
            hudBigInfo.Show(true);
        }

        public void ShowHUDInfoLog(string itemID, string localizedTextKey)
        {
            var localization = GreenHellGame.Instance.GetLocalization();
            HUDMessages hUDMessages = (HUDMessages)LocalHUDManager.GetHUD(typeof(HUDMessages));
            hUDMessages.AddMessage(
                $"{localization.Get(localizedTextKey)}  {localization.Get(itemID)}"
                );
        }

        private void EnableCursor(bool blockPlayer = false)
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

        private void Update()
        {
            if (Input.GetKeyDown(ModKeybindingId))
            {
                if (!ShowUI)
                {
                    InitData();
                    EnableCursor(true);
                }
                ToggleShowUI();
                if (!ShowUI)
                {
                    EnableCursor(false);
                }
            }

            if (Input.GetKeyDown(ModDeleteKeybindingId))
            {
                DestroyTarget();
            }
        }

        private void DestroyTarget()
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

        private void DestroyOnHit(RaycastHit hitInfo)
        {
            try
            {
                if (IsModActiveForSingleplayer || IsModActiveForMultiplayer)
                {
                    if (DestroyTargetOption)
                    {
                        GameObject go = hitInfo.collider.transform.gameObject;
                        if (go != null)
                        {
                            SelectedGameObjectToDestroy = go.gameObject;
                            ShowConfirmDestroyDialog();
                        }
                    }
                }
            }
            catch (Exception exc)
            {
                HandleException(exc, nameof(DestroyOnHit));
            }
        }

        private void ShowConfirmDestroyDialog()
        {
            EnableCursor(true);
            string description = $"Are you sure you want to destroy selected { (SelectedGameObjectToDestroy != null ? SelectedGameObjectToDestroy.name : SelectedFilter.ToString().ToLower()) }?";
            YesNoDialog destroyYesNoDialog = GreenHellGame.GetYesNoDialog();
            destroyYesNoDialog.Show(this, DialogWindowType.YesNo, $"{ModName} Info", description, true);
            destroyYesNoDialog.gameObject.SetActive(true);
        }

        private void ToggleShowUI()
        {
            ShowUI = !ShowUI;
        }

        private void DestroySelectedItem()
        {
            try
            {
                if (SelectedGameObjectToDestroy != null)
                {
                    SelectedItemToDestroy = SelectedGameObjectToDestroy.GetComponent<Item>();
                    SelectedGameObjectToDestroyName = SelectedItemToDestroy != null && SelectedItemToDestroy.m_Info != null
                                                                                                    ? SelectedItemToDestroy.m_Info.GetNameToDisplayLocalized()
                                                                                                    : GreenHellGame.Instance.GetLocalization().Get(SelectedGameObjectToDestroy.name);

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
                        ShowHUDBigInfo(HUDBigInfoMessage(ItemDestroyedMessage(SelectedGameObjectToDestroyName), MessageType.Info, Color.green));
                    }
                    else
                    {
                        ShowHUDBigInfo(HUDBigInfoMessage(ItemNotDestroyedMessage(SelectedGameObjectToDestroyName), MessageType.Error, Color.red));
                    }
                }
                else if (CraftedItems != null)
                {
                    List<Item> toDestroy = GetCraftedItems(SelectedFilter);
                    if (toDestroy != null)
                    {
                        foreach (Item craftedItem in toDestroy)
                        {
                            LocalItemsManager.AddItemToDestroy(craftedItem);
                        }
                        toDestroy.Clear();
                    }
                    else
                    {
                        ShowHUDBigInfo(HUDBigInfoMessage(ItemNotSelectedMessage(), MessageType.Warning, Color.yellow));
                    }
                }
                else
                {
                    ShowHUDBigInfo(HUDBigInfoMessage(ItemNotSelectedMessage(), MessageType.Warning, Color.yellow));
                }
            }
            catch (Exception exc)
            {
                HandleException(exc, nameof(DestroySelectedItem));
            }
        }

        private bool IsDestroyable(GameObject go)
        {
            try
            {
                if (go == null || string.IsNullOrEmpty(go.name))
                {
                    return false;
                }
                return DestroyableObjectNames.Any(destroyableObjectName => go.name.ToLower().Contains(destroyableObjectName));
            }
            catch (Exception exc)
            {
                HandleException(exc, nameof(IsDestroyable));
                return false;
            }
        }

        private void OnGUI()
        {
            if (ShowUI)
            {
                InitData();
                InitSkinUI();
                InitWindow();
            }
        }

        private void InitWindow()
        {
            int wid = GetHashCode();
            ModCraftingScreen = GUILayout.Window(wid,
                                                                                            ModCraftingScreen,
                                                                                            InitModCraftingScreen,
                                                                                            ModName,
                                                                                            GUI.skin.window,
                                                                                            GUILayout.ExpandWidth(true),
                                                                                            GUILayout.MinWidth(ModScreenMinWidth),
                                                                                            GUILayout.MaxWidth(ModScreenMaxWidth),
                                                                                            GUILayout.ExpandHeight(true),
                                                                                            GUILayout.MinHeight(ModScreenMinHeight),
                                                                                            GUILayout.MaxHeight(ModScreenMaxHeight));
        }

        private void InitData()
        {
            LocalItemsManager = ItemsManager.Get();
            LocalConstructionController = ConstructionController.Get();
            LocalHUDManager = HUDManager.Get();
            LocalPlayer = Player.Get();
            LocalInventoryBackpack = InventoryBackpack.Get();
        }

        private void InitSkinUI()
        {
            GUI.skin = ModAPI.Interface.Skin;
        }

        private void InitModCraftingScreen(int windowID)
        {
            ModScreenStartPositionX = ModCraftingScreen.x;
            ModScreenStartPositionY = ModCraftingScreen.y;

            using (var modContentScope = new GUILayout.VerticalScope(GUI.skin.box))
            {
                ScreenMenuBox();
                if (!IsMinimized)
                {
                    ModOptionsBox();
                    ItemsFilterBox();
                    DestroyItemsBox();
                    CraftItemsBox();
                }
            }
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 10000f));
        }

        private void ModOptionsBox()
        {
            if (IsModActiveForSingleplayer || IsModActiveForMultiplayer)
            {
                using (var optionsScope = new GUILayout.VerticalScope(GUI.skin.box))
                {
                    GUILayout.Label($"To toggle the main mod UI, press [{ModKeybindingId}]", GUI.skin.label);

                    MultiplayerOptionBox();
                    ModKeybindingOptionBox();
                    ConstructionsOptionBox();
                }
            }
            else
            {
                OnlyForSingleplayerOrWhenHostBox();
            }
        }

        private void OnlyForSingleplayerOrWhenHostBox()
        {
            using (var infoScope = new GUILayout.HorizontalScope(GUI.skin.box))
            {
                GUI.color = Color.yellow;
                GUILayout.Label(OnlyForSinglePlayerOrHostMessage(), GUI.skin.label);
            }
        }

        private void MultiplayerOptionBox()
        {
            try
            {
                using (var multiplayeroptionsScope = new GUILayout.VerticalScope(GUI.skin.box))
                {
                    GUILayout.Label("Multiplayer options: ", GUI.skin.label);
                    string multiplayerOptionMessage = string.Empty;
                    if (IsModActiveForSingleplayer || IsModActiveForMultiplayer)
                    {
                        GUI.color = Color.green;
                        if (IsModActiveForSingleplayer)
                        {
                            multiplayerOptionMessage = $"you are the game host";
                        }
                        if (IsModActiveForMultiplayer)
                        {
                            multiplayerOptionMessage = $"the game host allowed usage";
                        }
                        _ = GUILayout.Toggle(true, PermissionChangedMessage($"granted", multiplayerOptionMessage), GUI.skin.toggle);
                    }
                    else
                    {
                        GUI.color = Color.yellow;
                        if (!IsModActiveForSingleplayer)
                        {
                            multiplayerOptionMessage = $"you are not the game host";
                        }
                        if (!IsModActiveForMultiplayer)
                        {
                            multiplayerOptionMessage = $"the game host did not allow usage";
                        }
                        _ = GUILayout.Toggle(false, PermissionChangedMessage($"revoked", $"{multiplayerOptionMessage}"), GUI.skin.toggle);
                    }
                }
            }
            catch (Exception exc)
            {
                HandleException(exc, nameof(MultiplayerOptionBox));
            }
        }

        private void ModKeybindingOptionBox()
        {
            using (var modkeybindingScope = new GUILayout.VerticalScope(GUI.skin.box))
            {
                GUI.color = DefaultGuiColor;
                GUILayout.Label("Mod keybinding options: ", GUI.skin.label);
                GUILayout.Label($"To select a item to craft, press [{ModKeybindingId}]", GUI.skin.label);
                GUILayout.Label($"To destroy the target on mouse pointer, press [{ModDeleteKeybindingId}]", GUI.skin.label);
            }
        }

        private void ConstructionsOptionBox()
        {
            try
            {
                using (var constructionsoptionScope = new GUILayout.VerticalScope(GUI.skin.box))
                {
                    GUI.color = DefaultGuiColor;
                    GUILayout.Label($"Construction options: ", GUI.skin.label);
                    DestroyTargetOption = GUILayout.Toggle(DestroyTargetOption, $"Use [{ModDeleteKeybindingId}] to destroy target?", GUI.skin.toggle);
                }
            }
            catch (Exception exc)
            {
                HandleException(exc, nameof(ConstructionsOptionBox));
            }
        }

        private void DestroyItemsBox()
        {
            using (var actionScope = new GUILayout.HorizontalScope(GUI.skin.box))
            {
                GUILayout.Label($"Click to destroy {SelectedFilter.ToString().ToLower()} crafted using this mod.", GUI.skin.label);
                if (GUILayout.Button($"Destroy", GUI.skin.button, GUILayout.MaxWidth(200f)))
                {
                    ShowConfirmDestroyDialog();
                }
            }
        }

        private void ScreenMenuBox()
        {
            if (GUI.Button(new Rect(ModCraftingScreen.width - 40f, 0f, 20f, 20f), "-", GUI.skin.button))
            {
                CollapseWindow();
            }

            if (GUI.Button(new Rect(ModCraftingScreen.width - 20f, 0f, 20f, 20f), "X", GUI.skin.button))
            {
                CloseWindow();
            }
        }

        private void CollapseWindow()
        {
            if (!IsMinimized)
            {
                ModCraftingScreen = new Rect(ModScreenStartPositionX, ModScreenStartPositionY, ModScreenTotalWidth, ModScreenMinHeight);
                IsMinimized = true;
            }
            else
            {
                ModCraftingScreen = new Rect(ModScreenStartPositionX, ModScreenStartPositionY, ModScreenTotalWidth, ModScreenTotalHeight);
                IsMinimized = false;
            }
            InitWindow();
        }

        private void CloseWindow()
        {
            ShowUI = false;
            EnableCursor(false);
        }

        private void CraftItemsBox()
        {
            ItemsScrollViewBox();
        }

        private string[] GetFilters()
        {
            string[] filters = Enum.GetNames(typeof(ItemFilter));
            return filters;
        }

        private string[] GetFilteredItemNames(ItemFilter filter)
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

        private List<ItemInfo> GetMedical() => new List<ItemInfo>
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
                LocalItemsManager.GetInfo(ItemID.Tobacco_Torch)
            };

        private List<Item> GetCraftedItems(ItemFilter filter)
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

        private List<ItemInfo> GetUnique()
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

        private List<ItemInfo> GetResources() => new List<ItemInfo>
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

        private void ItemsFilterBox()
        {
            using (var filterScope = new GUILayout.VerticalScope(GUI.skin.box))
            {
                string[] filters = GetFilters();
                if (filters != null)
                {
                    int filtersCount = filters.Length;
                    GUI.color = Color.cyan;
                    GUILayout.Label("Choose an item filter. If you want to search for items on keyword, type it in the field bellow: ", GUI.skin.label);

                    GUI.color = DefaultGuiColor;
                    SearchItemKeyWord = GUILayout.TextField(SearchItemKeyWord, GUI.skin.textField);
                    SelectedFilterIndex = GUILayout.SelectionGrid(SelectedFilterIndex, filters, filtersCount, GUI.skin.button);
                    if (GUILayout.Button($"Apply filter", GUI.skin.button))
                    {
                        OnClickApplyFilterButton();
                    }
                }
            }
        }

        private void ItemsScrollViewBox()
        {
            using (var itemsViewScope = new GUILayout.VerticalScope(GUI.skin.box))
            {
                FilteredItemsScrollView();
                using (var actionScope = new GUILayout.HorizontalScope(GUI.skin.box))
                {
                    ShouldAddToBackpackOption = GUILayout.Toggle(ShouldAddToBackpackOption, "Add to backpack?", GUI.skin.toggle);
                    GUILayout.Label("Craft how many?: ", GUI.skin.label);
                    ItemCountToCraft = GUILayout.TextField(ItemCountToCraft, GUI.skin.textField, GUILayout.MaxWidth(50f));
                    if (GUILayout.Button($"Craft selected", GUI.skin.button, GUILayout.MaxWidth(200f)))
                    {
                        OnClickCraftSelectedItemButton();
                    }
                }
            }
        }

        private void OnClickApplyFilterButton()
        {
            string[] filters = GetFilters();
            if (filters != null)
            {
                SelectedFilterName = filters[SelectedFilterIndex];
                SelectedFilter = (ItemFilter)Enum.Parse(typeof(ItemFilter), SelectedFilterName);
            }
        }

        private void FilteredItemsScrollView()
        {
            GUI.color = Color.cyan;
            GUILayout.Label($"Items filtered on: {SelectedFilter}", GUI.skin.label);

            GUI.color =DefaultGuiColor;
            GUILayout.Label("Select item to craft: ", GUI.skin.label);

            FilteredItemsScrollViewPosition = GUILayout.BeginScrollView(FilteredItemsScrollViewPosition, GUI.skin.scrollView, GUILayout.MinHeight(300f));
            string[] filteredItemNames = GetFilteredItemNames(SelectedFilter);
            if (filteredItemNames != null)
            {
                SelectedItemToCraftIndex = GUILayout.SelectionGrid(SelectedItemToCraftIndex, filteredItemNames, 3, GUI.skin.button);
            }
            GUILayout.EndScrollView();
        }

        private void OnClickCraftSelectedItemButton()
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

        private void CraftSelectedItem(ItemID itemID)
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
                        LocalPlayer.AddItemToInventory(SelectedItemToCraftItemID.ToString());
                        SelectedItemToCraft = LocalInventoryBackpack.FindItem(SelectedItemToCraftItemID);
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
                           Color.green
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