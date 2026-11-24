using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using AnimatorController = UnityEditor.Animations.AnimatorController;
using AnimatorControllerParameter = UnityEngine.AnimatorControllerParameter;

namespace DreadScripts.VRCSDKPlus
{
    internal sealed partial class VRCSDKPlus
    {
        private static bool initialized;
        private static GUIContent yellowWarnIcon;
        private static GUIStyle centeredLabel => new GUIStyle(GUI.skin.label) {alignment = TextAnchor.MiddleCenter};
        private static readonly string[] allPlayables =
        {
            "Base",
            "Additive",
            "Gesture",
            "Action",
            "FX",
            "Sitting",
            "TPose",
            "IKPose"
        };
        
        private static VRCAvatarDescriptor avatar;
        private static VRCAvatarDescriptor[] validAvatars;
        private static AnimatorControllerParameter[] validParameters;

        private static string[] validPlayables;
        private static int[] validPlayableIndexes;

        private static void InitConstants()
        {
            if (initialized)
            {
                return;
            }

            yellowWarnIcon = new GUIContent(EditorGUIUtility.IconContent("d_console.warnicon.sml"));
            initialized = true;
        }

        private static void RefreshAvatar(System.Func<VRCAvatarDescriptor, bool> favoredAvatar = null)
        {
            RefreshAvatar(ref avatar, ref validAvatars, null, favoredAvatar);
            RefreshAvatarInfo();
        }

        private static void RefreshAvatarInfo()
        {
            RefreshValidParameters();
            RefreshValidPlayables();
        }
        private static void RefreshValidParameters()
        {
            if (!avatar)
            {
                validParameters = Array.Empty<AnimatorControllerParameter>();
                return;
            }
            List<AnimatorControllerParameter> validParams = new List<AnimatorControllerParameter>();
            // The layer arrays are null on a descriptor whose inspector hasn't initialised them yet.
            var baseLayers = avatar.baseAnimationLayers ?? Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>();
            var specialLayers = avatar.specialAnimationLayers ?? Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>();
            foreach (var r in baseLayers.Concat(specialLayers).Select(p => p.animatorController).Concat(avatar.GetComponentsInChildren<Animator>(true).Select(a => a.runtimeAnimatorController)).Distinct())
            {
                if (!r) continue;

                AnimatorController c = AssetDatabase.LoadAssetAtPath<AnimatorController>(AssetDatabase.GetAssetPath(r));
                if (c) validParams.AddRange(c.parameters);
            }

            validParameters = validParams.Distinct().OrderBy(p => p.name).ToArray();
        }

        private static void RefreshValidPlayables()
        {
            if (!avatar)
            {
                validPlayables = Array.Empty<string>();
                validPlayableIndexes = Array.Empty<int>();
                return;
            }
            List<(string, int)> myPlayables = new List<(string, int)>();
            for (int i = 0; i < allPlayables.Length; i++)
            {
                int index = i == 0 ? i : i + 1;
                if (avatar.GetPlayableLayer((VRCAvatarDescriptor.AnimLayerType)index, out AnimatorController c))
                {
                    myPlayables.Add((allPlayables[i], index));
                }
            }

            validPlayables = new string[myPlayables.Count];
            validPlayableIndexes = new int[myPlayables.Count];
            for (int i = 0; i < myPlayables.Count; i++)
            {
                validPlayables[i] = myPlayables[i].Item1;
                validPlayableIndexes[i] = myPlayables[i].Item2;
            }
        }
    }
}
