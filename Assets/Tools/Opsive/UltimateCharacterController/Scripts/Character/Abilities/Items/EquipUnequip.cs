﻿/// ---------------------------------------------
/// Ultimate Character Controller
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------

using UnityEngine;
using Opsive.UltimateCharacterController.Events;
using Opsive.UltimateCharacterController.Items;
using Opsive.UltimateCharacterController.Items.Actions;
using Opsive.UltimateCharacterController.Inventory;
using Opsive.UltimateCharacterController.Game;
using Opsive.UltimateCharacterController.Utility;
using System.Collections.Generic;

namespace Opsive.UltimateCharacterController.Character.Abilities.Items
{
    /// <summary>
    /// Equips or unequips an ItemSet. Can be started manually by calling EquipUnequip.StartEquipUnequip(ItemSetIndex).
    /// </summary>
    [DefaultStartType(AbilityStartType.Manual)]
    [DefaultInputName("Equip First Item", 0)]
    [DefaultInputName("Equip Second Item", 1)]
    [DefaultInputName("Equip Third Item", 2)]
    [DefaultInputName("Equip Fourth Item", 3)]
    [DefaultInputName("Equip Fifth Item", 4)]
    [DefaultInputName("Equip Sixth Item", 5)]
    [DefaultInputName("Equip Seventh Item", 6)]
    [DefaultInputName("Equip Eighth Item", 7)]
    [DefaultInputName("Equip Ninth Item", 8)]
    [DefaultInputName("Equip Tenth Item", 9)]
    [AllowMultipleAbilityTypes]
    public class EquipUnequip : ItemSetAbilityBase
    {
        /// <summary>
        /// Specifies when to equip a new Item.
        /// </summary>
        public enum AutoEquipType
        {
            Always = 1,             // Always equip a picked up item.
            Unequipped = 2,         // Equip the item if there are no items equipped.
            OutOfUsableItem = 4,    // Equip the item if the current item has no more usable ItemTypes left.
            NotPreset = 8,          // Equip the item if the item hasn't been added to the inventory already.
            FirstTime = 16          // Equip the item the first time the item has been added.
        }

        /// <summary>
        /// Specifies what action take place with the next update.
        /// </summary>
        private enum EquipUnequipAction
        {
            Inactive,           // No actions are currently necessary.
            Unequip,            // The Unequip method should be called.
            UnequipComplete,    // The UnequipComplete method should be called.
            Equip,              // The Equip method should be called.
            EquipComplete       // The EquipComplete method should be called.
        }

        [Tooltip("Mask which specifies when to auto equip a new item.")]
        [HideInInspector] [SerializeField] protected AutoEquipType m_AutoEquip = AutoEquipType.Unequipped | AutoEquipType.OutOfUsableItem | AutoEquipType.NotPreset | AutoEquipType.FirstTime;
        [Tooltip("The Item State Index while equipping.")]
        [SerializeField] protected int m_EquipItemStateIndex = 4;
        [Tooltip("The Item State Index while unequipping.")]
        [SerializeField] protected int m_UnequipItemStateIndex = 5;
        [Tooltip("The value to add to the Item Substate Index when the character is aiming.")]
        [SerializeField] protected int m_AimItemSubstateIndexAddition = 100;

        public AutoEquipType AutoEquip { get { return m_AutoEquip; } set { m_AutoEquip = value; } }

        private bool m_StartEquipUnequip;
        private int m_StartEquipUnequipIndex;
        private int m_ActiveItemSetIndex = -1;
        private int m_PrevActiveItemSetIndex = -1;
        private EquipUnequipAction[] m_EquipUnequipActions;
        private Item[] m_EquipItems;
        private Item[] m_UnequipItems;
        private ScheduledEventBase[] m_ItemEvents;
        private bool m_CanEquip;
        private bool[] m_EquippingItems;
        private bool[] m_UnequippingItems;
        private Dictionary<Item, float> m_InventoryCount = new Dictionary<Item, float>();
        private bool m_ImmediateEquipUnequip;
        private bool m_PlayEquipAudio;
        private bool m_Aiming;

        public override bool CanReceiveMultipleStarts { get { return true; } }
        public int ActiveItemSetIndex { get { return m_ActiveItemSetIndex; } }
        public override bool CanStayActivatedOnDeath { get { return true; } }

        /// <summary>
        /// Initialize the default values.
        /// </summary>
        public override void Awake()
        {
            base.Awake();

            m_ItemSetManager = m_GameObject.GetCachedComponent<ItemSetManager>();
            m_EquipUnequipActions = new EquipUnequipAction[m_Inventory.SlotCount];
            m_EquipItems = new Item[m_Inventory.SlotCount];
            m_UnequipItems = new Item[m_Inventory.SlotCount];
            m_ItemEvents = new ScheduledEventBase[m_Inventory.SlotCount];
            m_EquippingItems = new bool[m_Inventory.SlotCount];
            m_UnequippingItems = new bool[m_Inventory.SlotCount];

            EventHandler.RegisterEvent(m_GameObject, "OnItemPickupStartPickup", WillStartPickup);
            EventHandler.RegisterEvent(m_GameObject, "OnItemPickupStopPickup", StopPickup);
            EventHandler.RegisterEvent<Item, float, bool, bool>(m_GameObject, "OnInventoryPickupItem", OnPickupItem);
            EventHandler.RegisterEvent<ItemType, float, bool, bool>(m_GameObject, "OnInventoryPickupItemType", OnPickupItemType);
            // Animation events cannot have multiple parameters so use the event name to determine which slot to equip/unequip.
            EventHandler.RegisterEvent(m_GameObject, "OnAnimatorItemUnequip", OnItemUnequip);
            EventHandler.RegisterEvent(m_GameObject, "OnAnimatorItemUnequipComplete", OnItemUnequipComplete);
            EventHandler.RegisterEvent(m_GameObject, "OnAnimatorItemUnequipFirstSlot", OnItemUnequipFirstSlot);
            EventHandler.RegisterEvent(m_GameObject, "OnAnimatorItemUnequipCompleteFirstSlot", OnItemUnequipCompleteFirstSlot);
            EventHandler.RegisterEvent(m_GameObject, "OnAnimatorItemUnequipSecondSlot", OnItemUnequipSecondSlot);
            EventHandler.RegisterEvent(m_GameObject, "OnAnimatorItemUnequipCompleteSecondSlot", OnItemUnequipCompleteSecondSlot);
            EventHandler.RegisterEvent(m_GameObject, "OnAnimatorItemUnequipThirdSlot", OnItemUnequipThirdSlot);
            EventHandler.RegisterEvent(m_GameObject, "OnAnimatorItemUnequipCompleteThirdSlot", OnItemUnequipCompleteThirdSlot);
            EventHandler.RegisterEvent(m_GameObject, "OnAnimatorItemEquip", OnItemEquip);
            EventHandler.RegisterEvent(m_GameObject, "OnAnimatorItemEquipComplete", OnItemEquipComplete);
            EventHandler.RegisterEvent(m_GameObject, "OnAnimatorItemEquipFirstSlot", OnItemEquipFirstSlot);
            EventHandler.RegisterEvent(m_GameObject, "OnAnimatorItemEquipCompleteFirstSlot", OnItemEquipCompleteFirstSlot);
            EventHandler.RegisterEvent(m_GameObject, "OnAnimatorItemEquipSecondSlot", OnItemEquipSecondSlot);
            EventHandler.RegisterEvent(m_GameObject, "OnAnimatorItemEquipCompleteSecondSlot", OnItemEquipCompleteSecondSlot);
            EventHandler.RegisterEvent(m_GameObject, "OnAnimatorItemEquipThirdSlot", OnItemEquipThirdSlot);
            EventHandler.RegisterEvent(m_GameObject, "OnAnimatorItemEquipCompleteThirdSlot", OnItemEquipCompleteThirdSlot);
            EventHandler.RegisterEvent<Item, int>(m_GameObject, "OnInventoryRemoveItem", OnRemoveItem);
            EventHandler.RegisterEvent<bool, bool>(m_GameObject, "OnAimAbilityStart", OnAim);
            EventHandler.RegisterEvent<Vector3, Vector3, GameObject>(m_GameObject, "OnDeath", OnDeath);
            EventHandler.RegisterEvent(m_GameObject, "OnInventoryRespawned", OnInventoryRespawned);
        }

        /// <summary>
        /// The ItemPickup component is starting to pick up ItemTypes.
        /// </summary>
        public void WillStartPickup()
        {
            // Remember the initial item inventory list to be able to determine if an item has been added.
            m_InventoryCount.Clear();
            var allItems = m_Inventory.GetAllItems();
            for (int i = 0; i < allItems.Count; ++i) {
                m_InventoryCount.Add(allItems[i], m_Inventory.GetItemTypeCount(allItems[i].ItemType));
            }
            m_PlayEquipAudio = true;
        }

        /// <summary>
        /// The ItemPickup component is no longer picking up any ItemTypes.
        /// </summary>
        private void StopPickup()
        {
            m_PlayEquipAudio = false;
        }

        /// <summary>
        /// An item has been picked up within the inventory. Determine if the ability should start.
        /// </summary>
        /// <param name="item">The item that has been equipped.</param>
        /// <param name="count">The amount of item picked up.</param>
        /// <param name="immediatePickup">Was the item be picked up immediately?</param>
        /// <param name="forceEquip">Should the item be force equipped?</param>
        private void OnPickupItem(Item item, float count, bool immediatePickup, bool forceEquip)
        {
            // The ability doesn't need to respond if the category doesn't match.
            if (!item.ItemType.CategoryIDMatch(m_ItemSetCategoryID) || !Enabled) {
                return;
            }

            // Determine if the item should be auto equipped. There are a variety of circumstances which will allow the item to be equipped.
            if (ShouldEquip(item, count)) {
                // The ItemSetManager will manage which items are equipped.
                var itemSetIndex = m_ItemSetManager.GetItemSetIndex(item, m_ItemSetCategoryIndex, true, immediatePickup);
                // The itemSet may not be valid for the item yet. 
                if (itemSetIndex != -1) {
                    // The ItemSet can be equipped immediately or play the equip animation. If equipping immediately ensure
                    // the character starts with the topmost ItemSet no matter the init order.
                    if (immediatePickup && (forceEquip || m_ActiveItemSetIndex == -1 || itemSetIndex < m_ActiveItemSetIndex)) {
                        // The ItemSet should be updated immediately.
                        m_ActiveItemSetIndex = itemSetIndex;
                        EventHandler.ExecuteEvent<int>(this, "OnEquipUnequipItemSetIndexChange", m_ActiveItemSetIndex);
                        m_ItemSetManager.UpdateActiveItemSet(m_ItemSetCategoryIndex, m_ActiveItemSetIndex);
                        for (int i = 0; i < m_Inventory.SlotCount; ++i) {
                            // The current slot will be updated immediately.
                            ForceEquipUnequip(i, false);

                            var itemType = m_ItemSetManager.GetEquipItemType(m_ItemSetCategoryIndex, itemSetIndex, i);
                            if (itemType == null) {
                                // Unequip the current item if no items should be equipped with the current item set.
                                var unequipItem = m_Inventory.GetItem(i);
                                if (unequipItem != null && unequipItem.ItemType.CategoryIDMatch(m_ItemSetCategoryID)) {
                                    m_Inventory.UnequipItem(i);
                                    EventHandler.ExecuteEvent(m_GameObject, "OnAbilityUnequipItemComplete", unequipItem, i);
                                }

                                continue;
                            }
                            // Only manage the ItemType if the category matches.
                            if (itemType.CategoryIDMatch(m_ItemSetCategoryID)) {
                                var equippedItem = m_Inventory.GetItem(i);
                                if (equippedItem != null) {
                                    // No changes are necessary if the ItemType that should be equipped is the same as the ItemType that is already equipped.
                                    if (itemType == equippedItem.ItemType) {
                                        continue;
                                    }
                                    m_Inventory.UnequipItem(i);
                                    EventHandler.ExecuteEvent(m_GameObject, "OnAbilityUnequipItemComplete", equippedItem, i);
                                }
                                var equipItem = m_Inventory.GetItem(i, itemType);
                                if (equipItem != null) {
                                    equipItem.WillEquip();
                                    EventHandler.ExecuteEvent(m_GameObject, "OnAbilityWillEquipItem", equipItem, i);
                                    equipItem.StartEquip(true);
                                }
                                m_Inventory.EquipItem(itemType, i, !m_PlayEquipAudio);
                            }
                        }
                    } else if (forceEquip) {
                        StartEquipUnequip(itemSetIndex);
                    }
                } else if (m_ItemSetManager.ActiveItemSetIndex[m_ItemSetCategoryIndex] == -1) {
                    // The ItemSet isn't currently valid. Set the default ItemSet so at least one ItemSet is active which will allow the states to switch
                    // the ItemSet when it becomes valid.
                    m_ItemSetManager.SetDefaultItemSet(m_ItemSetCategoryIndex);
                }
            }
        }

        /// <summary>
        /// Should the item be equipped?
        /// </summary>
        /// <param name="item">The item that may be equipped.</param>
        /// <param name="count">The amount of item picked up.</param>
        /// <returns>True if the item should be equipped.</returns>
        public bool ShouldEquip(Item item, float count)
        {
            // The character shouldn't equip the item if an item is currently in use or is reloading.
            if (m_CharacterLocomotion.IsAbilityTypeActive<Use>()
#if ULTIMATE_CHARACTER_CONTROLLER_SHOOTER
                || m_CharacterLocomotion.IsAbilityTypeActive<Reload>()
#endif
                ) {
                return false;
            }

            var shouldEquip = false;
            var currentItem = m_Inventory.GetItem(item.SlotID);
            float itemCount;
            if (!m_InventoryCount.TryGetValue(item, out itemCount)) {
                itemCount = -1;
            }
            if ((m_AutoEquip & AutoEquipType.Always) != 0) {
                shouldEquip = true;
            } else if ((m_AutoEquip & AutoEquipType.Unequipped) != 0 && currentItem == null && m_EquipItems[item.SlotID] == null) {
                shouldEquip = true;
            } else if ((m_AutoEquip & AutoEquipType.NotPreset) != 0 && itemCount < count) {
                shouldEquip = true;
            } else if ((m_AutoEquip & AutoEquipType.FirstTime) != 0 && itemCount == -1) {
                shouldEquip = true;
            } else if ((m_AutoEquip & AutoEquipType.OutOfUsableItem) != 0 && (currentItem is IUsableItem) && m_Inventory.GetItemTypeCount((currentItem as IUsableItem).GetConsumableItemType()) == 0) {
                shouldEquip = true;
            }

            return shouldEquip;
        }

        /// <summary>
        /// An ItemType has been picked up within the inventory.
        /// </summary>
        /// <param name="itemType">The ItemType that has been picked up.</param>
        /// <param name="count">The amount of item picked up.</param>
        /// <param name="immediatePickup">Was the item be picked up immediately?</param>
        /// <param name="forceEquip">Should the item be force equipped?</param>
        private void OnPickupItemType(ItemType itemType, float count, bool immediatePickup, bool forceEquip)
        {
            // The ability doesn't need to respond if the category doesn't match.
            if (!itemType.CategoryIDMatch(m_ItemSetCategoryID) || !Enabled) {
                return;
            }

            // An ItemType may be picked up after the item is picked up. If this occurs OnPickUpItem will never be called and the item won't be equipped
            // even if it should be. Loop through the available items to determine if the ItemType should cause an item to be equipped.
            var allItems = m_Inventory.GetAllItems();
            for (int i = 0; i < allItems.Count; ++i) {
                if (allItems[i].ItemType == itemType) {
                    continue;
                }

                var itemActions = allItems[i].ItemActions;
                for (int j = 0; j < itemActions.Length; ++j) {
                    var usableItem = itemActions[j] as IUsableItem;
                    if (usableItem != null && usableItem.GetConsumableItemType() == itemType) {
                        OnPickupItem(allItems[i], m_Inventory.GetItemTypeCount(allItems[i].ItemType), immediatePickup, forceEquip);
                    }
                }
            }
        }

        /// <summary>
        /// Starts equipping/unequipping to the specified ItemSet.
        /// </summary>
        /// <param name="itemSetIndex">The ItemSet to equip/unequip the items to.</param>
        public void StartEquipUnequip(int itemSetIndex)
        {
            StartEquipUnequip(itemSetIndex, false);
        }

        /// <summary>
        /// Starts equipping/unequipping to the specified ItemSet.
        /// </summary>
        /// <param name="itemSetIndex">The ItemSet to equip/unequip the items to.</param>
        /// <param name="forceEquipUnequip">Should the ability be force started? This will stop all abilities that would prevent EquipUnequip from starting.</param>
        public void StartEquipUnequip(int itemSetIndex, bool forceEquipUnequip)
        {
            StartEquipUnequip(itemSetIndex, forceEquipUnequip, m_ImmediateEquipUnequip);
        }

        /// <summary>
        /// Starts equipping/unequipping to the specified ItemSet.
        /// </summary>
        /// <param name="itemSetIndex">The ItemSet to equip/unequip the items to.</param>
        /// <param name="forceEquipUnequip">Should the ability be force started? This will stop all abilities that would prevent EquipUnequip from starting.</param>
        /// <param name="immediateEquipUnequip">Should the items be equipped or unequipped immediately?</param>
        public void StartEquipUnequip(int itemSetIndex, bool forceEquipUnequip, bool immediateEquipUnequip)
        {
            if (itemSetIndex == m_ActiveItemSetIndex) {
                return;
            }

            // Equip unequip normally will not start if use or reload is active. If the ability is forced then it should first stop the abilities.
            if (forceEquipUnequip) {
                var activeItemAbilities = m_CharacterLocomotion.ActiveItemAbilities;
                for (int i = m_CharacterLocomotion.ActiveItemAbilityCount - 1; i > -1; --i) {
                    if (activeItemAbilities[i] is Use
#if ULTIMATE_CHARACTER_CONTROLLER_SHOOTER
                        || activeItemAbilities[i] is Reload
#endif
                        ) {
                        m_CharacterLocomotion.TryStopAbility(activeItemAbilities[i], true);
                    }
                }
            }

            m_ActiveItemSetIndex = itemSetIndex;
            m_ImmediateEquipUnequip = immediateEquipUnequip;
            StartAbility();
        }

        /// <summary>
        /// Can the ability be started?
        /// </summary>
        /// <returns>True if the ability can be started.</returns>
        public override bool CanStartAbility()
        {
            // An attribute may prevent the ability from starting.
            if (!base.CanStartAbility()) {
                return false;
            }

            // If the InputIndex is -1 then the ability has been started manually.
            var itemSetIndex = m_ActiveItemSetIndex;
            if (InputIndex != -1) { // Item specified by button index.
                itemSetIndex = InputIndex;
            }

            // Don't start if the ItemSetIndex is the same or invalid. The check has already been performed for the manual start type.
            if (InputIndex != -1 && (itemSetIndex == m_ActiveItemSetIndex || !m_ItemSetManager.IsItemSetValid(m_ItemSetCategoryIndex, itemSetIndex, true))) {
                InputIndex = -1;
                return false;
            }

            m_ActiveItemSetIndex = itemSetIndex;

            return true;
        }

        /// <summary>
        /// The ability has started.
        /// </summary>
        protected override void AbilityStarted()
        {
            base.AbilityStarted();

            m_StartEquipUnequip = true;
            m_StartEquipUnequipIndex = m_ActiveItemSetIndex;
            if (m_ImmediateEquipUnequip) {
                Update();
            }
        }

        /// <summary>
        /// Called when another ability is attempting to start and the current ability is active.
        /// Returns true or false depending on if the new ability should be blocked from starting.
        /// </summary>
        /// <param name="startingAbility">The ability that is starting.</param>
        /// <returns>True if the ability should be blocked.</returns>
        public override bool ShouldBlockAbilityStart(Ability startingAbility)
        {
            if (base.ShouldStopActiveAbility(startingAbility)) {
                return true;
            }
            if (startingAbility is EquipScroll) {
                return true;
            }
            // The item can't be used or reloaded if it is being unequipped.
            if (startingAbility is Use
#if ULTIMATE_CHARACTER_CONTROLLER_SHOOTER
                || startingAbility is Reload
#endif
                ) {
                for (int i = 0; i < m_UnequipItems.Length; ++i) {
                    if (m_UnequipItems[i] != null) {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Returns the Item State Index which corresponds to the slot ID.
        /// </summary>
        /// <param name="slotID">The ID of the slot that corresponds to the Item State Index.</param>
        /// <returns>The Item State Index which corresponds to the slot ID.</returns>
        public override int GetItemStateIndex(int slotID)
        {
            if (m_UnequipItems[slotID] != null) {
                return m_UnequipItemStateIndex;
            }
            if (m_CanEquip && m_EquipItems[slotID] != null) {
                return m_EquipItemStateIndex;
            }
            return -1;
        }

        /// <summary>
        /// Returns the Item Substate Index which corresponds to the slot ID.
        /// </summary>
        /// <param name="slotID">The ID of the slot that corresponds to the Item Substate Index.</param>
        /// <returns>The Item Substate Index which corresponds to the slot ID.</returns>
        public override int GetItemSubstateIndex(int slotID)
        {
            if (m_UnequipItems[slotID] != null) {
                return m_UnequipItems[slotID].UnequipAnimatorAudioStateSet.GetItemSubstateIndex() + (m_Aiming ? m_AimItemSubstateIndexAddition : 0);
            }
            if (m_CanEquip && m_EquipItems[slotID] != null) {
                return m_EquipItems[slotID].EquipAnimatorAudioStateSet.GetItemSubstateIndex() + (m_Aiming ? m_AimItemSubstateIndexAddition : 0);
            }
            return -1;
        }

        /// <summary>
        /// Updates the ability
        /// </summary>
        public override void Update()
        {
            base.Update();

            if (m_StartEquipUnequip) {
                m_StartEquipUnequip = false;
                // The ActiveItemIndexSetIndex may have been changed due to the EquipUnequipAction above.
                m_ActiveItemSetIndex = m_StartEquipUnequipIndex;

                // Equip any unequipped items which are within the ItemSetIndex and belong to the specified category.
                // Unequip any items which are equipped and are not within the ItemSetIndex and belong to the specified category.
                var unequip = false;
                var equip = false;
                for (int i = 0; i < m_Inventory.SlotCount; ++i) {
                    // Stop any equips/unequips that have already started.
                    ForceEquipUnequip(i, true);
                    m_EquipUnequipActions[i] = EquipUnequipAction.Inactive;

                    // Determine the item that is currently equipped and the item that should be equipped.
                    Item currentItem = null, targetItem = null;
                    var currentItemType = m_ItemSetManager.GetEquipItemType(m_ItemSetCategoryIndex, i);
                    if (currentItemType != null) {
                        currentItem = m_Inventory.GetItem(i, currentItemType);
                    }
                    var targetItemType = m_ItemSetManager.GetEquipItemType(m_ItemSetCategoryIndex, m_ActiveItemSetIndex, i);
                    var skipEquip = false;
                    // If the target ItemType doesn't equal the equip ItemType from the ItemSetManager then the equip ItemType is equipped in a different category.
                    // Only the lower categories should be searched because they have a higher priority.
                    for (int j = 0; j < m_ItemSetCategoryIndex; ++j) {
                        var equipItemType = m_ItemSetManager.GetEquipItemType(j, i);
                        if (equipItemType != null && equipItemType != targetItemType) {
                            skipEquip = true;
                            continue;
                        }
                    }
                    if (skipEquip) {
                        continue;
                    }
                    if (targetItemType != null && m_Inventory.GetItemTypeCount(targetItemType) > 0) {
                        targetItem = m_Inventory.GetItem(i, targetItemType);
                    }

                    // Nothing needs to be done if the current item is equal to the item that should be equipped. The target item may not be active if
                    // the ability is quickly moving through the items.
                    if (currentItem == targetItem && (targetItem == null || (targetItem != null && targetItem.IsActive()))) {
                        continue;
                    }
                    // The item first needs to be unequipped before another item can be equipped.
                    if (m_UnequipItems[i] == null && currentItem != targetItem && currentItem != null && (currentItem.ItemType.CategoryIDMatch(m_ItemSetCategoryID)) && currentItem.IsActive()) {
                        m_UnequipItems[i] = currentItem;
                        m_UnequippingItems[i] = true;
                        currentItem.StartUnequip(m_ImmediateEquipUnequip);
                    }
                    // ForceEquipUnequip may be unequipping the item.
                    unequip = unequip || m_UnequipItems[i] != null;
                    if (targetItemType != null && (targetItemType.CategoryIDMatch(m_ItemSetCategoryID))) {
                        m_EquipItems[i] = targetItem;
                        m_EquippingItems[i] = false;
                        equip = true;
                    }
                }

                // The ability can be stopped if no action needs to be performed.
                if (unequip || equip) {
                    EventHandler.ExecuteEvent<int>(this, "OnEquipUnequipItemSetIndexChange", m_ActiveItemSetIndex);
                    m_ItemSetManager.UpdateNextItemSet(m_ItemSetCategoryIndex, m_ActiveItemSetIndex);

                    m_CanEquip = !unequip; // The ability can equip as soon as the unequips are complete.
                    var canEqup = m_CanEquip; // The can equp status may change if an item is unequipped immediately. Remember the initial value so the item isn't equipped twice.
                                              // Wait to schedule the events until after all of the equip/unequip items have been determined. Otherwise if the event is fired immediate (with a duration
                                              // of 0) then the ability could end before all slots have a chance to determine if any items need to be equipped/unequipped.
                    for (int i = 0; i < m_Inventory.SlotCount; ++i) {
                        if (m_UnequipItems[i] == null && m_EquipItems[i] == null) {
                            continue;
                        }

                        if (m_UnequipItems[i] != null) {
                            if (!m_UnequipItems[i].UnequipEvent.WaitForAnimationEvent || m_ImmediateEquipUnequip) {
                                var duration = m_ImmediateEquipUnequip ? 0 : m_UnequipItems[i].UnequipEvent.Duration;
                                m_ItemEvents[i] = Scheduler.ScheduleFixed(duration, ItemUnequip, i, duration == 0);
                            }
                        } else if (m_EquipItems[i] != null && canEqup) {
                            m_EquippingItems[i] = true;
                            m_EquipItems[i].StartEquip(m_ImmediateEquipUnequip);
                            m_EquipItems[i].WillEquip();
                            EventHandler.ExecuteEvent(m_GameObject, "OnAbilityWillEquipItem", m_EquipItems[i], i);
                            if (!m_EquipItems[i].EquipEvent.WaitForAnimationEvent || m_ImmediateEquipUnequip) {
                                var duration = m_ImmediateEquipUnequip ? 0 : m_EquipItems[i].EquipEvent.Duration;
                                m_ItemEvents[i] = Scheduler.ScheduleFixed(duration, ItemEquip, i, duration == 0);
                            }
                        }
                    }

                    m_CharacterLocomotion.UpdateItemAbilityAnimatorParameters();
                } else {
                    EventHandler.ExecuteEvent<int>(this, "OnEquipUnequipItemSetIndexChange", m_ActiveItemSetIndex);
                    StopAbility();
                }
                return;
            }

            // The equip/unequip actions may occur during an animation event which will occur after the animator has updated. To prevent the animator from being out
            // of sync with the controller for a frame the ability should be updated within the Update loop.
            for (int i = 0; i < m_EquipUnequipActions.Length; ++i) {
                if (m_EquipUnequipActions[i] == EquipUnequipAction.Inactive) {
                    continue;
                }
                switch (m_EquipUnequipActions[i]) {
                    case EquipUnequipAction.Unequip:
                        ItemUnequip(i, true);
                        break;
                    case EquipUnequipAction.UnequipComplete:
                        ItemUnequipComplete(i, true);
                        break;
                    case EquipUnequipAction.Equip:
                        ItemEquip(i, true);
                        break;
                    case EquipUnequipAction.EquipComplete:
                        ItemEquipComplete(i, true);
                        break;
                }
            }
        }

        /// <summary>
        /// Equips and unequips all of the pending items.
        /// </summary>
        /// <param name="slotID">The slot ID to equip or unequip the item at.</param>
        /// <param name="startAbility">Is the ability being started?</param>
        private void ForceEquipUnequip(int slotID, bool startAbility)
        {
            if (m_EquipItems[slotID] != null || m_UnequipItems[slotID] != null) {
                Scheduler.Cancel(m_ItemEvents[slotID]);
                var startUnequip = false;
                // If the item is no longer being equipped then it is unequipped.
                if (startAbility) {
                    if (m_EquipItems[slotID] != null && m_EquippingItems[slotID]) {
                        m_EquipItems[slotID].StartUnequip(true);
                        m_Inventory.UnequipItem(m_EquipItems[slotID].ItemType, slotID);
                        EventHandler.ExecuteEvent(m_GameObject, "OnAbilityUnequipItemComplete", m_EquipItems[slotID], slotID);
                    } else if (m_EquipItems[slotID] != null && m_CanEquip) {
                        // Don't unequip the item currently being equipped if the ItemSet uses it.
                        var equipItemType = m_ItemSetManager.GetEquipItemType(m_ItemSetCategoryIndex, m_ActiveItemSetIndex, slotID);
                        if (equipItemType == null || m_EquipItems[slotID].ItemType != equipItemType) {
                            // The equipping item should be unequipped smoothly.
                            m_UnequipItems[slotID] = m_EquipItems[slotID];
                            m_UnequippingItems[slotID] = true;
                            m_UnequipItems[slotID].StartUnequip(m_ImmediateEquipUnequip);
                            startUnequip = true;
                        }
                    }
                } else {
                    // Equip the equip item immediately if the ability is being stopped to prevent the items from getting into an invalid state.
                    if (m_EquipItems[slotID] != null) {
                        m_EquipItems[slotID].StartEquip(m_ImmediateEquipUnequip);
                        m_EquipItems[slotID].WillEquip();
                        EventHandler.ExecuteEvent(m_GameObject, "OnAbilityWillEquipItem", m_EquipItems[slotID], slotID);
                        m_Inventory.EquipItem(m_EquipItems[slotID].ItemType, slotID, m_ImmediateEquipUnequip);
                    }
                }
                m_EquipItems[slotID] = null;
                m_EquippingItems[slotID] = false;

                if (!startUnequip && m_UnequipItems[slotID] != null) {
                    if (m_UnequippingItems[slotID] && startAbility) {
                        // Reset the unequpping item back to being equip if the item set uses the same item again. If a different item should be equipped
                        // then the ability will unequip it through AbilityStarted.
                        if (m_ItemSetManager.GetEquipItemType(m_ItemSetCategoryIndex, m_ActiveItemSetIndex, slotID) == m_UnequipItems[slotID].ItemType) {
                            m_UnequipItems[slotID].StartEquip(m_ImmediateEquipUnequip);
                            m_UnequipItems[slotID].WillEquip();
                            EventHandler.ExecuteEvent(m_GameObject, "OnAbilityWillEquipItem", m_UnequipItems[slotID], slotID);
                            m_Inventory.EquipItem(m_UnequipItems[slotID].ItemType, slotID, true);
                            m_UnequipItems[slotID] = null;
                            m_UnequippingItems[slotID] = false;
                        }
                    } else {
                        m_UnequipItems[slotID].StartUnequip(true);
                        m_Inventory.UnequipItem(m_UnequipItems[slotID].ItemType, slotID);
                        EventHandler.ExecuteEvent(m_GameObject, "OnAbilityUnequipItemComplete", m_UnequipItems[slotID], slotID);
                        m_UnequipItems[slotID] = null;
                        m_UnequippingItems[slotID] = false;
                    }
                }
                m_CharacterLocomotion.UpdateItemAbilityAnimatorParameters();
            }
        }

        /// <summary>
        /// The Aim ability has started or stopped.
        /// </summary>
        /// <param name="start">Has the Aim ability started?</param>
        /// <param name="inputStart">Was the ability started from input?</param>
        private void OnAim(bool aim, bool inputStart)
        {
            if (!inputStart) {
                return;
            }
            m_Aiming = aim;
        }

        /// <summary>
        /// The Animation Event has unequipped all of the items.
        /// </summary>
        private void OnItemUnequip()
        {
            for (int i = 0; i < m_UnequipItems.Length; ++i) {
                if (m_UnequipItems[i] != null) {
                    ItemUnequip(i, false);
                }
            }
        }

        /// <summary>
        /// The Animation Event has unequipped the first item slot.
        /// </summary>
        private void OnItemUnequipFirstSlot()
        {
            ItemUnequip(0, false);
        }

        /// <summary>
        /// The Animation Event has unequipped the second item slot.
        /// </summary>
        private void OnItemUnequipSecondSlot()
        {
            ItemUnequip(1, false);
        }

        /// <summary>
        /// The Animation Event has unequipped the third item slot.
        /// </summary>
        private void OnItemUnequipThirdSlot()
        {
            ItemUnequip(2, false);
        }

        /// <summary>
        /// The animation is done unequipping the item.
        /// </summary>
        /// <param name="slotID">The slot ID of the item that was unequipped.</param>
        /// <param name="canUpdate">Can the item be updated? If false the status enum will be set and the item will be updated within the Update loop.</param>
        private void ItemUnequip(int slotID, bool canUpdate)
        {
            // If the item can't be updated then the event should wait until the Update loop. This ensures items are updated in the proper order.
            if (!canUpdate) {
                m_EquipUnequipActions[slotID] = EquipUnequipAction.Unequip;
                return;
            }
            m_EquipUnequipActions[slotID] = EquipUnequipAction.Inactive;

            // Clear out the unequipped item and notify those interested.
            var unequipItem = m_UnequipItems[slotID];
            if (unequipItem == null || !m_UnequippingItems[slotID]) {
                return;
            }

            m_Inventory.UnequipItem(slotID);
            m_UnequippingItems[slotID] = false;

            if (!unequipItem.UnequipCompleteEvent.WaitForAnimationEvent) {
                var duration = m_ImmediateEquipUnequip ? 0 : unequipItem.UnequipCompleteEvent.Duration;
                m_ItemEvents[slotID] = Scheduler.ScheduleFixed(duration, ItemUnequipComplete, slotID, duration == 0);
            }
        }

        /// <summary>
        /// The unequip animation has completed for all of the items.
        /// </summary>
        private void OnItemUnequipComplete()
        {
            for (int i = 0; i < m_UnequipItems.Length; ++i) {
                if (m_UnequipItems[i] != null) {
                    ItemUnequipComplete(i, false);
                }
            }
        }

        /// <summary>
        /// The unequip animation has completed for the first item slot.
        /// </summary>
        private void OnItemUnequipCompleteFirstSlot()
        {
            ItemUnequipComplete(0, false);
        }

        /// <summary>
        /// The unequip animation has completed for the second item slot.
        /// </summary>
        private void OnItemUnequipCompleteSecondSlot()
        {
            ItemUnequipComplete(1, false);
        }

        /// <summary>
        /// The unequip animation has completed for the third item slot.
        /// </summary>
        private void OnItemUnequipCompleteThirdSlot()
        {
            ItemUnequipComplete(2, false);
        }

        /// <summary>
        /// The animation is done unequipping the item.
        /// </summary>
        /// <param name="slotID">The slot ID of the item that was unequipped.</param>
        /// <param name="canUpdate">Can the item be updated? If false the status enum will be set and the item will be updated within the Update loop.</param>
        private void ItemUnequipComplete(int slotID, bool canUpdate)
        {
            // If the item can't be updated then the event should wait until the Update loop. This ensures items are updated in the proper order.
            if (!canUpdate) {
                m_EquipUnequipActions[slotID] = EquipUnequipAction.UnequipComplete;
                return;
            }
            m_EquipUnequipActions[slotID] = EquipUnequipAction.Inactive;

            var unequipItem = m_UnequipItems[slotID];
            if (unequipItem == null) {
                return;
            }

            m_UnequipItems[slotID] = null;
            Scheduler.Cancel(m_ItemEvents[slotID]);
            EventHandler.ExecuteEvent(m_GameObject, "OnAbilityUnequipItemComplete", unequipItem, slotID);
            m_CharacterLocomotion.UpdateItemAbilityAnimatorParameters();

            // The ability shouldn't start to equip or stop until all unequip items have been unequipped.
            for (int i = 0; i < m_UnequipItems.Length; ++i) {
                if (m_UnequipItems[i] != null) {
                    return;
                }
            }

            // Start equipping the next item if all of the items have been unequipped. The next item doesn't have to be in the same slot 
            // because the item could have been waiting for the item in the current slot to be unequipped.
            m_CanEquip = true;
            var stopAbility = true;
            for (int i = 0; i < m_EquipItems.Length; ++i) {
                if (m_EquipItems[i] != null && !m_EquippingItems[i]) {
                    stopAbility = false;
                    m_EquippingItems[i] = true;
                    m_EquipItems[i].WillEquip();
                    EventHandler.ExecuteEvent(m_GameObject, "OnAbilityWillEquipItem", m_EquipItems[i], i);
                    // EquipItems[i] may be null if the item has an equip duration of 0.
                    if (m_EquipItems[i] != null && (m_ImmediateEquipUnequip || !m_EquipItems[i].EquipEvent.WaitForAnimationEvent)) {
                        var duration = m_ImmediateEquipUnequip ? 0 : m_EquipItems[i].EquipEvent.Duration;
                        m_ItemEvents[i] = Scheduler.ScheduleFixed(duration, ItemEquip, i, duration == 0);
                    }
                }
            }

            // Stop the ability if no items need to be unequipped/equipped.
            if (stopAbility) {
                TryStopEquipUnequipAbility();
            }
        }

        /// <summary>
        /// The Animation Event has equipped all of the items.
        /// </summary>
        private void OnItemEquip()
        {
            for (int i = 0; i < m_EquipItems.Length; ++i) {
                if (m_EquipItems[i] != null) {
                    ItemEquip(i, false);
                }
            }
        }

        /// <summary>
        /// The Animation Event has equipped the first item slot.
        /// </summary>
        private void OnItemEquipFirstSlot()
        {
            ItemEquip(0, false);
        }

        /// <summary>
        /// The Animation Event has equipped the second item slot.
        /// </summary>
        private void OnItemEquipSecondSlot()
        {
            ItemEquip(1, false);
        }

        /// <summary>
        /// The Animation Event has equipped the third item slot.
        /// </summary>
        private void OnItemEquipThirdSlot()
        {
            ItemEquip(2, false);
        }

        /// <summary>
        /// The animation is done equipping the item.
        /// </summary>
        /// <param name="slotID">The slot that the unequipped item belongs to.</param>
        /// <param name="canUpdate">Can the item be updated? If false the status enum will be set and the item will be updated within the Update loop.</param>
        private void ItemEquip(int slotID, bool canUpdate)
        {
            // If the item can't be updated then the event should wait until the Update loop. This ensures items are updated in the proper order.
            if (!canUpdate) {
                m_EquipUnequipActions[slotID] = EquipUnequipAction.Equip;
                return;
            }
            m_EquipUnequipActions[slotID] = EquipUnequipAction.Inactive;
            if (!m_CanEquip) {
                return;
            }

            // Clear out the equipped item and notify those interested.
            var equipItem = m_EquipItems[slotID];
            if (equipItem == null || !m_EquippingItems[slotID]) {
                return;
            }

            m_EquippingItems[slotID] = false;
            m_CharacterLocomotion.UpdateItemAbilityAnimatorParameters();
            equipItem.StartEquip(m_ImmediateEquipUnequip);
            m_Inventory.EquipItem(equipItem.ItemType, slotID, m_ImmediateEquipUnequip);

            // The new ItemSet is active as soon as the new items are equipped.
            var equip = false;
            for (int i = 0; i < m_EquipItems.Length; ++i) {
                if (m_EquippingItems[slotID]) {
                    equip = true;
                    break;
                }
            }
            if (!equip) {
                m_ItemSetManager.UpdateActiveItemSet(m_ItemSetCategoryIndex, m_ActiveItemSetIndex);
            }

            if (!equipItem.EquipCompleteEvent.WaitForAnimationEvent || m_ImmediateEquipUnequip) {
                var duration = m_ImmediateEquipUnequip ? 0 : equipItem.EquipCompleteEvent.Duration;
                m_ItemEvents[slotID] = Scheduler.ScheduleFixed(duration, ItemEquipComplete, slotID, duration == 0);
            }
        }

        /// <summary>
        /// The equip animation has completed for all of the items.
        /// </summary>
        private void OnItemEquipComplete()
        {
            for (int i = 0; i < m_EquipItems.Length; ++i) {
                if (m_EquipItems[i] != null) {
                    ItemEquipComplete(i, false);
                }
            }
        }

        /// <summary>
        /// The equip animation has completed for the first item slot.
        /// </summary>
        private void OnItemEquipCompleteFirstSlot()
        {
            ItemEquipComplete(0, false);
        }

        /// <summary>
        /// The equip animation has completed for the second item slot.
        /// </summary>
        private void OnItemEquipCompleteSecondSlot()
        {
            ItemEquipComplete(1, false);
        }

        /// <summary>
        /// The equip animation has completed for the third item slot.
        /// </summary>
        private void OnItemEquipCompleteThirdSlot()
        {
            ItemEquipComplete(2, false);
        }

        /// <summary>
        /// The animation is done equipping the item.
        /// </summary>
        /// <param name="slotID">The slot ID of the item that was unequipped.</param>
        /// <param name="canUpdate">Can the item be updated? If false the status enum will be set and the item will be updated within the Update loop.</param>
        private void ItemEquipComplete(int slotID, bool canUpdate)
        {
            // If the item can't be updated then the event should wait until the Update loop. This ensures items are updated in the proper order.
            if (!canUpdate) {
                m_EquipUnequipActions[slotID] = EquipUnequipAction.EquipComplete;
                return;
            }
            m_EquipUnequipActions[slotID] = EquipUnequipAction.Inactive;

            if (!m_CanEquip) {
                return;
            }
            var equipItem = m_EquipItems[slotID];
            if (equipItem == null) {
                return;
            }

            m_EquipItems[slotID] = null;
            Scheduler.Cancel(m_ItemEvents[slotID]);

            TryStopEquipUnequipAbility();
        }

        /// <summary>
        /// The animation event has indicated that the ability should stop.
        /// </summary>
        private void TryStopEquipUnequipAbility()
        {
            // Don't stop the ability unless all slots have been equipped/unequipped.
            var stopAbility = true;
            for (int i = 0; i < m_EquipItems.Length; ++i) {
                if (m_EquipItems[i] != null || m_UnequipItems[i] != null) {
                    stopAbility = false;
                    break;
                }
            }
            if (stopAbility) {
                StopAbility();
            }
        }

        /// <summary>
        /// The ability has stopped running.
        /// </summary>
        /// <param name="force">Was the ability force stopped?</param>
        protected override void AbilityStopped(bool force)
        {
            base.AbilityStopped(force);
            m_ImmediateEquipUnequip = false;

            if (force) {
                // If the ability was force stopped then it won't have a chance at finishing the item equip/unequip. Don't even try.
                for (int i = 0; i < m_Inventory.SlotCount; ++i) {
                    ForceEquipUnequip(i, false);
                }
            }

            m_CharacterLocomotion.UpdateItemAbilityAnimatorParameters();
            m_ItemSetManager.UpdateActiveItemSet(m_ItemSetCategoryIndex, m_ActiveItemSetIndex);
        }

        /// <summary>
        /// An item has been removed.
        /// </summary>
        /// <param name="item">The item that was removed.</param>
        /// <param name="slotID">The slot that the item was removed from.</param>
        private void OnRemoveItem(Item item, int slotID)
        {
            if (item.ItemType.CategoryIDMatch(m_ItemSetCategoryID)) {
                // The item may not be included in the active ItemSet.
                if (m_ActiveItemSetIndex == m_ItemSetManager.GetItemSetIndex(item, m_ItemSetCategoryIndex, false, true)) {
                    var prevImmediateEquipUnequip = m_ImmediateEquipUnequip;
                    // If the ItemSet contains an item that isn't being removed then the character should animate moving to the next ItemSet.
                    for (int i = 0; i < m_Inventory.SlotCount; ++i) {
                        var equipItemType = m_ItemSetManager.GetEquipItemType(m_ItemSetCategoryIndex, m_ActiveItemSetIndex, i);
                        if (equipItemType != null && equipItemType != item.ItemType) {
                            m_ImmediateEquipUnequip = false;
                            break;
                        }
                    }
                    StartEquipUnequip(m_ItemSetManager.GetDefaultItemSetIndex(m_ItemSetCategoryIndex), true, true);
                    m_ImmediateEquipUnequip = prevImmediateEquipUnequip;
                }
            }
        }

        /// <summary>
        /// The character has died.
        /// </summary>
        /// <param name="position">The position of the force.</param>
        /// <param name="force">The amount of force which killed the character.</param>
        /// <param name="attacker">The GameObject that killed the character.</param>
        private void OnDeath(Vector3 position, Vector3 force, GameObject attacker)
        {
            m_PrevActiveItemSetIndex = m_ActiveItemSetIndex;
            // Don't immediately unequip if in first person view to allow the arms move off the screen.
            StartEquipUnequip(-1, false, !m_CharacterLocomotion.FirstPersonPerspective);

            if (m_Inventory.RemoveAllOnDeath) {
                m_InventoryCount.Clear();
            }
        }

        /// <summary>
        /// The inventory has reloaded after respawning. 
        /// </summary>
        private void OnInventoryRespawned()
        {
            // If the items weren't removed upon death then the ability should equip what they previously had equipped.
            if (!m_Inventory.RemoveAllOnDeath && m_PrevActiveItemSetIndex != -1) {
                StartEquipUnequip(m_PrevActiveItemSetIndex, true, true);

                // No equip animations need to play - it should be like the character started fresh.
                m_CharacterLocomotion.UpdateItemAbilityAnimatorParameters();
                EventHandler.ExecuteEvent(m_GameObject, "OnCharacterSnapAnimator");
            }
        }

        /// <summary>
        /// Called when the character is destroyed.
        /// </summary>
        public override void OnDestroy()
        {
            base.OnDestroy();

            EventHandler.UnregisterEvent(m_GameObject, "OnItemPickupStartPickup", WillStartPickup);
            EventHandler.UnregisterEvent(m_GameObject, "OnItemPickupStopPickup", StopPickup);
            EventHandler.UnregisterEvent<Item, float, bool, bool>(m_GameObject, "OnInventoryPickupItem", OnPickupItem);
            EventHandler.UnregisterEvent<ItemType, float, bool, bool>(m_GameObject, "OnInventoryPickupItemType", OnPickupItemType);
            EventHandler.UnregisterEvent(m_GameObject, "OnAnimatorItemUnequip", OnItemUnequip);
            EventHandler.UnregisterEvent(m_GameObject, "OnAnimatorItemUnequipComplete", OnItemUnequipComplete);
            EventHandler.UnregisterEvent(m_GameObject, "OnAnimatorItemUnequipFirstSlot", OnItemUnequipFirstSlot);
            EventHandler.UnregisterEvent(m_GameObject, "OnAnimatorItemUnequipCompleteFirstSlot", OnItemUnequipCompleteFirstSlot);
            EventHandler.UnregisterEvent(m_GameObject, "OnAnimatorItemUnequipSecondSlot", OnItemUnequipSecondSlot);
            EventHandler.UnregisterEvent(m_GameObject, "OnAnimatorItemUnequipCompleteSecondSlot", OnItemUnequipCompleteSecondSlot);
            EventHandler.UnregisterEvent(m_GameObject, "OnAnimatorItemUnequipThirdSlot", OnItemUnequipThirdSlot);
            EventHandler.UnregisterEvent(m_GameObject, "OnAnimatorItemUnequipCompleteThirdSlot", OnItemUnequipCompleteThirdSlot);
            EventHandler.UnregisterEvent(m_GameObject, "OnAnimatorItemEquip", OnItemEquip);
            EventHandler.UnregisterEvent(m_GameObject, "OnAnimatorItemEquipComplete", OnItemEquipComplete);
            EventHandler.UnregisterEvent(m_GameObject, "OnAnimatorItemEquipFirstSlot", OnItemEquipFirstSlot);
            EventHandler.UnregisterEvent(m_GameObject, "OnAnimatorItemEquipCompleteFirstSlot", OnItemEquipCompleteFirstSlot);
            EventHandler.UnregisterEvent(m_GameObject, "OnAnimatorItemEquipSecondSlot", OnItemEquipSecondSlot);
            EventHandler.UnregisterEvent(m_GameObject, "OnAnimatorItemEquipCompleteSecondSlot", OnItemEquipCompleteSecondSlot);
            EventHandler.UnregisterEvent(m_GameObject, "OnAnimatorItemEquipThirdSlot", OnItemEquipThirdSlot);
            EventHandler.UnregisterEvent(m_GameObject, "OnAnimatorItemEquipCompleteThirdSlot", OnItemEquipCompleteThirdSlot);
            EventHandler.UnregisterEvent<Item, int>(m_GameObject, "OnInventoryRemoveItem", OnRemoveItem);
            EventHandler.UnregisterEvent<bool, bool>(m_GameObject, "OnAimAbilityStart", OnAim);
            EventHandler.UnregisterEvent<Vector3, Vector3, GameObject>(m_GameObject, "OnDeath", OnDeath);
            EventHandler.UnregisterEvent(m_GameObject, "OnInventoryRespawned", OnInventoryRespawned);
        }
    }
}