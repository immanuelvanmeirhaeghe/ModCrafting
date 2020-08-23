using Enums;
using System;
using System.Collections.Generic;
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

        private bool showUI = false;

        private static ItemsManager itemsManager;

        private static HUDManager hUDManager;

        private static Player player;

        private static CraftingManager craftingManager;

        private static CraftingSkill craftingSkill;

        private bool m_IsOptionActive;
        public bool UseOption => m_IsOptionActive;

        /// <summary>
        /// ModAPI required security check to enable this mod feature for multiplayer.
        /// See <see cref="ModManager"/> for implementation.
        /// Based on request in chat: use  !requestMods in chat as client to request the host to activate mods for them.
        /// </summary>
        /// <returns>true if enabled, else false</returns>
        public bool IsModActiveForMultiplayer => FindObjectOfType(typeof(ModManager.ModManager)) != null ? ModManager.ModManager.AllowModsForMultiplayer : false;

        public bool IsModActiveForSingleplayer => ReplTools.AmIMaster();

        public ModCrafting()
        {
            s_Instance = this;
        }

        public static ModCrafting Get()
        {
            return s_Instance;
        }

        public static void ShowHUDBigInfo(string text, string header, string textureName)
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

        public static void ShowHUDInfoLog(string itemID, string localizedTextKey)
        {
            var localization = GreenHellGame.Instance.GetLocalization();
            HUDMessages hUDMessages = (HUDMessages)hUDManager.GetHUD(typeof(HUDMessages));
            hUDMessages.AddMessage(
                $"{localization.Get(localizedTextKey)}  {localization.Get(itemID)}"
                );
        }

        private static void EnableCursor(bool enabled = false)
        {
            CursorManager.Get().ShowCursor(enabled, false);
            player = Player.Get();

            if (enabled)
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
        }

        private void OnGUI()
        {
            if (showUI)
            {
                InitData();
                InitSkinUI();
                InitModUI();
            }
        }

        private void InitModUI()
        {
            GUI.Box(new Rect(1000f, 500f, 450f, 150f), "ModCrafting UI - Press HOME to open/close", GUI.skin.window);
            if (GUI.Button(new Rect(1420f, 500f, 20f, 20f), "X", GUI.skin.button))
            {
                showUI = false;
                EnableCursor(false);
            }

            GUI.Label(new Rect(1020f, 520f, 200f, 20f), "4 x rope, 3 x palm leave, 3 x long stick", GUI.skin.label);
            if (GUI.Button(new Rect(1270f, 520f, 150f, 20f), "Craft hammock", GUI.skin.button))
            {
                OnClickCraftHammockButton();
                showUI = false;
                EnableCursor(false);
            }

            GUI.Label(new Rect(1020f, 540f, 200f, 20f), "1 x rope, 1 x bamboo log", GUI.skin.label);
            if (GUI.Button(new Rect(1270f, 540f, 150f, 20f), "Craft bamboo bidon", GUI.skin.button))
            {
                OnClickCraftBambooBidonButton();
                showUI = false;
                EnableCursor(false);
            }
        }

        private void CreateMultiplayerOption()
        {
            if (IsModActiveForSingleplayer || IsModActiveForMultiplayer)
            {
                GUI.Label(new Rect(30f, 70f, 200f, 20f), "Use OptionFeature", GUI.skin.label);
                m_IsOptionActive = GUI.Toggle(new Rect(280f, 70f, 20f, 20f), m_IsOptionActive, "");
            }
            else
            {
                GUI.Label(new Rect(30f, 70f, 330f, 20f), "Use OptionFeature", GUI.skin.label);
                GUI.Label(new Rect(30f, 90f, 330f, 20f), "is only for single player or when host", GUI.skin.label);
                GUI.Label(new Rect(30f, 110f, 330f, 20f), "Host can activate using ModManager.", GUI.skin.label);
            }
        }

        private static void InitData()
        {
            itemsManager = ItemsManager.Get();
            hUDManager = HUDManager.Get();
            player = Player.Get();
            craftingManager = CraftingManager.Get();
            craftingSkill = Skill.Get<CraftingSkill>();
        }

        private static void InitSkinUI()
        {
            GUI.skin = ModAPI.Interface.Skin;
        }

        private static void OnClickCraftBambooBidonButton()
        {
            try
            {
                Item m_BambooBidon = CraftBambooBidon();
                if (m_BambooBidon != null)
                {
                    ShowHUDBigInfo($"Created 1 x {m_BambooBidon.m_Info.GetNameToDisplayLocalized()}", "ModCrafting Info", HUDInfoLogTextureType.Count.ToString());
                }
            }
            catch (Exception exc)
            {
                ModAPI.Log.Write($"[{nameof(ModCrafting)}.{nameof(ModCrafting)}:{nameof(OnClickCraftBambooBidonButton)}] throws exception: {exc.Message}");
            }
        }

        private static void OnClickCraftHammockButton()
        {
            try
            {
                Item m_Hammock = CraftHammock();
                if (m_Hammock != null)
                {
                    ShowHUDBigInfo($"Created 1 x {m_Hammock.m_Info.GetNameToDisplayLocalized()}", "ModCrafting Info", HUDInfoLogTextureType.Count.ToString());
                }
            }
            catch (Exception exc)
            {
                ModAPI.Log.Write($"[{nameof(ModCrafting)}.{nameof(ModCrafting)}:{nameof(OnClickCraftHammockButton)}] throws exception: {exc.Message}");
            }
        }

        public static Item CraftBambooBidon()
        {
            Item bambooBidonToUse = null;
            try
            {
                bambooBidonToUse = CreateBambooContainer();
                return bambooBidonToUse;
            }
            catch (Exception exc)
            {
                ModAPI.Log.Write($"[{nameof(ModCrafting)}.{nameof(ModCrafting)}:{nameof(CraftBambooBidon)}] throws exception: {exc.Message}");
                return bambooBidonToUse;
            }
        }

        public static Item CraftHammock()
        {
            Item hammockToUse = null;
            try
            {
                hammockToUse = CreateHammock();
                return hammockToUse;
            }
            catch (Exception exc)
            {
                ModAPI.Log.Write($"[{nameof(ModCrafting)}.{nameof(ModCrafting)}:{nameof(CraftBambooBidon)}] throws exception: {exc.Message}");
                return hammockToUse;
            }
        }

        private static Item CreateBambooContainer()
        {
            string m_InfoName = ItemID.Bamboo_Container.ToString();
            GameObject prefab = GreenHellGame.Instance.GetPrefab(m_InfoName);
            Item bambooContainer = CreateItem(prefab, true, player.transform.position + player.transform.forward * 4f, player.transform.rotation);
            bambooContainer.m_InfoName = m_InfoName;
            LiquidContainerInfo m_Info = (LiquidContainerInfo)CreateItemInfo(bambooContainer, ItemID.Bamboo_Container, m_InfoName, CreateBambooContainerComponents(), true, true);
            m_Info.m_CantDestroy = false;
            m_Info.m_UsedForCrafting = false;
            bambooContainer.m_Info = m_Info;
            CalcHealth(bambooContainer);
            bambooContainer.Initialize(true);
            player.AddKnownItem(ItemID.Bamboo_Container);
            EventsManager.OnEvent(Enums.Event.Craft, 1, (int)ItemID.Bamboo_Container);
            craftingSkill.OnSkillAction();
            itemsManager.m_CreationsData.Add((int)bambooContainer.m_Info.m_ID, (int)bambooContainer.m_Info.m_ID);
            itemsManager.OnCreateItem(ItemID.Bamboo_Container);
            return bambooContainer;
        }

        private static Item CreateHammock()
        {
            string m_InfoName = ItemID.village_hammock_a.ToString();
            GameObject prefab = GreenHellGame.Instance.GetPrefab(m_InfoName);
            Item hammockToUse = CreateItem(prefab, true, player.transform.position + player.transform.forward * 4f, player.transform.rotation);
            hammockToUse.m_InfoName = m_InfoName;
            ConstructionInfo m_Info = (ConstructionInfo)CreateItemInfo(hammockToUse, ItemID.village_hammock_a, m_InfoName, CreateHammockComponents(), false, false);
            m_Info.m_CantDestroy = false;
            m_Info.m_UsedForCrafting = false;
            m_Info.m_ConstructionType = ConstructionType.Shelter;
            m_Info.m_HitsCountToDestroy = 3;
            m_Info.m_PlaceToAttachNames = new List<string>
            {
                ItemID.building_frame.ToString(),
                ItemID.building_bamboo_frame.ToString()
            };
            m_Info.m_PlaceToAttachToNames = new List<string>
            {
                ItemID.building_wall.ToString(),
                ItemID.building_bamboo_wall.ToString()
            };
            m_Info.m_Type = ItemType.Construction;
            hammockToUse.m_Info = m_Info;
            CalcHealth(hammockToUse);
            hammockToUse.Initialize(true);
            player.AddKnownItem(ItemID.village_hammock_a);
            EventsManager.OnEvent(Enums.Event.Craft, 1, (int)ItemID.village_hammock_a);
            craftingSkill.OnSkillAction();
            itemsManager.m_CreationsData.Add((int)hammockToUse.m_Info.m_ID, (int)hammockToUse.m_Info.m_ID);
            itemsManager.OnCreateItem(ItemID.village_hammock_a);
            return hammockToUse;
        }

        private static void AddCraftingItem(Item item, int count = 1)
        {
            try
            {
                for (int i = 0; i < count; i++)
                {
                    CraftingManager.Get().AddItem(item, false);
                }
            }
            catch (Exception exc)
            {
                ModAPI.Log.Write($"[{nameof(ModCrafting)}.{nameof(ModCrafting)}:{nameof(AddCraftingItem)}] throws exception: {exc.Message}");
            }
        }

        private static Item CreateItem(GameObject prefab, bool im_register, Vector3 position, Quaternion rotation)
        {
            GameObject gameObject = Instantiate(prefab, position, rotation);
            gameObject.name = prefab.name;
            Item m_Item = gameObject.GetComponent<Item>();
            if (!m_Item)
            {
                DebugUtils.Assert($"[{nameof(ModCrafting)}:{nameof(CreateItem)}] Missing Item component - {prefab.name}");
                Destroy(gameObject);
                return null;
            }
            return m_Item;
        }

        private static ItemInfo CreateItemInfo(Item item, ItemID id, string infoName, Dictionary<int, int> componentsToReturn, bool canBeAddedToInventory, bool canBePlacedInStorage)
        {
            ItemInfo m_Info = new ItemInfo
            {
                m_CreationTime = (float)MainLevel.Instance.m_TODSky.Cycle.GameTime,
                m_Item = item,
                m_ID = id,
                m_CanBeDamaged = true,
                m_CanBeAddedToInventory = canBeAddedToInventory,
                m_CanBePlacedInStorage = canBePlacedInStorage,
                m_Craftable = true,
                m_ComponentsToReturn = componentsToReturn
            };

            return m_Info;
        }

        private static void CalcHealth(Item item)
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

        private static Dictionary<int, int> CreateHammockComponents()
        {
            Dictionary<int, int> dictionary = new Dictionary<int, int> { };
            dictionary.Add((int)ItemID.Rope, 4);
            dictionary.Add((int)ItemID.Palm_Leaf, 3);
            dictionary.Add((int)ItemID.Long_Stick, 3);

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

        private static Dictionary<int, int> CreateBambooContainerComponents()
        {
            Dictionary<int, int> dictionary = new Dictionary<int, int> { };
            dictionary.Add((int)ItemID.Rope, 1);
            dictionary.Add((int)ItemID.Bamboo_Log, 1);

            return dictionary;
        }

        private static List<Item> GetBambooContainerCraftingitems()
        {
            List<Item> list = new List<Item>
            {
                itemsManager.GetInfo(ItemID.Rope).m_Item,
                itemsManager.GetInfo(ItemID.Bamboo_Log).m_Item
            };
            return list;
        }
    }
}