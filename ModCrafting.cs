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
        private static ModCrafting s_Instance;

        private static readonly string ModName = nameof(ModCrafting);
        private static readonly float ModScreenWidth = 750f;
        private static readonly float ModScreenHeight = 430f;
        private static bool IsMinimized { get; set; } = false;
        private static bool LocalOptionState { get; set; }
        private bool ShowUI = false;

        public static Rect ModCraftingScreen = new Rect(Screen.width / 40f, Screen.height / 40f, ModScreenWidth, ModScreenHeight);

        public static Vector2 FilteredItemsScrollViewPosition;

        private static ItemsManager LocalItemsManager;

        private static HUDManager LocalHUDManager;

        private static Player LocalPlayer;

        private static InventoryBackpack LocalInventoryBackpack;

        public static string SelectedItemToCraftItemName;
        public static int SelectedItemToCraftIndex;
        public static ItemID SelectedItemToCraftItemID;
        public static Item SelectedItemToCraft;
        public static Item SelectedItemToDestroy;

        public static List<Item> CraftedItems = new List<Item>();

        public bool IsModActiveForMultiplayer { get; private set; }
        public bool IsModActiveForSingleplayer => ReplTools.AmIMaster();

        public string SelectedFilterName { get; private set; }
        public int SelectedFilterIndex { get; private set; }
        public ItemFilter SelectedFilter { get; private set; } = ItemFilter.All;

        public static string ItemDestroyedMessage(string item) => $"<color=#{ColorUtility.ToHtmlStringRGBA(Color.green)}>{item} destroyed!</color>";

        public static string NoItemSelectedMessage() => $"<color=#{ColorUtility.ToHtmlStringRGBA(Color.yellow)}>No item selected to destroy!</color>";

        public static string NoItemCraftedMessage() => $"<color=#{ColorUtility.ToHtmlStringRGBA(Color.yellow)}>Item could not be crafted!</color>";

        public static string ItemCraftedMessage(string item, int count) => $"Crafted {count} x {item}";

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
            s_Instance = this;
        }

        public static ModCrafting Get()
        {
            return s_Instance;
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
                                    EnableCursor(true);
                                    SelectedItemToDestroy = item;
                                    YesNoDialog deleteYesNo = GreenHellGame.GetYesNoDialog();
                                    deleteYesNo.Show(this, DialogWindowType.YesNo, $"{ModName} Info", $"Destroy {item.m_Info.GetNameToDisplayLocalized()}?", false);
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

        public static string OnlyForSinglePlayerOrHostMessage()
            => $"\n<color=#{ColorUtility.ToHtmlStringRGBA(Color.yellow)}>DELETE option</color> is only available for single player or when host.\nHost can activate using <b>ModManager</b>.";

        private void ToggleShowUI()
        {
            ShowUI = !ShowUI;
        }

        private void TryClearItems()
        {
            try
            {
                foreach (Item item in CraftedItems)
                {
                    LocalItemsManager.AddItemToDestroy(item);
                }
            }
            catch (Exception exc)
            {
                ModAPI.Log.Write($"[{ModName}:{nameof(TryClearItems)}] throws exception:\n{exc.Message}");
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
                CraftHammockBox();
                CraftBambooContainerBox();
                CraftBambooRaftBox();
                CraftSelectedItemBox();
            }
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 10000f));
        }

        private void ScreenMenuBox()
        {
            if (GUI.Button(new Rect(ModCraftingScreen.width - 40f, 0f, 20f, 20f), "_", GUI.skin.button))
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

        private void CraftBambooRaftBox()
        {
            using (var horizontalScope = new GUILayout.HorizontalScope(GUI.skin.box))
            {
                if (GUILayout.Button("Craft bamboo raft", GUI.skin.button))
                {
                    OnClickCraftBambooRaftButton();
                    CloseWindow();
                }
            }
        }

        private void CraftBambooContainerBox()
        {
            using (var horizontalScope = new GUILayout.HorizontalScope(GUI.skin.box))
            {
                if (GUILayout.Button("Craft bamboo container", GUI.skin.button))
                {
                    OnClickCraftBambooBidonButton();
                    CloseWindow();
                }
            }
        }

        private void CraftHammockBox()
        {
            using (var horizontalScope = new GUILayout.HorizontalScope(GUI.skin.box))
            {
                if (GUILayout.Button("Craft village hammock A", GUI.skin.button))
                {
                    OnClickCraftHammockButton();
                    CloseWindow();
                }
            }
        }

        private void CraftSelectedItemBox()
        {
            ItemFilterBox();
            ItemViewBox();
        }

        private string[] GetFilters()
        {
            string[] filters = default;
            int filterIdx = 0;
            EnumUtils<ItemFilter>.ForeachName(
                filterName =>
                {
                    filters[filterIdx] = filterName;
                    filterIdx++;
                });
            return filters;
        }

        private string[] GetFilteredItems(ItemFilter filter)
        {
            int itemNameIdx = 0;
            string[] filteredItemNames = default;
            List<ItemInfo> allItemInfos = LocalItemsManager.GetAllInfos().Values.ToList();
            List<ItemInfo> filteredInfos = default;
            switch (filter)
            {
                case ItemFilter.Resources:
                    filteredInfos = allItemInfos.Where(info => info.IsStone() || info.IsSeed() || info.IsMeat() || info.IsFood() || info.IsConsumable() || info.IsDressing() || info.IsHeavyObject()
                                                                                  || info.m_ID.IsLeaf() || info.m_ID.IsPlant() || info.m_ID.IsTree()
                                                                                  || info.m_Item.IsFish() || info.m_Item.IsLiquidSource()).ToList();
                    break;
                case ItemFilter.Construction:
                    filteredInfos = allItemInfos.Where(info => info.IsConstruction()).ToList();
                    break;
                case ItemFilter.Tools:
                    filteredInfos = allItemInfos.Where(info => info.IsTool()).ToList();
                    break;
                case ItemFilter.Weapons:
                    filteredInfos = allItemInfos.Where(info => info.IsWeapon()).ToList();
                    break;
                case ItemFilter.Armor:
                    filteredInfos = allItemInfos.Where(info => info.IsArmor()).ToList();
                    break;
                case ItemFilter.All:
                default:
                    filteredInfos = allItemInfos;
                    break;
            }

            foreach (ItemInfo filteredInfo in filteredInfos)
            {
                string filteredItemName = filteredInfo.m_ID.ToString();
                filteredItemNames[itemNameIdx] = filteredItemName.Replace("_", " ");
            }
            return filteredItemNames;
        }

        private void ItemFilterBox()
        {
            using (var contentScope = new GUILayout.VerticalScope(GUI.skin.box))
            {
                string[] filters = GetFilters();
                if (filters != null)
                {
                    int filtersCount = filters.Length;
                    GUILayout.Label("Select filter: ", GUI.skin.label);
                    SelectedFilterIndex = GUILayout.SelectionGrid(SelectedFilterIndex, filters, filtersCount, GUI.skin.button);
                    if (GUILayout.Button($"Filter items", GUI.skin.button))
                    {
                        OnClickFilterItemsButton();
                        CloseWindow();
                    }
                }
            }
        }

        private void ItemViewBox()
        {
            using (var itemsContentScope = new GUILayout.VerticalScope(GUI.skin.box))
            {
                GUILayout.Label("Select item: ", GUI.skin.label);
                FilteredItemsScrollView();
                if (GUILayout.Button($"Craft selected", GUI.skin.button))
                {
                    OnClickCraftSelectedItemButton();
                    CloseWindow();
                }
            }
        }

        private void OnClickFilterItemsButton()
        {
            string[] filters = GetFilters();
            if (filters != null)
            {
                SelectedFilterName = filters[SelectedFilterIndex];
                SelectedFilter = EnumUtils<ItemFilter>.GetValue(SelectedFilterName);
            }
        }

        private void FilteredItemsScrollView()
        {
            FilteredItemsScrollViewPosition = GUILayout.BeginScrollView(FilteredItemsScrollViewPosition, GUI.skin.scrollView, GUILayout.MinHeight(300f));
            string[] filteredItemNames = GetFilteredItems(SelectedFilter);
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
                string[] filteredItemNames = GetFilteredItems(SelectedFilter);
                SelectedItemToCraftItemName = filteredItemNames[SelectedItemToCraftIndex].Replace(" ", "_");
                SelectedItemToCraftItemID = EnumUtils<ItemID>.GetValue(SelectedItemToCraftItemName);
                GameObject prefab = GreenHellGame.Instance.GetPrefab(SelectedItemToCraftItemName);
                if (prefab != null)
                {
                    SelectedItemToCraft = CreateItem(prefab, true, LocalPlayer.transform.position + LocalPlayer.transform.forward * 2f, LocalPlayer.transform.rotation);
                    if (SelectedItemToCraft != null)
                    {
                        CraftedItems.Add(SelectedItemToCraft);
                        ShowHUDBigInfo(
                            HUDBigInfoMessage(
                                ItemCraftedMessage(SelectedItemToCraft.m_Info.GetNameToDisplayLocalized(), 1)
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
            }
            catch (Exception exc)
            {
                ModAPI.Log.Write($"[{ModName}:{nameof(OnClickCraftSelectedItemButton)}] throws exception:\n{exc.Message}");
            }
        }

        private void OnClickCraftBambooBidonButton()
        {
            try
            {
                Item bambooBidon = CraftBambooContainer();
                if (bambooBidon != null)
                {
                    CraftedItems.Add(bambooBidon);
                    ShowHUDBigInfo(
                        HUDBigInfoMessage(
                            ItemCraftedMessage(bambooBidon.m_Info.GetNameToDisplayLocalized(), 1)
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
                ModAPI.Log.Write($"[{ModName}:{nameof(OnClickCraftBambooBidonButton)}] throws exception:\n{exc.Message}");
            }
        }

        private void OnClickCraftHammockButton()
        {
            try
            {
                Item hammock = CraftHammock();
                if (hammock != null)
                {
                    CraftedItems.Add(hammock);
                    ShowHUDBigInfo(
                        HUDBigInfoMessage(
                            ItemCraftedMessage(hammock.m_Info.GetNameToDisplayLocalized(), 1)
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
                ModAPI.Log.Write($"[{ModName}:{nameof(OnClickCraftHammockButton)}] throws exception:\n{exc.Message}");
            }
        }

        private void OnClickCraftBambooRaftButton()
        {
            try
            {
                Item raft = CraftBambooRaft();
                if (raft != null)
                {
                    CraftedItems.Add(raft);
                    ShowHUDBigInfo(
                       HUDBigInfoMessage(
                           ItemCraftedMessage(raft.m_Info.GetNameToDisplayLocalized(), 1)
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
                ModAPI.Log.Write($"[{ModName}:{nameof(OnClickCraftBambooRaftButton)}] throws exception:\n{exc.Message}");
            }
        }

        public Item CraftBambooContainer()
        {
            Item bambooContainerToUse = null;
            try
            {
                bambooContainerToUse = CreateBambooContainer();
                return bambooContainerToUse;
            }
            catch (Exception exc)
            {
                ModAPI.Log.Write($"[{ModName}:{nameof(CraftBambooContainer)}] throws exception:\n{exc.Message}");
                return bambooContainerToUse;
            }
        }

        public Item CraftHammock()
        {
            Item hammockToUse = null;
            try
            {
                hammockToUse = CreateHammock();
                return hammockToUse;
            }
            catch (Exception exc)
            {
                ModAPI.Log.Write($"[{ModName}:{nameof(CraftHammock)}] throws exception:\n{exc.Message}");
                return hammockToUse;
            }
        }

        public Item CraftBambooRaft()
        {
            Item raft = null;
            try
            {
                raft = CreateBambooRaft();
                return raft;
            }
            catch (Exception exc)
            {
                ModAPI.Log.Write($"[{ModName}:{nameof(CraftBambooRaft)}] throws exception:\n{exc.Message}");
                return raft;
            }
        }

        private Item CreateBambooContainer()
        {
            Item bambooContainer = null;
            try
            {
                string m_InfoName = ItemID.Bamboo_Container.ToString();
                GameObject prefab = GreenHellGame.Instance.GetPrefab(m_InfoName);
                bambooContainer = CreateItem(prefab, true, LocalPlayer.transform.position + LocalPlayer.transform.forward * 2f, LocalPlayer.transform.rotation);
                return bambooContainer;
            }
            catch (Exception exc)
            {
                ModAPI.Log.Write($"[{ModName}:{nameof(CreateBambooContainer)}] throws exception:\n{exc.Message}");
                return bambooContainer;
            }
        }

        private Item CreateHammock()
        {
            Item hammockToUse = null;
            try
            {
                string m_InfoName = ItemID.village_hammock_a.ToString();
                GameObject prefab = GreenHellGame.Instance.GetPrefab(m_InfoName);
                hammockToUse = CreateItem(prefab, true, LocalPlayer.transform.position + LocalPlayer.transform.forward * 2f + LocalPlayer.transform.up * 1f, LocalPlayer.transform.rotation);
                return hammockToUse;
            }
            catch (Exception exc)
            {
                ModAPI.Log.Write($"[{ModName}:{nameof(CreateHammock)}] throws exception:\n{exc.Message}");
                return hammockToUse;
            }
        }

        private Item CreateBambooRaft()
        {
            Item raft = null;
            try
            {
                string m_InfoName = ItemID.raft.ToString();
                GameObject prefab = GreenHellGame.Instance.GetPrefab(m_InfoName);
                raft = CreateItem(prefab, true, LocalPlayer.transform.position + LocalPlayer.transform.forward * 2f, LocalPlayer.transform.rotation);
                return raft;
            }
            catch (Exception exc)
            {
                ModAPI.Log.Write($"[{ModName}:{nameof(CreateBambooRaft)}] throws exception:\n{exc.Message}");
                return raft;
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
                if (SelectedItemToDestroy.m_Info.IsConstruction())
                {
                    SelectedItemToDestroy.TakeDamage(new DamageInfo { m_Damage = 100f, m_CriticalHit = true, m_DamageType = DamageType.Melee });
                }
                LocalItemsManager.AddItemToDestroy(SelectedItemToDestroy);
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