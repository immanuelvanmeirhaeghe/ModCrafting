using Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace ModCrafting
{
    public class ModCrafting : MonoBehaviour, IYesNoDialogOwner
    {
        private static ModCrafting s_Instance;

        private static readonly string ModName = nameof(ModCrafting);

        private bool ShowUI = false;

        public static Rect ModCraftingScreen = new Rect(Screen.width / 4f, Screen.height / 4f, 450f, 150f);

        public static DropdownMenu itemsDropdownList;

        private static ItemsManager itemsManager;

        private static HUDManager hUDManager;

        private static Player player;

        private static CraftingManager craftingManager;

        private static CraftingSkill craftingSkill;

        private static ConstructionController constructionController;

        private static InventoryBackpack inventoryBackpack;

        public static string SelectedItemName;
        public static int SelectedItemIndex;

        public static Item SelectedItemToDestroy;

        public static List<Item> CraftedItems = new List<Item>();

        public bool UseOption { get; private set; }

        public bool IsModActiveForMultiplayer { get; private set; }
        public bool IsModActiveForSingleplayer => ReplTools.AmIMaster();

        public static string ItemDestroyedMessage(string item) => $"{item} was <color=#{ColorUtility.ToHtmlStringRGBA(Color.green)}>destroyed!</color>";

        public static string NoItemSelectedMessage() => $"<color=#{ColorUtility.ToHtmlStringRGBA(Color.yellow)}>No item selected to destroy!</color>";

        public static string PermissionChangedMessage(string permission) => $"Permission to use mods and cheats in multiplayer was {permission}";

        private static string HUDBigInfoMessage(string message) => $"<color=#{ColorUtility.ToHtmlStringRGBA(Color.red)}>System</color>\n{message}";

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
            string header  = $"{ModName} Info";
            string textureName = HUDInfoLogTextureType.Count.ToString();

            HUDBigInfo bigInfo = (HUDBigInfo)hUDManager.GetHUD(typeof(HUDBigInfo));
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
            HUDMessages hUDMessages = (HUDMessages)hUDManager.GetHUD(typeof(HUDMessages));
            hUDMessages.AddMessage(
                $"{localization.Get(localizedTextKey)}  {localization.Get(itemID)}"
                );
        }

        private void EnableCursor(bool blockPlayer = false)
        {
            CursorManager.Get().ShowCursor(blockPlayer, false);

            if (blockPlayer)
            {
                player.BlockMoves();
                player.BlockRotation();
                player.BlockInspection();
            }
            else
            {
                player.UnblockMoves();
                player.UnblockRotation();
                player.UnblockInspection();
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
                                    deleteYesNo.Show(this, DialogWindowType.YesNo, $"{ModName} Info", $"Destroy {SelectedItemToDestroy.m_Info.GetNameToDisplayLocalized()}?", false);
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
                ModAPI.Log.Write($"[{ModName}:{nameof(DestroyMouseTarget)}] throws exception: {exc.Message}");
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
                    itemsManager.AddItemToDestroy(item);
                }
            }
            catch (Exception exc)
            {
                ModAPI.Log.Write($"[{ModName}:{nameof(TryClearItems)}] throws exception: {exc.Message}");
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
            itemsManager = ItemsManager.Get();
            hUDManager = HUDManager.Get();
            player = Player.Get();
            craftingManager = CraftingManager.Get();
            craftingSkill = Skill.Get<CraftingSkill>();
            constructionController = ConstructionController.Get();
            inventoryBackpack = InventoryBackpack.Get();
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

                TryCraftItemBox();
            }
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 10000f));
        }

        private void TryCraftItemBox()
        {
            using (var horizontalScope = new GUILayout.HorizontalScope(GUI.skin.box))
            {
                GUILayout.Label("Select item fom list then click Try craft", GUI.skin.label, GUILayout.MaxWidth(200f));
                SelectedItemIndex = GUILayout.SelectionGrid(SelectedItemIndex, GetItems(), 3, GUI.skin.button);
                if (GUILayout.Button("Try craft", GUI.skin.button))
                {
                    OnClickTryCraftButton();
                    CloseWindow();
                }
            }
        }

        private string[] GetItems()
        {
            string[] itemNames = Enum.GetNames(typeof(ItemID));

            for (int i = 0; i < itemNames.Length; i++)
            {
                string itemName = itemNames[i];
                itemNames[i] = itemName.Replace("_", " ");
            }

            return itemNames;
        }

        private void OnClickTryCraftButton()
        {
            try
            {
                string[] itemNames = GetItems();
                SelectedItemName = itemNames[SelectedItemIndex].Replace(" ", "_");
                ItemID selectedItemID = EnumUtils<ItemID>.GetValue(SelectedItemName);
                Item craftedItem = itemsManager.CreateItem(selectedItemID, true, player.transform.position, player.transform.rotation);
                if (craftedItem != null)
                {
                    CraftedItems.Add(craftedItem);
                    ShowHUDBigInfo($"Created 1 x {craftedItem.m_Info.GetNameToDisplayLocalized()}");
                }
            }
            catch (Exception exc)
            {
                ModAPI.Log.Write($"[{ModName}:{nameof(OnClickTryCraftButton)}] throws exception: {exc.Message}");
            }
        }

        private void CraftBambooRaftBox()
        {
            using (var horizontalScope = new GUILayout.HorizontalScope(GUI.skin.box))
            {
                GUILayout.Label("4 x rope, 5 x long bamboo stick", GUI.skin.label, GUILayout.MaxWidth(200f));
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
                GUILayout.Label("1 x rope, 1 x bamboo bowl", GUI.skin.label, GUILayout.MaxWidth(200f));
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
                GUILayout.Label("20 x rope, 4 x Banesteriopsis vine, 2 x stick, 2 x Brazilian nut", GUI.skin.label, GUILayout.MaxWidth(200));
                if (GUILayout.Button("Craft hammock", GUI.skin.button))
                {
                    OnClickCraftHammockButton();
                    CloseWindow();
                }
            }
        }

        private void ScreenMenuBox()
        {
            if (GUI.Button(new Rect(ModCraftingScreen.width - 20f, 0f, 20f, 20f), "X", GUI.skin.button))
            {
                CloseWindow();
            }
        }

        private void CloseWindow()
        {
            ShowUI = false;
            EnableCursor(false);
        }

        private void OnClickCraftBambooBidonButton()
        {
            try
            {
                Item bambooBidon = CraftBambooContainer();
                if (bambooBidon != null)
                {
                    CraftedItems.Add(bambooBidon);
                    ShowHUDBigInfo($"Created 1 x {bambooBidon.m_Info.GetNameToDisplayLocalized()}");
                }
            }
            catch (Exception exc)
            {
                ModAPI.Log.Write($"[{ModName}:{nameof(OnClickCraftBambooBidonButton)}] throws exception: {exc.Message}");
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
                    ShowHUDBigInfo($"Created 1 x {hammock.m_Info.GetNameToDisplayLocalized()}");
                }
            }
            catch (Exception exc)
            {
                ModAPI.Log.Write($"[{ModName}:{nameof(OnClickCraftHammockButton)}] throws exception: {exc.Message}");
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
                    ShowHUDBigInfo($"Created 1 x {raft.m_Info.GetNameToDisplayLocalized()}");
                }
            }
            catch (Exception exc)
            {
                ModAPI.Log.Write($"[{ModName}:{nameof(OnClickCraftBambooBidonButton)}] throws exception: {exc.Message}");
            }
        }

        private void OnClickCraftBlowgunButton()
        {
            try
            {
                Item blowgun = CraftBambooBlowgun();
                if (blowgun != null)
                {
                    CraftedItems.Add(blowgun);
                    ShowHUDBigInfo($"Created 1 x {blowgun.m_Info.GetNameToDisplayLocalized()}");
                }
            }
            catch (Exception exc)
            {
                ModAPI.Log.Write($"[{ModName}:{nameof(OnClickCraftBlowgunButton)}] throws exception: {exc.Message}");
            }
        }

        private void OnClickCraftBlowgunArrowButton()
        {
            try
            {
                GetMaxThreeBlowpipeArrow();
            }
            catch (Exception exc)
            {
                ModAPI.Log.Write($"[{ModName}:{nameof(OnClickCraftBlowgunArrowButton)}] throws exception: {exc.Message}");
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
                ModAPI.Log.Write($"[{ModName}:{nameof(CraftBambooContainer)}] throws exception: {exc.Message}");
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
                ModAPI.Log.Write($"[{ModName}:{nameof(CraftHammock)}] throws exception: {exc.Message}");
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
                ModAPI.Log.Write($"[{ModName}:{nameof(CraftBambooRaft)}] throws exception: {exc.Message}");
                return raft;
            }
        }

        public Item CraftBambooBlowgun()
        {
            Item blowgunToUse = null;
            try
            {
                blowgunToUse = CreateBambooBlowgun();
                return blowgunToUse;
            }
            catch (Exception exc)
            {
                ModAPI.Log.Write($"[{ModName}:{nameof(CraftBambooBlowgun)}] throws exception: {exc.Message}");
                return blowgunToUse;
            }
        }

        private Item CreateBambooContainer()
        {
            Item bambooContainer = null;
            try
            {
                string m_InfoName = ItemID.Bamboo_Container.ToString();
                GameObject prefab = GreenHellGame.Instance.GetPrefab(m_InfoName);
                bambooContainer = CreateItem(prefab, true, player.transform.position + player.transform.forward * 2f, player.transform.rotation);
                bambooContainer.m_InfoName = m_InfoName;
                ItemInfo m_Info = CreateItemInfo(bambooContainer, ItemID.Bamboo_Container, BambooContainerComponents(), BambooContainerComponentsToReturn(), true, true);
                m_Info.m_CantDestroy = false;
                m_Info.m_UsedForCrafting = false;
                ((LiquidContainerInfo)m_Info).m_LiquidType = LiquidType.Water;
                ((LiquidContainerInfo)m_Info).m_Capacity = 75f;
                ((LiquidContainerInfo)m_Info).m_Amount = 0f;
                bambooContainer.m_Info = (LiquidContainerInfo)m_Info;
                CalcHealth(bambooContainer);
                bambooContainer.Initialize(true);

                player.AddKnownItem(ItemID.Bamboo_Container);
                EventsManager.OnEvent(Enums.Event.Craft, 1, (int)ItemID.Bamboo_Container);
                craftingSkill.OnSkillAction();
                itemsManager.m_CreationsData.Add((int)bambooContainer.m_Info.m_ID, (int)bambooContainer.m_Info.m_ID);
                itemsManager.OnCreateItem(ItemID.Bamboo_Container);
                return bambooContainer;
            }
            catch (Exception exc)
            {
                ModAPI.Log.Write($"[{ModName}:{nameof(CreateBambooContainer)}] throws exception: {exc.Message}");
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
                hammockToUse = CreateItem(prefab, true, player.transform.position + player.transform.forward * 2f + player.transform.up * 1f, player.transform.rotation);
                hammockToUse.m_InfoName = m_InfoName;
                ItemInfo m_Info = CreateItemInfo(hammockToUse, ItemID.village_hammock_a, HammockComponents(), HammockComponentsToReturn(), false, false);
                m_Info.m_CantDestroy = false;
                m_Info.m_CanBeDamaged = true;
                m_Info.m_ReceiveDamageType = (int)DamageType.Melee;
                m_Info.m_UsedForCrafting = false;
                ((ConstructionInfo)m_Info).m_ConstructionType = ConstructionType.Shelter;
                ((ConstructionInfo)m_Info).m_RestingParamsMul = 1f;
                ((ConstructionInfo)m_Info).m_ParamsMulRadius = -0.5f;
                ((ConstructionInfo)m_Info).m_HitsCountToDestroy = 3;
                ((ConstructionInfo)m_Info).m_PlaceToAttachNames = new List<string>
                {
                    ItemID.building_frame.ToString(),
                    ItemID.building_bamboo_frame.ToString()
                };
                ((ConstructionInfo)m_Info).m_PlaceToAttachToNames = new List<string>
                {
                    ItemID.building_wall.ToString(),
                    ItemID.building_bamboo_wall.ToString()
                };
                m_Info.m_Type = ItemType.Construction;
                hammockToUse.m_Info = (ConstructionInfo)m_Info;
                CalcHealth(hammockToUse);
                hammockToUse.Initialize(true);

                player.AddKnownItem(ItemID.village_hammock_a);
                EventsManager.OnEvent(Enums.Event.Craft, 1, (int)ItemID.village_hammock_a);
                craftingSkill.OnSkillAction();
                itemsManager.m_CreationsData.Add((int)hammockToUse.m_Info.m_ID, (int)hammockToUse.m_Info.m_ID);
                itemsManager.OnCreateItem(ItemID.village_hammock_a);
                return hammockToUse;
            }
            catch (Exception exc)
            {
                ModAPI.Log.Write($"[{ModName}:{nameof(CreateHammock)}] throws exception: {exc.Message}");
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
                raft = CreateItem(prefab, true, player.transform.position + player.transform.forward * 2f, player.transform.rotation);
                raft.m_InfoName = m_InfoName;
                ItemInfo m_Info = CreateItemInfo(raft, ItemID.raft, BambooRaftComponents(), BambooRaftComponentsToReturn(), true, true);
                m_Info.m_CantDestroy = false;
                raft.m_Info = m_Info;
                CalcHealth(raft);
                raft.Initialize(true);
                return raft;
            }
            catch (Exception exc)
            {
                ModAPI.Log.Write($"[{ModName}:{nameof(CreateBambooRaft)}] throws exception: {exc.Message}");
                return raft;
            }
        }

        private Item CreateBambooBlowgun()
        {
            Item blowgun = null;
            try
            {
                string m_InfoName = ItemID.Bamboo_Blowpipe.ToString();
                GameObject prefab = GreenHellGame.Instance.GetPrefab(m_InfoName);
                blowgun = CreateItem(prefab, true, player.transform.position + player.transform.forward * 2f, player.transform.rotation);
                blowgun.m_InfoName = m_InfoName;
                ItemInfo m_Info = CreateItemInfo(blowgun, ItemID.Bamboo_Blowpipe, BlowgunComponents(), BlowgunComponentsToReturn(), true, false);
                m_Info.m_CanEquip = true;
                m_Info.m_CantDestroy = false;
                m_Info.m_UsedForCrafting = false;
                ((WeaponInfo)m_Info).m_WeaponType = WeaponType.Blowpipe;
                blowgun.m_Info = (WeaponInfo)m_Info;
                CalcHealth(blowgun);
                blowgun.Initialize(true);

                player.AddKnownItem(ItemID.Bamboo_Blowpipe);
                EventsManager.OnEvent(Enums.Event.Craft, 1, (int)ItemID.Bamboo_Blowpipe);
                craftingSkill.OnSkillAction();
                itemsManager.m_CreationsData.Add((int)blowgun.m_Info.m_ID, (int)blowgun.m_Info.m_ID);
                itemsManager.OnCreateItem(ItemID.Bamboo_Blowpipe);
                return blowgun;
            }
            catch (Exception exc)
            {
                ModAPI.Log.Write($"[{ModName}:{nameof(CreateBambooBlowgun)}] throws exception: {exc.Message}");
                return blowgun;
            }
        }

        private Item CreateItem(GameObject prefab, bool im_register, Vector3 position, Quaternion rotation)
        {
            GameObject gameObject = Instantiate(prefab, position, rotation);
            gameObject.name = prefab.name;
            Item m_Item = gameObject.GetComponent<Item>();
            if (!m_Item)
            {
                DebugUtils.Assert($"[{ModName}:{nameof(CreateItem)}] Missing Item component - {prefab.name}");
                Destroy(gameObject);
                return null;
            }
            return m_Item;
        }

        private ItemInfo CreateItemInfo(Item item, ItemID id, Dictionary<int, int> components, Dictionary<int, int> componentsToReturn, bool canBeAddedToInventory, bool canBePlacedInStorage)
        {
            ItemInfo m_Info = new ItemInfo
            {
                m_CreationTime = (float)MainLevel.Instance.m_TODSky.Cycle.GameTime,
                m_Item = item,
                m_ID = id,
                m_CanBeAddedToInventory = canBeAddedToInventory,
                m_CanBePlacedInStorage = canBePlacedInStorage,
                m_Craftable = true,
                m_Components = components,
                m_ComponentsToReturn = componentsToReturn
            };
            return m_Info;
        }

        private void CalcHealth(Item item)
        {
            float playerHealthMul = craftingSkill.GetPlayerHealthMul();
            float itemHealthMul = craftingSkill.GetItemHealthMul(item);
            float num = Mathf.Clamp01(playerHealthMul + itemHealthMul + craftingSkill.m_InitialHealthMul);
            item.m_Info.m_Health = item.m_Info.m_MaxHealth * num;
            if (item.m_Info.IsTorch())
            {
                ((Torch)item).m_Fuel = num;
            }
        }

        private static Dictionary<int, int> HammockComponents()
        {
            Dictionary<int, int> dictionary = new Dictionary<int, int> { };
            dictionary.Add((int)ItemID.village_hammock_a, 1);
            return dictionary;
        }

        private static Dictionary<int, int> HammockComponentsToReturn()
        {
            Dictionary<int, int> dictionary = new Dictionary<int, int> { };
            dictionary.Add((int)ItemID.Rope, 20);
            dictionary.Add((int)ItemID.banisteriopsis_ToHoldHarvest, 4);
            dictionary.Add((int)ItemID.Stick, 2);
            dictionary.Add((int)ItemID.Brazil_nut, 2);

            return dictionary;
        }

        private static Dictionary<int, int> BambooContainerComponents()
        {
            Dictionary<int, int> dictionary = new Dictionary<int, int> { };
            dictionary.Add((int)ItemID.Bamboo_Container, 1);
            return dictionary;
        }

        private static Dictionary<int, int> BambooContainerComponentsToReturn()
        {
            Dictionary<int, int> dictionary = new Dictionary<int, int> { };
            dictionary.Add((int)ItemID.Rope, 1);
            dictionary.Add((int)ItemID.Bamboo_Bowl, 1);

            return dictionary;
        }

        private static Dictionary<int, int> BambooRaftComponents()
        {
            Dictionary<int, int> dictionary = new Dictionary<int, int> { };
            dictionary.Add((int)ItemID.raft, 1);
            return dictionary;
        }

        private static Dictionary<int, int> BambooRaftComponentsToReturn()
        {
            Dictionary<int, int> dictionary = new Dictionary<int, int> { };
            dictionary.Add((int)ItemID.Rope, 4);
            dictionary.Add((int)ItemID.Bamboo_Long_Stick, 5);

            return dictionary;
        }

        private static Dictionary<int, int> BlowgunComponents()
        {
            Dictionary<int, int> dictionary = new Dictionary<int, int> { };
            dictionary.Add((int)ItemID.Bamboo_Blowpipe, 1);
            return dictionary;
        }

        private static Dictionary<int, int> BlowgunComponentsToReturn()
        {
            Dictionary<int, int> dictionary = new Dictionary<int, int> { };
            dictionary.Add((int)ItemID.Rope, 1);
            dictionary.Add((int)ItemID.Bamboo_Long_Stick, 1);
            return dictionary;
        }

        private static List<Item> GetHammockCraftingitems()
        {
            List<Item> list = new List<Item>
            {
                itemsManager.GetInfo(ItemID.Rope).m_Item,
                itemsManager.GetInfo(ItemID.Palm_Leaf).m_Item,
                itemsManager.GetInfo(ItemID.Long_Stick).m_Item
            };
            return list;
        }

        private static List<Item> GetBambooContainerCraftingitems()
        {
            List<Item> list = new List<Item>
            {
                itemsManager.GetInfo(ItemID.Rope).m_Item,
                itemsManager.GetInfo(ItemID.Bamboo_Bowl).m_Item
            };
            return list;
        }

        private static List<Item> GetBambooBowlCraftingitems()
        {
            List<Item> list = new List<Item>
            {
                itemsManager.GetInfo(ItemID.Bamboo_Log).m_Item
            };
            return list;
        }

        private static List<Item> GetBlowpipeCraftingitems()
        {
            List<Item> list = new List<Item>
            {
                itemsManager.GetInfo(ItemID.Rope).m_Item,
                 itemsManager.GetInfo(ItemID.Bamboo_Long_Stick).m_Item
            };
            return list;
        }

        public void GetMaxThreeBlowpipeArrow(int count = 1)
        {
            try
            {
                if (count > 3)
                {
                    count = 3;
                }
                itemsManager.UnlockItemInfo(ItemID.Blowpipe_Arrow.ToString());
                ItemInfo blowPipeArrowItemInfo = itemsManager.GetInfo(ItemID.Blowpipe_Arrow);
                for (int i = 0; i < count; i++)
                {
                    player.AddItemToInventory(blowPipeArrowItemInfo.m_ID.ToString());
                }
                ShowHUDBigInfo($"Added {count} x {blowPipeArrowItemInfo.GetNameToDisplayLocalized()} to inventory");
            }
            catch (Exception exc)
            {
                ModAPI.Log.Write($"[{ModName}:{nameof(GetMaxThreeBlowpipeArrow)}] throws exception: {exc.Message}");
            }
        }

        private void AddCraftingItem(Item item, int count = 1)
        {
            try
            {
                for (int i = 0; i < count; i++)
                {
                    craftingManager.AddItem(item, false);
                }
            }
            catch (Exception exc)
            {
                ModAPI.Log.Write($"[{ModName}:{nameof(AddCraftingItem)}] throws exception: {exc.Message}");
            }
        }

        private bool CanEquipItem
        {
            get
            {
                if (HarvestingAnimalController.Get().IsActive())
                {
                    return false;
                }
                if (MudMixerController.Get().IsActive())
                {
                    return false;
                }
                if (HarvestingSmallAnimalController.Get().IsActive())
                {
                    return false;
                }
                if (FishingController.Get().IsActive() && !FishingController.Get().CanHideRod())
                {
                    return false;
                }
                if (player.m_Animator.GetBool(player.m_CleanUpHash))
                {
                    return false;
                }
                if (ScenarioManager.Get().IsBoolVariableTrue("PlayerMechGameEnding"))
                {
                    return false;
                }
                return true;
            }
        }

        public void TryEquipBlowpipe()
        {
            try
            {
                ItemSlot equippedSlot = null;
                for (int i = 0; i < 4; i++)
                {
                    equippedSlot = inventoryBackpack.GetSlotByIndex(i, BackpackPocket.Left);
                    if (equippedSlot != null && equippedSlot.m_Item.m_Info.m_ID == ItemID.Bamboo_Blowpipe)
                    {
                        inventoryBackpack.m_EquippedItem = equippedSlot.m_Item;
                        player.Equip(equippedSlot);
                    }
                }

                if (equippedSlot != null)
                {
                    ShowHUDBigInfo($"Blowpipe has been equipped!");
                }
                else
                {
                    ShowHUDBigInfo($"Cannot find blowpipe to equip!");
                }
            }
            catch (Exception exc)
            {
                ModAPI.Log.Write($"[{ModName}:{nameof(TryEquipBlowpipe)}] throws exception: {exc.Message}");
            }
        }

        public void OnYesFromDialog()
        {
            if (SelectedItemToDestroy != null)
            {
                SelectedItemToDestroy.TakeDamage(new DamageInfo { m_Damage = 100f, m_CriticalHit = true, m_DamageType = DamageType.Melee });
                itemsManager.AddItemToDestroy(SelectedItemToDestroy);
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