﻿/// ---------------------------------------------
/// Ultimate Character Controller
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------

using UnityEngine;
using Opsive.UltimateCharacterController.Game;
using Opsive.UltimateCharacterController.Events;
using Opsive.UltimateCharacterController.Traits;
using Opsive.UltimateCharacterController.Objects.CharacterAssist;
using Opsive.UltimateCharacterController.Utility;

namespace Opsive.UltimateCharacterController.Character.Abilities
{
    /// <summary>
    /// Interacts with another object within the scene. The object that the ability interacts with must have the Interact component added to it.
    /// </summary>
    [DefaultStartType(AbilityStartType.ButtonDown)]
    [DefaultInputName("Action")]
    [DefaultAbilityIndex(9)]
    [DefaultAllowPositionalInput(false)]
    [DefaultAllowRotationalInput(false)]
    [AllowMultipleAbilityTypes]
    public class Interact : DetectObjectAbilityBase
    {
        [Tooltip("The ID of the Interactable. A value of -1 indicates no ID.")]
        [SerializeField] protected int m_InteractableID = -1;
        [Tooltip("The value of the AbilityIntData animator parameter.")]
        [SerializeField] protected int m_AbilityIntDataValue;
        [Tooltip("Specifies if the ability should wait for the OnAnimatorInteract animation event or wait for the specified duration before interacting with the item.")]
        [SerializeField] protected AnimationEventTrigger m_InteractEvent = new AnimationEventTrigger(false, 0.2f);
        [Tooltip("Specifies if the ability should wait for the OnAnimatorInteractComplete animation event or wait for the specified duration before stopping the ability.")]
        [SerializeField] protected AnimationEventTrigger m_InteractCompleteEvent = new AnimationEventTrigger(false, 0.2f);

        public int InteractableID { get { return m_InteractableID; } set { m_InteractableID = value; } }
        public int AbilityIntDataValue { get { return m_AbilityIntDataValue; } set { m_AbilityIntDataValue = value; } }
        public AnimationEventTrigger InteractEvent { get { return m_InteractEvent; } set { m_InteractEvent = value; } }
        public AnimationEventTrigger InteractCompleteEvent { get { return m_InteractCompleteEvent; } set { m_InteractCompleteEvent = value; } }

        private CharacterIKBase m_CharacterIK;
        private Interactable m_Interactable;
        private ScheduledEventBase[] m_DisableIKInteractionEvents;
        private bool m_HasInteracted;
        private bool m_ExitedTrigger;

        public override int AbilityIntData { get { return m_AbilityIntDataValue; } }
        public override string AbilityMessageText
        {
            get
            {
                var message = m_AbilityMessageText;
                if (m_Interactable != null) {
                    message = string.Format(message, m_Interactable.AbilityMessage());
                }
                return message;
            }
            set { base.AbilityMessageText = value; }
        }

#if UNITY_EDITOR
        public override string AbilityDescription { get { if (m_InteractableID != -1) { return "Interactable " + m_InteractableID; } return string.Empty; } }
#endif

        /// <summary>
        /// Initialize the default values.
        /// </summary>
        public override void Awake()
        {
            base.Awake();

            m_CharacterIK = m_GameObject.GetCachedComponent<CharacterIKBase>();

            EventHandler.RegisterEvent(m_GameObject, "OnAnimatorInteract", DoInteract);
            EventHandler.RegisterEvent(m_GameObject, "OnAnimatorInteractComplete", InteractComplete);
        }

        /// <summary>
        /// Called when the ablity is tried to be started. If false is returned then the ability will not be started.
        /// </summary>
        /// <returns>True if the ability can be started.</returns>
        public override bool CanStartAbility()
        {
            // The base class may prevent the ability from starting.
            if (!base.CanStartAbility()) {
                m_Interactable = null;
                return false;
            }

            // The ability can't start if the Interactable isn't ready. 
            if (!m_Interactable.CanInteract(m_GameObject)) {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Returns the possible AbilityStartLocations that the character can move towards.
        /// </summary>
        /// <returns>The possible AbilityStartLocations that the character can move towards.</returns>
        public override AbilityStartLocation[] GetStartLocations()
        {
            return m_Interactable.gameObject.GetCachedComponents<AbilityStartLocation>();
        }

        /// <summary>
        /// Validates the object to ensure it is valid for the current ability.
        /// </summary>
        /// <param name="obj">The object being validated.</param>
        /// <param name="fromTrigger">Is the object being validated from a trigger?</param>
        /// <returns>True if the object is valid. The object may not be valid if it doesn't have an ability-specific component attached.</returns>
        protected override bool ValidateObject(GameObject obj, bool fromTrigger)
        {
            if (!base.ValidateObject(obj, fromTrigger)) {
                return false;
            }

            if (m_Interactable != null) {
                return obj == m_Interactable.gameObject;
            }

            // The object must have the Interactable component.
            if ((m_Interactable = obj.GetCachedComponent<Interactable>()) != null) {
                // If the ID is used then the IDs must match.
                if (m_InteractableID != -1 && m_Interactable.ID != m_InteractableID) {
                    m_Interactable = null;
                    return false;
                }

                return true;
            }
            return false;
        }

        /// <summary>
        /// Called when another ability is attempting to start and the current ability is active.
        /// Returns true or false depending on if the new ability should be blocked from starting.
        /// </summary>
        /// <param name="startingAbility">The ability that is starting.</param>
        /// <returns>True if the ability should be blocked.</returns>
        public override bool ShouldBlockAbilityStart(Ability startingAbility)
        {
            return (startingAbility is Items.ItemAbility) || startingAbility.Index > Index || startingAbility is StoredInputAbilityBase;
        }

        /// <summary>
        /// Called when the current ability is attempting to start and another ability is active.
        /// Returns true or false depending on if the active ability should be stopped.
        /// </summary>
        /// <param name="activeAbility">The ability that is currently active.</param>
        /// <returns>True if the ability should be stopped.</returns>
        public override bool ShouldStopActiveAbility(Ability activeAbility)
        {
            return activeAbility is HeightChange || activeAbility is StoredInputAbilityBase;
        }

        /// <summary>
        /// The ability has started.
        /// </summary>
        protected override void AbilityStarted()
        {
            base.AbilityStarted();
            m_HasInteracted = false;
            m_ExitedTrigger = false;

            if (!m_InteractEvent.WaitForAnimationEvent) {
                Scheduler.ScheduleFixed(m_InteractEvent.Duration, DoInteract);
            }

            // The interactable can move the limbs to a specific location.
            if (m_CharacterIK != null) {
                for (int i = 0; i < m_Interactable.IKTargets.Length; ++i) {
                    var ikTarget = m_Interactable.IKTargets[i];
                    if (ikTarget.Goal != CharacterIKBase.IKGoal.Last) {
                        Scheduler.ScheduleFixed<AbilityIKTarget, Transform>(ikTarget.Delay, SetIKTarget, ikTarget, ikTarget.Transform);
                    }
                }
            }
        }

        /// <summary>
        /// Sets the IK target.
        /// </summary>
        /// <param name="ikTarget">The IK target that should be set.</param>
        /// <param name="targetTransform">The transform target that should be set.</param>
        private void SetIKTarget(AbilityIKTarget ikTarget, Transform targetTransform)
        {
            m_CharacterIK.SetAbilityIKTarget(targetTransform, ikTarget.Goal, ikTarget.InterpolationDuration);
            // If the transform is not null then the end should be scheduled so it can be set to null.
            if (targetTransform != null) {
                if (m_DisableIKInteractionEvents == null) {
                    m_DisableIKInteractionEvents = new ScheduledEventBase[m_Interactable.IKTargets.Length];
                } else if (m_DisableIKInteractionEvents.Length < m_Interactable.IKTargets.Length) {
                    System.Array.Resize(ref m_DisableIKInteractionEvents, m_Interactable.IKTargets.Length);
                }
                for (int i = 0; i < m_DisableIKInteractionEvents.Length; ++i) {
                    if (m_DisableIKInteractionEvents[i] == null) {
                        m_DisableIKInteractionEvents[i] = Scheduler.ScheduleFixed<AbilityIKTarget, Transform>(ikTarget.Duration, (AbilityIKTarget abilityIKTarget, Transform target) =>
                        {
                            SetIKTarget(ikTarget, target);
                            m_DisableIKInteractionEvents[i] = null;
                        }, ikTarget, null);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Interacts with the object.
        /// </summary>
        private void DoInteract()
        {
            if (!IsActive || m_HasInteracted) {
                return;
            }

            m_Interactable.Interact(m_GameObject);
            m_HasInteracted = true;

            if (!m_InteractCompleteEvent.WaitForAnimationEvent) {
                Scheduler.ScheduleFixed(m_InteractCompleteEvent.Duration, InteractComplete);
            }
        }
        
        /// <summary>
        /// Completes the ability.
        /// </summary>
        private void InteractComplete()
        {
            if (!IsActive) {
                return;
            }

            StopAbility();
        }

        /// <summary>
        /// The character has exited a trigger.
        /// </summary>
        /// <param name="other">The GameObject that the character exited.</param>
        /// <returns>Returns true if the entered object leaves the trigger.</returns>
        protected override bool TriggerExit(GameObject other)
        {
            if (IsActive) {
                m_ExitedTrigger = true;
                return false;
            }

            if (base.TriggerExit(other)) {
                m_Interactable = null;
                return true;
            }
            return false;
        }

        /// <summary>
        /// The ability has stopped running.
        /// </summary>
        /// <param name="force">Was the ability force stopped?</param>
        protected override void AbilityStopped(bool force)
        {
            base.AbilityStopped(force);

            if (m_ExitedTrigger) {
                m_Interactable = null;
                m_DetectedTriggerObjectsCount = 0;
                m_DetectedObject = null;
                m_ExitedTrigger = false;
            }
            // The ability may end before the interaction duration has elapsed.
            if (m_DisableIKInteractionEvents != null) {
                for (int i = 0; i < m_DisableIKInteractionEvents.Length; ++i) {
                    if (m_DisableIKInteractionEvents[i] == null) {
                        continue;
                    }
                    m_DisableIKInteractionEvents[i].Invoke();
                    Scheduler.Cancel(m_DisableIKInteractionEvents[i]);
                    m_DisableIKInteractionEvents[i] = null;
                }
            }
        }

        /// <summary>
        /// The object has been destroyed.
        /// </summary>
        public override void OnDestroy()
        {
            base.OnDestroy();

            EventHandler.UnregisterEvent(m_GameObject, "OnAnimatorInteract", DoInteract);
            EventHandler.UnregisterEvent(m_GameObject, "OnAnimatorInteractComplete", InteractComplete);
        }
    }
}