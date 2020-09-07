using Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ModCrafting
{
    /// <summary>
    /// ModConstructions is a mod for Green Hell
	/// (only in single player mode - Use ModManager for multiplayer).
    /// Enable the mod UI by pressing END.
    /// </summary>
    public class ModCrafting : MonoBehaviour
    {
        private static ModCrafting s_Instance;

        private static readonly string ModName = nameof(ModCrafting);

        private bool showUI = false;

        public Rect ModCraftingScreen = new Rect(150f, 500f, 450f, 150f);

        private static ItemsManager itemsManager;

        private static HUDManager hUDManager;

        private static Player player;

        private static CraftingManager craftingManager;

        private static CraftingSkill craftingSkill;

        private static ConstructionController constructionController;

        private static InventoryBackpack inventoryBackpack;

        public bool UseOption { get; private set; }

        public bool IsModActiveForMultiplayer => FindObjectOfType(typeof(ModManager.ModManager)) != null && ModManager.ModManager.AllowModsForMultiplayer;

        public bool IsModActiveForSingleplayer => ReplTools.AmIMaster();

        public ModCrafting()
        {
            useGUILayout = true;
            s_Instance = this;
        }

        public static ModCrafting Get()
        {
            return s_Instance;
        }

        public void ShowHUDBigInfo(string text, string header, string textureName)
        {
            HUDBigInfo bigInfo = (HUDBigInfo)hUDManager.GetHUD(typeof(HUDBigInfo));
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
            player = Player.Get();

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
                if (!showUI)
                {
                    InitData();
                    EnableCursor(true);
                }
                // toggle menu
                showUI = !showUI;
                if (!showUI)
                {
                    EnableCursor(false);
                }
            }
            if (GreenHellGame.DEBUG && Input.GetKeyDown(KeyCode.Delete))
            {
                InitData();
                PrintDebugActions();
                ShowHUDBigInfo($"Debug action info was printed.", $"{ModName}  Info", HUDInfoLogTextureType.Count.ToString());
                TryClearItems();
                ShowHUDBigInfo($"All items were cleared.", $"{ModName}  Info", HUDInfoLogTextureType.Count.ToString());
            }
        }

        private void TryClearItems()
        {
            try
            {
                var list = itemsManager.GetAllInfos().Values.Where(info =>
                                                                                                                                    !info.m_Item.IsImmutableSceneObject()
                                                                                                                              && !info.m_Item.IsPlayer()
                                                                                                                              && !info.m_Item.IsHumanAI()
                                                                                                                              && !info.m_Item.IsAI()
                                                                                                                              && info.m_CanBeDamaged
                                                                                                                              && info.IsDestroyableObject()
                                                                                                                            );
                foreach (ItemInfo itemInfo in list)
                {
                    Destroy(itemInfo.m_Item.gameObject);
                }
            }
            catch (Exception exc)
            {
                ModAPI.Log.Write($"[{ModName}.{ModName}:{nameof(TryClearItems)}] throws exception: {exc.Message}");
            }
        }

        private void PrintDebugActions()
        {
            StringBuilder printed = new StringBuilder("PLAYER INPUT ACTIONS");
            try
            {
                List<int> playerInputActions = new List<int>();
                player.GetInputActions(ref playerInputActions);
                foreach (int action in playerInputActions)
                {
                    printed.AppendLine($"\nAction\t{action}");
                }
                printed.AppendLine($"\nPLAYERCONTROLLER INPUT ACTIONS");
                PlayerController blowgunController = player.GetController(PlayerControllerType.Blowpipe);
                printed.AppendLine($"\n(int)PlayerControllerType.Blowpipe\t{(int)PlayerControllerType.Blowpipe}");
                List<int> blowgunControllerInputActions = new List<int>();
                blowgunController.GetInputActions(ref blowgunControllerInputActions);
                foreach (int action in blowgunControllerInputActions)
                {
                    printed.AppendLine($"\nAction\t{action}");
                }
                ModAPI.Log.Write($"{printed}");
            }
            catch (Exception exc)
            {
                ModAPI.Log.Write($"[{ModName}.{ModName}:{nameof(PrintDebugActions)}] throws exception: {exc.Message}");
            }
        }

        private void OnGUI()
        {
            if (showUI)
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

        private void CreateMultiplayerOption()
        {
            if (IsModActiveForSingleplayer || IsModActiveForMultiplayer)
            {
                GUI.Label(new Rect(30f, 70f, 200f, 20f), "Use OptionFeature", GUI.skin.label);
                UseOption = GUI.Toggle(new Rect(280f, 70f, 20f, 20f), UseOption, "");
            }
            else
            {
                GUI.Label(new Rect(30f, 70f, 330f, 20f), "Use OptionFeature", GUI.skin.label);
                GUI.Label(new Rect(30f, 90f, 330f, 20f), "is only for single player or when host", GUI.skin.label);
                GUI.Label(new Rect(30f, 110f, 330f, 20f), "Host can activate using ModManager.", GUI.skin.label);
            }
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
                if (GUI.Button(new Rect(430f, 0f, 20f, 20f), "X", GUI.skin.button))
                {
                    CloseWindow();
                }

                using (var horizontalScope = new GUILayout.HorizontalScope(GUI.skin.box))
                {
                    GUILayout.Label("4 x rope, 3 x palm leave, 3 x long stick", GUI.skin.label);
                    if (GUILayout.Button("Craft hammock", GUI.skin.button, GUILayout.MinWidth(100f), GUILayout.MaxWidth(200f)))
                    {
                        OnClickCraftHammockButton();
                        CloseWindow();
                    }
                }

                using (var horizontalScope = new GUILayout.HorizontalScope(GUI.skin.box))
                {
                    GUILayout.Label("1 x rope, 1 x bamboo bowl", GUI.skin.label);
                    if (GUILayout.Button("Craft bamboo container", GUI.skin.button, GUILayout.MinWidth(100f), GUILayout.MaxWidth(200f)))
                    {
                        OnClickCraftBambooBidonButton();
                        CloseWindow();
                    }
                }

                //GUILayout.Label("1 x bamboo log", GUI.skin.label);
                //if (GUILayout.Button("Craft bamboo bowl", GUI.skin.button, GUILayout.MinWidth(100f), GUILayout.MaxWidth(200f)))
                //{
                //   OnClickCraftBambooBowlButton();
                //    CloseWindow();
                //}

                //GUILayout.Label("1 x rope, 1 x bamboo long stick", GUI.skin.label);
                //if (GUILayout.Button("Craft bamboo blowpipe", GUI.skin.button, GUILayout.MinWidth(100f), GUILayout.MaxWidth(200f)))
                //{
                //    OnClickCraftBlowgunButton();
                //    CloseWindow();
                //}

                //GUILayout.Label("1 x bamboo stick, 2 x feather", GUI.skin.label);
                //if (GUILayout.Button("Craft blowpipe dart", GUI.skin.button, GUILayout.MinWidth(100f), GUILayout.MaxWidth(200f)))
                //{
                //    OnClickCraftBlowgunArrowButton();
                //    CloseWindow();
                //}
            }
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 10000f));
        }

        private void CloseWindow()
        {
            showUI = false;
            EnableCursor(false);
        }

        private void OnClickCraftBambooBidonButton()
        {
            try
            {
                Item m_BambooBidon = CraftBambooContainer();
                if (m_BambooBidon != null)
                {
                    ShowHUDBigInfo($"Created 1 x {m_BambooBidon.m_Info.GetNameToDisplayLocalized()}", $"{ModName}  Info", HUDInfoLogTextureType.Count.ToString());
                }
            }
            catch (Exception exc)
            {
                ModAPI.Log.Write($"[{ModName}.{ModName}:{nameof(OnClickCraftBambooBidonButton)}] throws exception: {exc.Message}");
            }
        }

        private void OnClickCraftHammockButton()
        {
            try
            {
                Item m_Hammock = CraftHammock();
                if (m_Hammock != null)
                {
                    ShowHUDBigInfo($"Created 1 x {m_Hammock.m_Info.GetNameToDisplayLocalized()}", $"{ModName}  Info", HUDInfoLogTextureType.Count.ToString());
                }
            }
            catch (Exception exc)
            {
                ModAPI.Log.Write($"[{ModName}.{ModName}:{nameof(OnClickCraftHammockButton)}] throws exception: {exc.Message}");
            }
        }

        private void OnClickCraftBambooBowlButton()
        {
            try
            {
                Item m_BambooBowl = CraftBambooBowl();
                if (m_BambooBowl != null)
                {
                    ShowHUDBigInfo($"Created 1 x {m_BambooBowl.m_Info.GetNameToDisplayLocalized()}", $"{ModName}  Info", HUDInfoLogTextureType.Count.ToString());
                }
            }
            catch (Exception exc)
            {
                ModAPI.Log.Write($"[{ModName}.{ModName}:{nameof(OnClickCraftBambooBidonButton)}] throws exception: {exc.Message}");
            }
        }

        private void OnClickCraftBlowgunButton()
        {
            try
            {
                Item m_Blowgun = CraftBambooBlowgun();
                ShowHUDBigInfo($"Created 1 x Bamboo Blowpipe", $"{ModName}  Info", HUDInfoLogTextureType.Count.ToString());
            }
            catch (Exception exc)
            {
                ModAPI.Log.Write($"[{ModName}.{ModName}:{nameof(OnClickCraftBlowgunButton)}] throws exception: {exc.Message}");
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
                ModAPI.Log.Write($"[{ModName}.{ModName}:{nameof(OnClickCraftBlowgunArrowButton)}] throws exception: {exc.Message}");
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
                ModAPI.Log.Write($"[{ModName}.{ModName}:{nameof(CraftBambooContainer)}] throws exception: {exc.Message}");
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
                ModAPI.Log.Write($"[{ModName}.{ModName}:{nameof(CraftHammock)}] throws exception: {exc.Message}");
                return hammockToUse;
            }
        }

        public Item CraftBambooBowl()
        {
            Item bambooBowlToUse = null;
            try
            {
                bambooBowlToUse = CreateBambooBowl();
                return bambooBowlToUse;
            }
            catch (Exception exc)
            {
                ModAPI.Log.Write($"[{ModName}.{ModName}:{nameof(CraftBambooContainer)}] throws exception: {exc.Message}");
                return bambooBowlToUse;
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
                ModAPI.Log.Write($"[{ModName}.{ModName}:{nameof(CraftBambooBlowgun)}] throws exception: {exc.Message}");
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
                ModAPI.Log.Write($"[{ModName}.{ModName}:{nameof(CreateBambooContainer)}] throws exception: {exc.Message}");
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
                ModAPI.Log.Write($"[{ModName}.{ModName}:{nameof(CreateHammock)}] throws exception: {exc.Message}");
                return hammockToUse;
            }
        }

        private Item CreateBambooBowl()
        {
            Item bambooBowl = null;
            try
            {
                string m_InfoName = ItemID.Bamboo_Bowl.ToString();
                GameObject prefab = GreenHellGame.Instance.GetPrefab(m_InfoName);
                bambooBowl = CreateItem(prefab, true, player.transform.position + player.transform.forward * 2f, player.transform.rotation);
                bambooBowl.m_InfoName = m_InfoName;
                ItemInfo m_Info = CreateItemInfo(bambooBowl, ItemID.Bamboo_Bowl, BambooBowlComponents(), BambooBowlComponentsToReturn(), true, true);
                m_Info.m_CantDestroy = false;
                m_Info.m_UsedForCrafting = true;
                ((BowlInfo)m_Info).m_LiquidType = LiquidType.Water;
                ((BowlInfo)m_Info).m_Capacity = 50f;
                ((BowlInfo)m_Info).m_Amount = 0f;
                bambooBowl.m_Info = (BowlInfo)m_Info;
                CalcHealth(bambooBowl);
                bambooBowl.Initialize(true);

                player.AddKnownItem(ItemID.Bamboo_Bowl);
                EventsManager.OnEvent(Enums.Event.Craft, 1, (int)ItemID.Bamboo_Bowl);
                craftingSkill.OnSkillAction();
                itemsManager.m_CreationsData.Add((int)bambooBowl.m_Info.m_ID, (int)bambooBowl.m_Info.m_ID);
                itemsManager.OnCreateItem(ItemID.Bamboo_Bowl);
                return bambooBowl;
            }
            catch (Exception exc)
            {
                ModAPI.Log.Write($"[{ModName}.{ModName}:{nameof(CreateBambooBowl)}] throws exception: {exc.Message}");
                return bambooBowl;
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
                ModAPI.Log.Write($"[{ModName}.{ModName}:{nameof(CreateBambooBlowgun)}] throws exception: {exc.Message}");
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
                m_ComponentsToReturn = componentsToReturn,
                m_Health = 25f,
                m_MaxHealth = 25f
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
            dictionary.Add((int)ItemID.Rope, 4);
            dictionary.Add((int)ItemID.Palm_Leaf, 3);
            dictionary.Add((int)ItemID.Long_Stick, 3);

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

        private static Dictionary<int, int> BambooBowlComponents()
        {
            Dictionary<int, int> dictionary = new Dictionary<int, int> { };
            dictionary.Add((int)ItemID.Bamboo_Bowl, 1);
            return dictionary;
        }

        private static Dictionary<int, int> BambooBowlComponentsToReturn()
        {
            Dictionary<int, int> dictionary = new Dictionary<int, int> { };
            dictionary.Add((int)ItemID.Bamboo_Log, 1);

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
                ShowHUDBigInfo($"Added {count} x {blowPipeArrowItemInfo.GetNameToDisplayLocalized()} to inventory", $"{ModName}  Info", HUDInfoLogTextureType.Count.ToString());
            }
            catch (Exception exc)
            {
                ModAPI.Log.Write($"[{ModName}.{ModName}:{nameof(GetMaxThreeBlowpipeArrow)}] throws exception: {exc.Message}");
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
                ModAPI.Log.Write($"[{ModName}.{ModName}:{nameof(AddCraftingItem)}] throws exception: {exc.Message}");
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
                    ShowHUDBigInfo($"Blowpipe has been equipped!", $"{ModName}  Info", HUDInfoLogTextureType.Count.ToString());
                }
                else
                {
                    ShowHUDBigInfo($"Cannot find blowpipe to equip!", $"{ModName}  Info", HUDInfoLogTextureType.Count.ToString());
                }
            }
            catch (Exception exc)
            {
                ModAPI.Log.Write($"[{ModName}.{ModName}:{nameof(TryEquipBlowpipe)}] throws exception: {exc.Message}");
            }
        }
    }
}