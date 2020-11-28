using Enums;
using ModCrafting.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ModCrafting
{
    /// <summary>
    /// ModCrafting is a mod for Green Hell, that allows a player to craft any game item
    /// without the needed materials and to destroy any item pointed at with the mouse.
    /// (only in single player mode - Use ModManager for multiplayer).
    /// Enable the mod UI by pressing Home.
    /// </summary>
    public class ModCrafting : MonoBehaviour, IYesNoDialogOwner
    {
        private static ModCrafting Instance;

        private static readonly string ModName = nameof(ModCrafting);
        private static readonly float ModScreenTotalWidth = 850f;
        private static readonly float ModScreenTotalHeight = 500f;
        private static readonly float ModScreenMinHeight = 30f;
        private static readonly float ModScreenMaxHeight = 530f;

        private static bool IsMinimized { get; set; } = false;

        private bool ShowUI = false;

        private static ItemsManager LocalItemsManager;
        private static ConstructionController LocalConstructionController;
        private static HUDManager LocalHUDManager;
        private static Player LocalPlayer;
        private static InventoryBackpack LocalInventoryBackpack;

        public static Rect ModCraftingScreen = new Rect(Screen.width / 2f, Screen.height / 2f, ModScreenTotalWidth, ModScreenTotalHeight);

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

        public bool DestroyTargetOption { get; private set; }

        public bool IsModActiveForMultiplayer { get; private set; }
        public bool IsModActiveForSingleplayer => ReplTools.AmIMaster();

        public static string OnlyForSinglePlayerOrHostMessage() => $"Only available for single player or when host. Host can activate using ModManager.";
        public static string ItemDestroyedMessage(string item) => $"{item} destroyed!";
        public static string ItemNotDestroyedMessage(string item) => $"{item} cannot be destroyed!";
        public static string ItemNotSelectedMessage() => $"Not any item selected to destroy!";
        public static string ItemNotCraftedMessage() => $"Item could not be crafted!";
        public static string ItemCraftedMessage(string item, int count) => $"{count} x {item} crafted!";
        public static string PermissionChangedMessage(string permission) => $"Permission to use mods and cheats in multiplayer was {permission}";
        public static string HUDBigInfoMessage(string message, MessageType messageType, Color? headcolor = null)
            => $"<color=#{ (headcolor != null ? ColorUtility.ToHtmlStringRGBA(headcolor.Value) : ColorUtility.ToHtmlStringRGBA(Color.red))  }>{messageType}</color>\n{message}";

        public void Start()
        {
            ModManager.ModManager.onPermissionValueChanged += ModManager_onPermissionValueChanged;
        }

        private void ModManager_onPermissionValueChanged(bool optionValue)
        {
            IsModActiveForMultiplayer = optionValue;
            ShowHUDBigInfo(
                          (optionValue ?
                            HUDBigInfoMessage(PermissionChangedMessage($"granted"), MessageType.Info, Color.green)
                            : HUDBigInfoMessage(PermissionChangedMessage($"revoked"), MessageType.Info, Color.yellow))
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
            if (Input.GetKeyDown(KeyCode.Home))
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

            if (Input.GetKeyDown(KeyCode.Delete))
            {
                DestroyTarget();
            }
        }

        public void DestroyTarget()
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
                else
                {
                    ShowHUDBigInfo(HUDBigInfoMessage(OnlyForSinglePlayerOrHostMessage(), MessageType.Warning, Color.yellow));
                }
            }
            catch (Exception exc)
            {
                ModAPI.Log.Write($"[{ModName}:{nameof(DestroyTarget)}] throws exception:\n{exc.Message}");
            }
        }

        public void DestroyOnHit(RaycastHit hitInfo)
        {
            try
            {
                if (IsModActiveForSingleplayer || IsModActiveForMultiplayer)
                {
                    GameObject go = hitInfo.collider.transform.gameObject;
                    if (go != null)
                    {
                        SelectedGameObjectToDestroy = go.gameObject;
                        ShowConfirmDestroyDialog();
                    }
                }
                else
                {
                    ShowHUDBigInfo(HUDBigInfoMessage(OnlyForSinglePlayerOrHostMessage(), MessageType.Warning, Color.yellow));
                }
            }
            catch (Exception exc)
            {
                ModAPI.Log.Write($"[{ModName}:{nameof(DestroyOnHit)}] throws exception:\n{exc.Message}");
            }
        }

        private void ShowConfirmDestroyDialog()
        {
            EnableCursor(true);
            string description = $"Are you sure you want to destroy selected { (SelectedGameObjectToDestroy != null ? SelectedGameObjectToDestroy.name : SelectedFilter.ToString().ToLower()) }?";
            YesNoDialog destroyYesNoDialog = GreenHellGame.GetYesNoDialog();
            destroyYesNoDialog.Show(this, DialogWindowType.YesNo, $"{ModName} Info", description, true);
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
                ModAPI.Log.Write($"[{ModName}:{nameof(DestroySelectedItem)}] throws exception:\n{exc.Message}");
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
                ModAPI.Log.Write($"[{ModName}:{nameof(IsDestroyable)}] throws exception:\n{exc.Message}");
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
            ModCraftingScreen = GUILayout.Window(wid, ModCraftingScreen, InitModCraftingScreen, $"{ModName}", GUI.skin.window);
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
            using (var modContentScope = new GUILayout.VerticalScope(GUI.skin.box, GUILayout.ExpandHeight(true), GUILayout.MinHeight(ModScreenMinHeight), GUILayout.MaxHeight(ModScreenMaxHeight)))
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
                using (var optionScope = new GUILayout.VerticalScope(GUI.skin.box))
                {
                    if (IsModActiveForMultiplayer)
                    {
                        GUI.color = Color.green;
                        GUILayout.Label(PermissionChangedMessage($"granted"), GUI.skin.label);
                    }
                    else
                    {
                        GUI.color = Color.yellow;
                        PermissionChangedMessage($"revoked");
                    }
                    GUI.color = Color.white;
                    DestroyTargetOption = GUILayout.Toggle(DestroyTargetOption, $"Use [DELETE] to destroy target?", GUI.skin.toggle);
                }
            }
            else
            {
                using (var infoScope = new GUILayout.VerticalScope(GUI.skin.box))
                {
                    GUI.color = Color.yellow;
                    GUILayout.Label(OnlyForSinglePlayerOrHostMessage(), GUI.skin.label);
                    GUI.color = Color.white;
                }
            }
        }

        private void DestroyItemsBox()
        {
            using (var actionScope = new GUILayout.HorizontalScope(GUI.skin.box))
            {
                GUILayout.Label($"Click to destroy {SelectedFilter.ToString().ToLower()} that were crafted using this mod.", GUI.skin.label);
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
                ModCraftingScreen.Set(ModCraftingScreen.x, Screen.height - ModScreenMinHeight, ModScreenTotalWidth, ModScreenMinHeight);
                IsMinimized = true;
            }
            else
            {
                ModCraftingScreen.Set(ModCraftingScreen.x, Screen.height / ModScreenMinHeight, ModScreenTotalWidth, ModScreenTotalHeight);
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
                    filteredInfos = allInfos.Where(info => info.GetNameToDisplayLocalized().Contains(SearchItemKeyWord)).ToList();
                    break;
                case ItemFilter.Medical:
                    filteredInfos =GetMedical();
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
                    GUI.color = Color.white;
                    GUILayout.TextField(SearchItemKeyWord, GUI.skin.textField);
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
                        CloseWindow();
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
            GUI.color = Color.white;
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
                ModAPI.Log.Write($"[{ModName}:{nameof(OnClickCraftSelectedItemButton)}] throws exception:\n{exc.Message}");
            }
        }

        private void CraftSelectedItem(ItemID itemID)
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

            GameObject prefab = GreenHellGame.Instance.GetPrefab($"{itemID}");
            if (prefab != null)
            {
                for (int i = 0; i < CountToCraft; i++)
                {
                    if (ShouldAddToBackpackOption)
                    {
                        LocalPlayer.AddItemToInventory(SelectedItemToCraftItemID.ToString());
                        SelectedItemToCraft = LocalInventoryBackpack.FindItem(SelectedItemToCraftItemID);
                    }
                    else
                    {
                        SelectedItemToCraft = CreateItem(prefab, true, LocalPlayer.transform.position + LocalPlayer.transform.forward * 1f, LocalPlayer.transform.rotation);
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
            else
            {
                ShowHUDBigInfo(HUDBigInfoMessage(ItemNotCraftedMessage(), MessageType.Info, Color.yellow));
            }
        }

        private Item CreateItem(GameObject prefab, bool im_register, Vector3 position, Quaternion rotation)
        {
            return LocalItemsManager.CreateItem(prefab, im_register, position, rotation);
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