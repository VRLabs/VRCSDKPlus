using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using Object = UnityEngine.Object;

namespace DreadScripts.VRCSDKPlus
{
    internal sealed partial class VRCSDKPlus
    {
        #region Helper Methods
        #region Clickables
        internal static bool ClickableButton(string     label, GUIStyle                 style = null, params GUILayoutOption[] options) => ClickableButton(new GUIContent(label), style, options);
        internal static bool ClickableButton(string     label, params GUILayoutOption[] options) => ClickableButton(new GUIContent(label), null, options);
        internal static bool ClickableButton(GUIContent label, params GUILayoutOption[] options) => ClickableButton(label,                 null, options);
        internal static bool ClickableButton(GUIContent label, GUIStyle style = null, params GUILayoutOption[] options)
        {
            if (style == null)
                style = GUI.skin.button;
            bool clicked = GUILayout.Button(label, style, options);
            if (GUI.enabled) w_MakeRectLinkCursor();
            return clicked;
        }
        internal static void w_MakeRectLinkCursor(Rect rect = default)
        {
            if (!GUI.enabled) return;
            if (Event.current.type == EventType.Repaint)
            {
                if (rect == default) rect = GUILayoutUtility.GetLastRect();
                EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
            }
        }
        internal static bool w_MakeRectClickable(Rect rect = default)
        {
            if (rect == default) rect = GUILayoutUtility.GetLastRect();
            w_MakeRectLinkCursor(rect);
            var e = Event.current;
            return e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition);
        }
        #endregion

        private const string DocumentationUrl = "https://creators.vrchat.com/avatars/expression-menu-and-controls/";

        private static void Link(string label, string url)
        {
            var bgcolor = GUI.backgroundColor;
            GUI.backgroundColor = Color.clear;

            if (GUILayout.Button(new GUIContent(label, url), VRCSDKPlusToolbox.Styles.Label.faintLinkLabel))
                Application.OpenURL(url);
            w_UnderlineLastRectOnHover();
            
            GUI.backgroundColor = bgcolor;
        }
        
        private static void DrawMaintainedBy()
        {
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                GUILayout.Label("Maintained by", VRCSDKPlusToolbox.Styles.Label.Watermark);
                Link("VRLabs", "https://vrlabs.dev/");
            }
        }

        internal static void w_UnderlineLastRectOnHover(Color? color = null)
        {
            if (color == null) color = new Color(0.3f, 0.7f, 1);
            if (Event.current.type == EventType.Repaint)
            {
                var rect = GUILayoutUtility.GetLastRect();
                var mp = Event.current.mousePosition;
                if (rect.Contains(mp)) EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1, rect.width, 1), color.Value);
                EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
            }

        }
        
        internal static System.Type ExtendedGetType(string typeName)
        {
            var myType = System.Type.GetType(typeName);
            if (myType != null)
                return myType;
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                // One assembly that fails to load some of its types shouldn't break the whole lookup.
                try { types = assembly.GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }
                myType = types.FirstOrDefault(t  => t.FullName == typeName);
                if (myType != null)
                    return myType;
                myType = types.FirstOrDefault(t => t.Name == typeName);
                if (myType != null)
                    return myType;
            }
            return null;
        }
        /// <summary>All avatars in the open scenes, including inactive ones.</summary>
        private static VRCAvatarDescriptor[] FindAvatars()
        {
#if UNITY_6000_0_OR_NEWER
            // FindObjectsOfType is obsolete in Unity 6. Sorting by instance ID keeps the same order it had.
            return Object.FindObjectsByType<VRCAvatarDescriptor>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);
#else
            return Object.FindObjectsOfType<VRCAvatarDescriptor>(true);
#endif
        }

        internal static void RefreshAvatar(ref VRCAvatarDescriptor avatar, ref VRCAvatarDescriptor[] validAvatars, Action OnAvatarChanged = null, Func<VRCAvatarDescriptor, bool> favoredAvatar = null)
        {
            // Include inactive avatars, so a deactivated active avatar stays in the list.
            validAvatars = FindAvatars();

            // Prefer the avatar the caller asks for (e.g. the one that uses this parameters asset),
            // even when another avatar is still selected from earlier.
            if (favoredAvatar != null)
            {
                VRCAvatarDescriptor favored = validAvatars.FirstOrDefault(a => a && favoredAvatar(a));
                if (favored && favored != avatar)
                {
                    avatar = favored;
                    OnAvatarChanged?.Invoke();
                    return;
                }
            }

            if (avatar) return;

            if (validAvatars.Length > 0)
            {
                if (favoredAvatar != null)
                    avatar = validAvatars.FirstOrDefault(favoredAvatar) ?? validAvatars[0];
                else avatar = validAvatars[0];
            }

            OnAvatarChanged?.Invoke();
        }

        internal static bool DrawAdvancedAvatarFull(ref VRCAvatarDescriptor avatar, VRCAvatarDescriptor[] validAvatars, Action OnAvatarChanged = null, bool warnNonHumanoid = true, bool warnPrefab = true, bool warnDoubleFX = true, string label = "Avatar", string tooltip = "The Targeted VRCAvatar", Action ExtraGUI = null)
            => DrawAdvancedAvatarField(ref avatar, validAvatars, OnAvatarChanged, label, tooltip, ExtraGUI) && DrawAdvancedAvatarWarning(avatar, warnNonHumanoid, warnPrefab, warnDoubleFX);

        private static VRCAvatarDescriptor DrawAdvancedAvatarField(ref VRCAvatarDescriptor avatar, VRCAvatarDescriptor[] validAvatars, Action OnAvatarChanged = null, string label = "Avatar", string tooltip = "The Targeted VRCAvatar", Action ExtraGUI = null)
        {
            using (new GUILayout.HorizontalScope())
            {
                var avatarContent = new GUIContent(label, tooltip);
                if (validAvatars == null || validAvatars.Length <= 0) EditorGUILayout.LabelField(avatarContent, new GUIContent("No Avatar Descriptors Found"));
                else
                {
                    // Use the same filtered list for the names, the current index and the picked avatar,
                    // so a deleted avatar can't shift the choices.
                    VRCAvatarDescriptor[] aliveAvatars = validAvatars.Where(a => a).ToArray();
                    if (AvatarPopup(avatarContent, ref avatar, aliveAvatars, validAvatars))
                    {
                        EditorGUIUtility.PingObject(avatar);
                        OnAvatarChanged?.Invoke();
                    }
                }

                ExtraGUI?.Invoke();
            }
            return avatar;
        }

        private static object avatarPickOwner;
        private static VRCAvatarDescriptor pickedAvatar;

        /// <summary>
        /// A dropdown of avatars, split into the ones that are active in the scene and the ones that aren't.
        /// Returns true when another one was picked. <paramref name="owner"/> tells apart the dropdowns that are drawn at the same time.
        /// </summary>
        private static bool AvatarPopup(GUIContent label, ref VRCAvatarDescriptor current, VRCAvatarDescriptor[] avatars, object owner)
        {
            Rect rect = EditorGUILayout.GetControlRect();
            Rect popupRect = EditorGUI.PrefixLabel(rect, label);
            if (EditorGUI.DropdownButton(popupRect, new GUIContent(current ? current.name : string.Empty), FocusType.Keyboard, EditorStyles.popup))
                ShowAvatarMenu(popupRect, current, avatars, owner);

            // The menu's callback runs outside of OnInspectorGUI. Only changed on layout, so the layout stays the same until the next one.
            if (Event.current.type != EventType.Layout || avatarPickOwner == null || !ReferenceEquals(avatarPickOwner, owner)) return false;
            avatarPickOwner = null;
            if (!pickedAvatar || pickedAvatar == current) return false;
            current = pickedAvatar;
            GUI.changed = true;
            return true;
        }

        private static void ShowAvatarMenu(Rect position, VRCAvatarDescriptor current, VRCAvatarDescriptor[] avatars, object owner)
        {
            GenericMenu menu = new GenericMenu { allowDuplicateNames = true };

            void AddSection(string title, VRCAvatarDescriptor[] section)
            {
                if (section.Length == 0) return;
                if (menu.GetItemCount() > 0) menu.AddSeparator(string.Empty);
                // Only a heading.
                menu.AddDisabledItem(new GUIContent(title));
                menu.AddSeparator(string.Empty);
                foreach (VRCAvatarDescriptor descriptor in section)
                {
                    // A slash would make a submenu.
                    menu.AddItem(new GUIContent(descriptor.name.Replace('/', '\u2215')), descriptor == current, () =>
                    {
                        avatarPickOwner = owner;
                        pickedAvatar = descriptor;
                        InternalEditorUtility.RepaintAllViews();
                    });
                }
            }

            AddSection("Active", avatars.Where(a => a && a.gameObject.activeInHierarchy).ToArray());
            AddSection("Inactive", avatars.Where(a => a && !a.gameObject.activeInHierarchy).ToArray());
            menu.DropDown(position);
        }

        private static bool DrawAdvancedAvatarWarning(VRCAvatarDescriptor avatar, bool warnNonHumanoid = true, bool warnPrefab = true, bool warnDoubleFX = true)
        {
            return (!warnPrefab || !DrawPrefabWarning(avatar)) && (!warnDoubleFX || !DrawDoubleFXWarning(avatar, warnNonHumanoid));
        }

        private static bool DrawPrefabWarning(VRCAvatarDescriptor avatar)
        {
            if (!avatar) return false;
            bool isPrefab = PrefabUtility.IsPartOfAnyPrefab(avatar.gameObject);
            if (isPrefab)
            {
                EditorGUILayout.HelpBox("Target Avatar is a part of a prefab. Prefab unpacking is required.", MessageType.Error);
                if (GUILayout.Button("Unpack")) PrefabUtility.UnpackPrefabInstance(avatar.gameObject, PrefabUnpackMode.Completely, InteractionMode.UserAction);
            }
            return isPrefab;
        }
        private static bool DrawDoubleFXWarning(VRCAvatarDescriptor avatar, bool warnNonHumanoid = true)
        {
            if (!avatar) return false;
            var layers = avatar.baseAnimationLayers ?? Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>();

            if (layers.Length > 3)
            {
                var isDoubled = layers.Length > 4 && layers[3].type == layers[4].type;
                if (isDoubled)
                {
                    EditorGUILayout.HelpBox("Your Avatar's Action playable layer is set as FX. This is an uncommon bug.", MessageType.Error);
                    if (GUILayout.Button("Fix"))
                    {
                        Undo.RecordObject(avatar, "Fix Action Layer");
                        avatar.baseAnimationLayers[3].type = VRCAvatarDescriptor.AnimLayerType.Action;
                        EditorUtility.SetDirty(avatar);
                    }
                }

                return isDoubled;
            }

            if (warnNonHumanoid)
                EditorGUILayout.HelpBox("Your Avatar's descriptor is set as Non-Humanoid! Please make sure that your Avatar's rig is Humanoid.", MessageType.Error);
            return warnNonHumanoid;

        }

        private static void GreenLog(string msg) => Debug.Log($"<color=green>[VRCSDK+] </color>{msg}");
        #endregion
    }
}
