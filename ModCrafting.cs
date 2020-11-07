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

        public static Rect ModCraftingScreen = new Rect(Screen.width / 40f, Screen.height / 40f, 750f, 230f);

        public static Vector2 scrollPosition;

        private static ItemsManager itemsManager;

        private static HUDManager hUDManager;

        private static Player player;

        private static InventoryBackpack inventoryBackpack;

        public static string SelectedItemName;
        public static int SelectedItemIndex;

        private static Item SelectedItemToDestroy;

        public static List<Item> CraftedItems = new List<Item>();

        public bool UseOption { get; private set; }

        public bool IsModActiveForMultiplayer { get; private set; }
        public bool IsModActiveForSingleplayer => ReplTools.AmIMaster();

        public static string ItemDestroyedMessage(string item) => $"{item} was <color=#{ColorUtility.ToHtmlStringRGBA(Color.green)}>destroyed!</color>";

        public static string NoItemSelectedMessage() => $"<color=#{ColorUtility.ToHtmlStringRGBA(Color.yellow)}>No item selected to destroy!</color>";

        public static string NoItemCraftedMessage() => $"<color=#{ColorUtility.ToHtmlStringRGBA(Color.yellow)}>Item could not be crafted!</color>";

        public static string ItemCraftedMessage(string item, int count) => $"Crafted {count} x {item}";

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
            string header = $"{ModName} Info";
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
                    itemsManager.AddItemToDestroy(item);
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
            itemsManager = ItemsManager.Get();
            hUDManager = HUDManager.Get();
            player = Player.Get();

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

                CraftItemBox();
            }
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 10000f));
        }

        private void CraftItemBox()
        {
            using (var verScope = new GUILayout.VerticalScope(GUI.skin.box))
            {
                GUILayout.Label("Select item from list then click Try craft", GUI.skin.label, GUILayout.MaxWidth(200f));

                ScrollingitemsView();

                if (GUILayout.Button("Try craft", GUI.skin.button))
                {
                    OnClickTryCraftButton();
                    CloseWindow();
                }
            }
        }

        private void ScrollingitemsView()
        {
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUI.skin.scrollView, GUILayout.MinHeight(300f));

            SelectedItemIndex = GUILayout.SelectionGrid(SelectedItemIndex, GetItems(), 3, GUI.skin.button);

            GUILayout.EndScrollView();
        }

        private void ScreenMenuBox()
        {
            if (GUI.Button(new Rect(ModCraftingScreen.width - 20f, 0f, 20f, 20f), "X", GUI.skin.button))
            {
                CloseWindow();
            }
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
                if (GUILayout.Button("Craft hammock", GUI.skin.button))
                {
                    OnClickCraftHammockButton();
                    CloseWindow();
                }
            }
        }

        private void CloseWindow()
        {
            ShowUI = false;
            EnableCursor(false);
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
                GameObject prefab = GreenHellGame.Instance.GetPrefab(SelectedItemName);
                Item craftedItem = CreateItem(prefab, true, player.transform.position, player.transform.rotation);
                if (craftedItem != null)
                {
                    CraftedItems.Add(craftedItem);
                    ShowHUDBigInfo(
                        HUDBigInfoMessage(
                            ItemCraftedMessage(craftedItem.m_Info.GetNameToDisplayLocalized(), 1)
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
                ModAPI.Log.Write($"[{ModName}:{nameof(OnClickTryCraftButton)}] throws exception:\n{exc.Message}");
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

        private void OnClickCraftBlowgunButton()
        {
            try
            {
                Item blowgun = CraftBambooBlowgun();
                if (blowgun != null)
                {
                    CraftedItems.Add(blowgun);
                    ShowHUDBigInfo(
                       HUDBigInfoMessage(
                           ItemCraftedMessage(blowgun.m_Info.GetNameToDisplayLocalized(), 1)
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
                ModAPI.Log.Write($"[{ModName}:{nameof(OnClickCraftBlowgunButton)}] throws exception:\n{exc.Message}");
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
                ModAPI.Log.Write($"[{ModName}:{nameof(OnClickCraftBlowgunArrowButton)}] throws exception:\n{exc.Message}");
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
                ModAPI.Log.Write($"[{ModName}:{nameof(CraftBambooBlowgun)}] throws exception:\n{exc.Message}");
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
                hammockToUse = CreateItem(prefab, true, player.transform.position + player.transform.forward * 2f + player.transform.up * 1f, player.transform.rotation);
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
                raft = CreateItem(prefab, true, player.transform.position + player.transform.forward * 2f, player.transform.rotation);
                return raft;
            }
            catch (Exception exc)
            {
                ModAPI.Log.Write($"[{ModName}:{nameof(CreateBambooRaft)}] throws exception:\n{exc.Message}");
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
                return blowgun;
            }
            catch (Exception exc)
            {
                ModAPI.Log.Write($"[{ModName}:{nameof(CreateBambooBlowgun)}] throws exception:\n{exc.Message}");
                return blowgun;
            }
        }

        private Item CreateItem(GameObject prefab, bool im_register, Vector3 position, Quaternion rotation)
        {
            return itemsManager.CreateItem(prefab, im_register, position, rotation);
        }

        public void GetMaxThreeBlowpipeArrow(int count = 1)
        {
            try
            {
                if (count <= 0)
                {
                    count = 1;
                }
                if (count > 3)
                {
                    count = 3;
                }
                string blowpipeArrow = ItemID.Blowpipe_Arrow.ToString();
                string itemName = itemsManager.GetInfo(ItemID.Blowpipe_Arrow).GetNameToDisplayLocalized();

                for (int i = 0; i < count; i++)
                {
                    player.AddItemToInventory(blowpipeArrow);
                }
                ShowHUDBigInfo(
                   HUDBigInfoMessage(
                       ItemCraftedMessage(itemName, count)
                   )
               );
            }
            catch (Exception exc)
            {
                ModAPI.Log.Write($"[{ModName}:{nameof(GetMaxThreeBlowpipeArrow)}] throws exception:\n{exc.Message}");
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

        public void OnYesFromDialog()
        {
            if (SelectedItemToDestroy != null)
            {
                if (SelectedItemToDestroy.m_Info.IsConstruction())
                {
                    SelectedItemToDestroy.TakeDamage(new DamageInfo { m_Damage = 100f, m_CriticalHit = true, m_DamageType = DamageType.Melee });
                }
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