using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
using Object = UnityEngine.Object;

namespace DreadScripts.VRCSDKPlus
{
    internal sealed partial class VRCSDKPlus
    {
        #region Help Icon
        private static bool helpIconOverridden;
        private static Func<Rect, Object[], bool> defaultHelpIcon;

        /// <summary>
        /// The help icon in the inspector's header leads to a page of the Unity manual that doesn't exist for expression
        /// menus and parameters, because their classes don't have a help link. This replaces it, only for those two types.
        /// </summary>
        private static void OverrideHelpIcon()
        {
            if (helpIconOverridden) return;

            // Unity creates this list when it draws the first inspector header.
            IList headerItems = typeof(EditorGUIUtility).GetField("s_EditorHeaderItemsMethods", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null) as IList;
            if (headerItems == null) return;

            // Only tried once. If Unity changes this, its own icon stays.
            helpIconOverridden = true;
            try
            {
                for (int i = 0; i < headerItems.Count; i++)
                {
                    Delegate item = (Delegate)headerItems[i];
                    if (item.Method.Name != "HelpIconButton" || item.Method.DeclaringType != typeof(EditorGUI)) continue;

                    defaultHelpIcon = (Func<Rect, Object[], bool>)Delegate.CreateDelegate(typeof(Func<Rect, Object[], bool>), item.Method);
                    MethodInfo replacement = typeof(VRCSDKPlus).GetMethod(nameof(DrawHelpIcon), BindingFlags.Static | BindingFlags.NonPublic);
                    headerItems[i] = Delegate.CreateDelegate(item.GetType(), replacement);
                    return;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VRCSDK+] The help icon couldn't be changed: {e.Message}");
            }
        }

        private static bool DrawHelpIcon(Rect rect, Object[] targets)
        {
            if (!(targets[0] is VRCExpressionsMenu) && !(targets[0] is VRCExpressionParameters))
                return defaultHelpIcon(rect, targets);

            // Like Unity's own icon, it also works when the inspector is disabled.
            bool wasEnabled = GUI.enabled;
            GUI.enabled = true;
            GUIContent content = new GUIContent(EditorGUIUtility.IconContent("_Help")) { tooltip = "Expressions Menu and Controls Documentation" };
            if (GUI.Button(rect, content, EditorStyles.iconButton))
                Application.OpenURL(DocumentationUrl);
            GUI.enabled = wasEnabled;
            return true;
        }
        #endregion
    }
}
