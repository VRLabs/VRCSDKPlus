using UnityEditor;
using UnityEngine;

namespace DreadScripts.VRCSDKPlus
{
    internal static partial class VRCSDKPlusToolbox
    {
        public static class Styles
        {
            public const float Padding = 3;

            public static class Label
            {
                internal static readonly GUIStyle Centered
                    = new GUIStyle(GUI.skin.label) {alignment = TextAnchor.MiddleCenter};

                internal static readonly GUIStyle RichText
                    = new GUIStyle(GUI.skin.label) {richText = true};
                

                internal static readonly GUIStyle Type
                    = new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleRight,
                        normal =
                        {
                            textColor = EditorGUIUtility.isProSkin ? Color.gray : BrightnessToColor(91),
                        },
                        fontStyle = FontStyle.Italic,
                    };

                internal static readonly GUIStyle PlaceHolder
                    = new GUIStyle(Type)
                    {
                        fontSize = 11,
                        alignment = TextAnchor.MiddleLeft,
                        contentOffset = new Vector2(2.5f, 0)
                    };
                internal static readonly GUIStyle faintLinkLabel = new GUIStyle(PlaceHolder) { name = "Toggle", hover = { textColor = new Color(0.3f, 0.7f, 1) } };

                internal static readonly GUIStyle TypeFocused
                    = new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleRight,
                        normal =
                        {
                            textColor = EditorGUIUtility.isProSkin ? Color.white : Color.black,
                        },
                        fontStyle = FontStyle.Italic,
                    };

                internal static readonly GUIStyle TypeLabel = new GUIStyle(PlaceHolder) {contentOffset = new Vector2(-2.5f, 0)};
                internal static readonly GUIStyle RightPlaceHolder = new GUIStyle(TypeLabel) {alignment = TextAnchor.MiddleRight};
                internal static readonly GUIStyle Watermark
                    = new GUIStyle(PlaceHolder)
                    {
                        alignment = TextAnchor.MiddleRight,
                        fontSize  = 10,
                    };

                internal static readonly GUIStyle LabelDropdown
                    = new GUIStyle(GUI.skin.GetStyle("DropDownButton"))
                    {
                        alignment = TextAnchor.MiddleLeft,
                        contentOffset = new Vector2(2.5f, 0)
                    };

                internal static readonly GUIStyle RemoveIcon
                    = new GUIStyle(GUI.skin.GetStyle("RL FooterButton"));

            }

            internal static readonly GUIStyle icon = new GUIStyle(GUI.skin.label) {fixedWidth = 18, fixedHeight = 18};
            internal static readonly GUIStyle letterButton = 
                new GUIStyle(GUI.skin.button) { padding = new RectOffset(), margin = new RectOffset(1,1,1,1), richText = true};

        }

        public static class Strings
        {
            public const string IconCopy = "SaveActive";
            public const string IconPaste = "Clipboard";
            public const string IconMove = "MoveTool";
            public const string IconPlace = "DefaultSorting";
            public const string IconDuplicate = "TreeEditor.Duplicate";
            public const string IconHelp = "_Help";
            public const string IconWarn = "console.warnicon.sml";
            public const string IconError = "console.erroricon.sml";
            public const string IconFolder = "FolderOpened Icon";
            public const string IconRemove = "Toolbar Minus";
            public const string IconSearch = "Search Icon";

            public const string ClipboardPrefixControl = "[TAG=VSP_CONTROL]";

            public const string SettingsCompact = "VSP_Compact";
        }

        public static class GUIContent
        {
            public const string MissingParametersTooltip = "No Expression Parameters targeted. Auto-fill and warnings are disabled.";
            public const string MenuFullTooltip = "Menu's controls are already maxed out. (8/8)";
            public static readonly UnityEngine.GUIContent Copy
                = new UnityEngine.GUIContent(EditorGUIUtility.IconContent(VRCSDKPlusToolbox.Strings.IconCopy))
                {
                    tooltip = "Copy"
                };

            public static readonly UnityEngine.GUIContent Paste
                = new UnityEngine.GUIContent(EditorGUIUtility.IconContent(VRCSDKPlusToolbox.Strings.IconPaste))
                {
                    tooltip = "Paste"
                };

            public static readonly UnityEngine.GUIContent Move
                = new UnityEngine.GUIContent(EditorGUIUtility.IconContent(VRCSDKPlusToolbox.Strings.IconMove))
                {
                    tooltip = "Move"
                };
            public static readonly UnityEngine.GUIContent Place
                = new UnityEngine.GUIContent(EditorGUIUtility.IconContent(VRCSDKPlusToolbox.Strings.IconPlace))
                {
                    tooltip = "Place"
                };

            public static readonly UnityEngine.GUIContent Duplicate
                = new UnityEngine.GUIContent(EditorGUIUtility.IconContent(VRCSDKPlusToolbox.Strings.IconDuplicate))
                {
                    tooltip = "Duplicate"
                };

            public static readonly UnityEngine.GUIContent Help
                = new UnityEngine.GUIContent(EditorGUIUtility.IconContent(VRCSDKPlusToolbox.Strings.IconHelp));

            public static readonly UnityEngine.GUIContent Warn
                = new UnityEngine.GUIContent(EditorGUIUtility.IconContent(VRCSDKPlusToolbox.Strings.IconWarn));
            public static readonly UnityEngine.GUIContent Error
                = new UnityEngine.GUIContent(EditorGUIUtility.IconContent(VRCSDKPlusToolbox.Strings.IconError));

            private const string CrossForDarkThemeGuid = "78dfa9792b3b41e8a99acb8c2d39f005";
            private const string CrossForLightThemeGuid = "358188b6bcc34c9d9ad27adb8d243323";
            private static Texture2D crossForDarkTheme;
            private static Texture2D crossForLightTheme;
            private static readonly UnityEngine.GUIContent clear = new UnityEngine.GUIContent { tooltip = "Clear" };

            // Checked on every use, so it's also right after switching the editor theme.
            public static UnityEngine.GUIContent Clear
            {
                get
                {
                    Texture2D icon = EditorGUIUtility.isProSkin
                        ? LoadIcon(ref crossForDarkTheme, CrossForDarkThemeGuid)
                        : LoadIcon(ref crossForLightTheme, CrossForLightThemeGuid);
                    clear.image = icon;
                    clear.text = icon ? string.Empty : "X";
                    return clear;
                }
            }

            // Found by GUID, so it works wherever the package is installed.
            private static Texture2D LoadIcon(ref Texture2D icon, string guid)
            {
                if (!icon) icon = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid));
                return icon;
            }

            public static readonly UnityEngine.GUIContent Folder
                = new UnityEngine.GUIContent(EditorGUIUtility.IconContent(VRCSDKPlusToolbox.Strings.IconFolder))
                {
                    tooltip = "Open"
                };

            public static readonly UnityEngine.GUIContent Remove
                = new UnityEngine.GUIContent(EditorGUIUtility.IconContent(VRCSDKPlusToolbox.Strings.IconRemove)) {tooltip = "Remove element from list"};

            public static readonly UnityEngine.GUIContent Search
                = new UnityEngine.GUIContent(EditorGUIUtility.IconContent(VRCSDKPlusToolbox.Strings.IconSearch)) {tooltip = "Search"};
        }
    }
}
