﻿/// ---------------------------------------------
/// Ultimate Character Controller
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------

using UnityEditor;
using Opsive.UltimateCharacterController.Utility;

namespace Opsive.UltimateCharacterController.Networking.Editor.Utility
{
    /// <summary>
    /// Editor script which will define or remove the Ultimate Character Controller compiler symbols so the components are aware of the asset import status.
    /// </summary>
    [InitializeOnLoad]
    public class DefineCompilerSymbols
    {
        private static string s_FirstPersonControllerSymbol = "FIRST_PERSON_CONTROLLER";
        private static string s_ThirdPersonControllerSymbol = "THIRD_PERSON_CONTROLLER";
        private static string s_ShooterSymbol = "ULTIMATE_CHARACTER_CONTROLLER_SHOOTER";
        private static string s_FirstPersonShooterSymbol = "FIRST_PERSON_SHOOTER";
        private static string s_MeleeSymbol = "ULTIMATE_CHARACTER_CONTROLLER_MELEE";
        private static string s_FirstPersonMeleeSymbol = "FIRST_PERSON_MELEE";
        private static string s_MultiplayerSymbol = "ULTIMATE_CHARACTER_CONTROLLER_MULTIPLAYER";
        private static string s_VRSymbol = "ULTIMATE_CHARACTER_CONTROLLER_VR";
#if ULTIMATE_CHARACTER_CONTROLLER_CINEMACHINE
        private static string s_CinemachineSymbol = "ULTIMATE_CHARACTER_CONTROLLER_CINEMACHINE";
#endif

        /// <summary>
        /// If the specified classes exist then the compiler symbol should be defined, otherwise the symbol should be removed.
        /// </summary>
        static DefineCompilerSymbols()
        {
            // The First Person Controller Combat MovementType will exist when the First Person Controller asset is imported.
            var firstPersonControllerExists = UnityEngineUtility.GetType("Opsive.UltimateCharacterController.FirstPersonController.Character.MovementTypes.Combat") != null;
#if FIRST_PERSON_CONTROLLER
            if (!firstPersonControllerExists) {
                RemoveSymbol(s_FirstPersonControllerSymbol);
            }
#else
            if (firstPersonControllerExists) {
                AddSymbol(s_FirstPersonControllerSymbol);
            }
#endif

            // The Third Person Controller Combat MovementType will exist when the Third Person Controller asset is imported.
            var thirdPersonControllerExists = UnityEngineUtility.GetType("Opsive.UltimateCharacterController.ThirdPersonController.Character.MovementTypes.Combat") != null;
#if THIRD_PERSON_CONTROLLER
            if (!thirdPersonControllerExists) {
                RemoveSymbol(s_ThirdPersonControllerSymbol);
            }
#else
            if (thirdPersonControllerExists) {
                AddSymbol(s_ThirdPersonControllerSymbol);
            }
#endif

            // Shootable Weapon will exist if the shooter controller is imported.
            var shootableWeaponExists = UnityEngineUtility.GetType("Opsive.UltimateCharacterController.Items.Actions.ShootableWeapon") != null;
#if ULTIMATE_CHARACTER_CONTROLLER_SHOOTER
            if (!shootableWeaponExists) {
                RemoveSymbol(s_ShooterSymbol);
            }
#else
            if (shootableWeaponExists) {
                AddSymbol(s_ShooterSymbol);
            }
#endif

            // First Person Shootable Weapon Properties will exist if the first person shootable controller is imported.
            var firstPersonShootableWeaponExists = UnityEngineUtility.GetType("Opsive.UltimateCharacterController.FirstPersonController.Items.FirstPersonShootableWeaponProperties") != null;
#if FIRST_PERSON_SHOOTER
            if (!firstPersonShootableWeaponExists) {
                RemoveSymbol(s_FirstPersonShooterSymbol);
            }
#else
            if (firstPersonShootableWeaponExists) {
                AddSymbol(s_FirstPersonShooterSymbol);
            }
#endif

            // Melee Weapon will exist if the melee controller is imported.
            var meleeWeaponExists = UnityEngineUtility.GetType("Opsive.UltimateCharacterController.Items.Actions.MeleeWeapon") != null;
#if ULTIMATE_CHARACTER_CONTROLLER_MELEE
            if (!meleeWeaponExists) {
                RemoveSymbol(s_MeleeSymbol);
            }
#else
            if (meleeWeaponExists) {
                AddSymbol(s_MeleeSymbol);
            }
#endif

            // First Person Melee Weapon Properties will exist if the first person melee controller is imported.
            var firstPersonMeleeWeaponExists = UnityEngineUtility.GetType("Opsive.UltimateCharacterController.FirstPersonController.Items.FirstPersonMeleeWeaponProperties") != null;
#if FIRST_PERSON_MELEE
            if (!firstPersonMeleeWeaponExists) {
                RemoveSymbol(s_FirstPersonMeleeSymbol);
            }
#else
            if (firstPersonMeleeWeaponExists) {
                AddSymbol(s_FirstPersonMeleeSymbol);
            }
#endif

            // INetworkCharacter will exist if the multiplayer add-on is imported.
            var multiplayerExists = UnityEngineUtility.GetType("Opsive.UltimateCharacterController.AddOns.Multiplayer.Character.NetworkCharacterLocomotionHandler") != null;
#if ULTIMATE_CHARACTER_CONTROLLER_MULTIPLAYER
            if (!multiplayerExists) {
                RemoveSymbol(s_MultiplayerSymbol);
            }
#else
            if (multiplayerExists) {
                AddSymbol(s_MultiplayerSymbol);
            }
#endif

            // VRViewType will exist if the VR add-on is imported.
            var VRExists = UnityEngineUtility.GetType("Opsive.UltimateCharacterController.VR.Camera.ViewTypes.VRViewType") != null;
#if ULTIMATE_CHARACTER_CONTROLLER_VR
            if (!VRExists) {
                RemoveSymbol(s_VRSymbol);
            }
#else
            if (VRExists) {
                AddSymbol(s_VRSymbol);
            }
#endif

#if ULTIMATE_CHARACTER_CONTROLLER_CINEMACHINE
            // TODO: CinemachineBrain will exist if Cinemachine is imported. As of 2.1.2 Cinemachine is a separate integration so this can be removed later.
            var cinemachineExists = UnityEngineUtility.GetType("Cinemachine.CinemachineBrain") != null;
            if (!cinemachineExists) {
                RemoveSymbol(s_CinemachineSymbol);
            }
#endif
        }

        /// <summary>
        /// Adds the specified symbol to the compiler definitions.
        /// </summary>
        /// <param name="symbol">The symbol to add.</param>
        private static void AddSymbol(string symbol)
        {
            var symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            if (symbols.Contains(symbol)) {
                return;
            }
            symbols += (";" + symbol);
            PlayerSettings.SetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup, symbols);
        }

        /// <summary>
        /// Remove the specified symbol from the compiler definitions.
        /// </summary>
        /// <param name="symbol">The symbol to remove.</param>
        private static void RemoveSymbol(string symbol)
        {
            var symbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            if (!symbols.Contains(symbol)) {
                return;
            }
            if (symbols.Contains(";" + symbol)) {
                symbols = symbols.Replace(";" + symbol, "");
            } else {
                symbols = symbols.Replace(symbol, "");
            }
            PlayerSettings.SetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup, symbols);
        }
    }
}