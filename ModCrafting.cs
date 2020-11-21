using Enums;
using ModCrafting.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ModCrafting
{
    public class ModCrafting : MonoBehaviour, IYesNoDialogOwner
    {
        private static ModCrafting Instance;

        private static readonly string ModName = nameof(ModCrafting);
        private static readonly float ModScreenWidth = 850f;
        private static readonly float ModScreenHeight = 500f;
        private static bool IsMinimized { get; set; } = false;

        private bool ShowUI = false;

        private static ItemsManager LocalItemsManager;
        private static HUDManager LocalHUDManager;
        private static Player LocalPlayer;
        private static InventoryBackpack LocalInventoryBackpack;

        public static Rect ModCraftingScreen = new Rect(Screen.width / 40f, Screen.height / 40f, ModScreenWidth, ModScreenHeight);
        public static Vector2 FilteredItemsScrollViewPosition;
        public static string SelectedItemToCraftItemName;
        public static int SelectedItemToCraftIndex;
        public static ItemID SelectedItemToCraftItemID;
        public static Item SelectedItemToCraft;
        public static Item SelectedItemToDestroy;
        public static string SelectedFilterName;
        public static int SelectedFilterIndex;
        public static ItemFilter SelectedFilter = ItemFilter.All;
        public static string ItemCountToCraft = "1";
        public static bool ShouldAddToBackpackOption = true;
        public static List<Item> CraftedItems = new List<Item>();

        public bool IsModActiveForMultiplayer { get; private set; }
        public bool IsModActiveForSingleplayer => ReplTools.AmIMaster();

        public static string ItemDestroyedMessage(string item) => $"<color=#{ColorUtility.ToHtmlStringRGBA(Color.green)}>{item} destroyed!</color>";

        public static string NoItemSelectedMessage() => $"<color=#{ColorUtility.ToHtmlStringRGBA(Color.yellow)}>No item selected to destroy!</color>";

        public static string NoItemCraftedMessage() => $"<color=#{ColorUtility.ToHtmlStringRGBA(Color.yellow)}>Item could not be crafted!</color>";

        public static string ItemCraftedMessage(string item, int count) => $"<color=#{ColorUtility.ToHtmlStringRGBA(Color.green)}>{count} x {item} crafted!</color>";

        public static string PermissionChangedMessage(string permission) => $"Permission to use mods and cheats in multiplayer was {permission}";

        public static string HUDBigInfoMessage(string message) => $"<color=#{ColorUtility.ToHtmlStringRGBA(Color.red)}>System</color>\n{message}";

        public void Start()
        {
            ModManager.ModManager.onPermissionValueChanged += ModManager_onPermissionValueChanged;
        }

        private void ModManager_onPermissionValueChanged(bool optionValue)
        {
            IsModActiveForMultiplayer = optionValue;
            ShowHUDBigInfo(
                          (optionValue ?
                            HUDBigInfoMessage(PermissionChangedMessage($"<color=#{ColorUtility.ToHtmlStringRGBA(Color.green)}>granted!</color>"))
                            : HUDBigInfoMessage(PermissionChangedMessage($"<color=#{ColorUtility.ToHtmlStringRGBA(Color.yellow)}>revoked!</color>")))
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
            HUDBigInfoData.s_Duration = 5f;
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
                DestroyMouseTarget();
            }
        }

        public void DestroyMouseTarget()
        {
            try
            {
                if (IsModActiveForSingleplayer || IsModActiveForMultiplayer)
                {
                    if (Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out RaycastHit hitInfo))
                    {
                        GameObject go = hitInfo.collider.transform.gameObject;
                        if (go != null)
                        {
                            Item item = go.GetComponent<Item>();
                            if (item != null)
                            {
                                if (!item.IsPlayer() && !item.IsAI() && !item.IsHumanAI())
                                {
                                    SelectedItemToDestroy = item;
                                    ShowConfirmDestroyDialog(item);
                                }
                            }
                        }
                    }
                }
                else
                {
                    ShowHUDBigInfo(OnlyForSinglePlayerOrHostMessage());
                }
            }
            catch (Exception exc)
            {
                ModAPI.Log.Write($"[{ModName}:{nameof(DestroyMouseTarget)}] throws exception:\n{exc.Message}");
            }
        }

        private void ShowConfirmDestroyDialog(Item item)
        {
            EnableCursor(true);
            YesNoDialog deleteYesNo = GreenHellGame.GetYesNoDialog();
            deleteYesNo.Show(this, DialogWindowType.YesNo, $"{ModName} Info", $"Destroy {item.m_Info.GetNameToDisplayLocalized()}?", false);
        }

        public static string OnlyForSinglePlayerOrHostMessage()
            => $"\n<color=#{ColorUtility.ToHtmlStringRGBA(Color.yellow)}>DELETE option</color> is only available for single player or when host.\nHost can activate using <b>ModManager</b>.";

        private void ToggleShowUI()
        {
            ShowUI = !ShowUI;
        }

        private void DestroyItem(Item itemTodestroy = null, bool allCrafted = false)
        {
            try
            {
                if (itemTodestroy != null)
                {
                    LocalItemsManager.AddItemToDestroy(itemTodestroy);
                }

                if (allCrafted)
                {
                    if (CraftedItems != null)
                    {
                        foreach (Item craftedItem in CraftedItems)
                        {
                            LocalItemsManager.AddItemToDestroy(craftedItem);
                        }
                        CraftedItems.Clear();
                    }
                }
            }
            catch (Exception exc)
            {
                ModAPI.Log.Write($"[{ModName}:{nameof(DestroyItem)}] throws exception:\n{exc.Message}");
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
            using (var verticalScope = new GUILayout.VerticalScope(GUI.skin.box))
            {
                ScreenMenuBox();
                DestroyItemsBox();
                CraftItemsBox();
            }
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 10000f));
        }

        private void DestroyItemsBox()
        {
            using (var actionScope = new GUILayout.HorizontalScope(GUI.skin.box))
            {
                GUILayout.Label("Click to destroy all items that were crafted using this mod.", GUI.skin.label);
                if (GUILayout.Button("Destroy all", GUI.skin.button, GUILayout.MaxWidth(200f)))
                {
                    DestroyItem(null, true);
                    ShowHUDBigInfo(
                       HUDBigInfoMessage(
                           ItemDestroyedMessage(
                               "All items that were crafted using this mod"
                           )
                       )
                   );
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
                ModCraftingScreen.Set(ModCraftingScreen.x, ModCraftingScreen.y, ModScreenWidth, 30f);
                IsMinimized = true;
            }
            else
            {
                ModCraftingScreen.Set(ModCraftingScreen.x, ModCraftingScreen.y, ModScreenWidth, ModScreenHeight);
                IsMinimized = false;
            }
        }

        private void CloseWindow()
        {
            ShowUI = false;
            EnableCursor(false);
        }

        private void CraftItemsBox()
        {
            ItemsFilterBox();
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
                LocalItemsManager.GetInfo(ItemID.Wood_Resin),
                LocalItemsManager.GetInfo(ItemID.bag_lootable),
                LocalItemsManager.GetInfo(ItemID.Brazil_nut),
                LocalItemsManager.GetInfo(ItemID.Coconut),
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
                    GUILayout.Label("Choose an item filter: ", GUI.skin.label);
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
            using (var viewScope = new GUILayout.VerticalScope(GUI.skin.box))
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
                if (string.IsNullOrEmpty(ItemCountToCraft) || !int.TryParse(ItemCountToCraft, out int CountToCraft))
                {
                    CountToCraft = 1;
                    ItemCountToCraft = "1";
                }
                string[] filteredItemNames = GetFilteredItemNames(SelectedFilter);
                SelectedItemToCraftItemName = filteredItemNames[SelectedItemToCraftIndex].Replace(" ", "_");
                SelectedItemToCraftItemID = (ItemID)Enum.Parse(typeof(ItemID), SelectedItemToCraftItemName);
                GameObject prefab = GreenHellGame.Instance.GetPrefab(SelectedItemToCraftItemName);
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
                               ItemCraftedMessage(LocalItemsManager.GetInfo(SelectedItemToCraftItemID).GetNameToDisplayLocalized(), CountToCraft)
                           )
                       );
                }
                else
                {
                    ShowHUDBigInfo(
                        HUDBigInfoMessage(
                            NoItemCraftedMessage()
                        )
                    );
                }
            }
            catch (Exception exc)
            {
                ModAPI.Log.Write($"[{ModName}:{nameof(OnClickCraftSelectedItemButton)}] throws exception:\n{exc.Message}");
            }
        }

        private Item CreateItem(GameObject prefab, bool im_register, Vector3 position, Quaternion rotation)
        {
            return LocalItemsManager.CreateItem(prefab, im_register, position, rotation);
        }

        public void OnYesFromDialog()
        {
            if (SelectedItemToDestroy != null)
            {
               DestroyItem(SelectedItemToDestroy, false);
                ShowHUDBigInfo(
                    HUDBigInfoMessage(
                        ItemDestroyedMessage(
                            SelectedItemToDestroy.m_Info.GetNameToDisplayLocalized()
                        )
                    )
                );
            }
            else
            {
                ShowHUDBigInfo(
                   HUDBigInfoMessage(
                       NoItemSelectedMessage()
                   )
                );
            }
            EnableCursor(false);
        }

        public void OnNoFromDialog()
        {
            SelectedItemToDestroy = null;
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