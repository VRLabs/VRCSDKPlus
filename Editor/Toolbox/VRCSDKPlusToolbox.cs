using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using AnimatorController = UnityEditor.Animations.AnimatorController;
using Object = UnityEngine.Object;

namespace DreadScripts.VRCSDKPlus
{
    internal static partial class VRCSDKPlusToolbox
    {
        #region Ready Paths
		internal enum PathOption
		{
			Normal,
			ForceFolder,
			ForceFile
		}
		internal static string ReadyAssetPath(string path, bool makeUnique = false, PathOption pathOption = PathOption.Normal)
		{
			bool forceFolder = pathOption == PathOption.ForceFolder;
			bool forceFile = pathOption == PathOption.ForceFile;

			path = forceFile ? LegalizeName(path) : forceFolder ? LegalizePath(path) : LegalizeFullPath(path);
			bool isFolder = forceFolder || (!forceFile && string.IsNullOrEmpty(Path.GetExtension(path)));

			if (isFolder)
			{
				if (!Directory.Exists(path))
				{
					Directory.CreateDirectory(path);
					AssetDatabase.ImportAsset(path);
				}
				else if (makeUnique)
				{
					path = AssetDatabase.GenerateUniqueAssetPath(path);
					Directory.CreateDirectory(path);
					AssetDatabase.ImportAsset(path);
				}
			}
			else
			{
				const string basePath = "Assets";
				string folderPath = Path.GetDirectoryName(path)?.Replace('\\', '/'); // GetDirectoryName returns '\' on Windows
				string fileName = Path.GetFileName(path);
				string dataPath = Application.dataPath.Replace('\\', '/');

				if (string.IsNullOrEmpty(folderPath))
					folderPath = basePath;
				else if (folderPath == dataPath || folderPath.StartsWith(dataPath + "/"))
					folderPath = basePath + folderPath.Substring(dataPath.Length); // absolute path inside Assets
				else if (!IsProjectRelativeRoot(folderPath, basePath) && !IsProjectRelativeRoot(folderPath, "Packages"))
					folderPath = $"{basePath}/{folderPath}";

				if (folderPath != basePath && !Directory.Exists(folderPath))
				{
					Directory.CreateDirectory(folderPath);
					AssetDatabase.ImportAsset(folderPath);
				}

				path = $"{folderPath}/{fileName}";
				if (makeUnique)
					path = AssetDatabase.GenerateUniqueAssetPath(path);

			}

			return path;
		}

		// "Assets" or "Assets/..." (not "AssetsBackup/..."); same for "Packages", so menus in embedded packages stay there.
		private static bool IsProjectRelativeRoot(string folderPath, string root) =>
			folderPath == root || folderPath.StartsWith(root + "/", StringComparison.Ordinal);

		internal static string ReadyAssetPath(string folderPath, string fullNameOrExtension, bool makeUnique = false)
		{
			if (string.IsNullOrEmpty(fullNameOrExtension))
				return ReadyAssetPath(LegalizePath(folderPath), makeUnique, PathOption.ForceFolder);
			if (string.IsNullOrEmpty(folderPath))
				return ReadyAssetPath(LegalizeName(fullNameOrExtension), makeUnique, PathOption.ForceFile);

			return ReadyAssetPath($"{LegalizePath(folderPath)}/{LegalizeName(fullNameOrExtension)}", makeUnique);
		}
		internal static string ReadyAssetPath(Object buddyAsset, string fullNameOrExtension = "", bool makeUnique = true)
		{
			var buddyPath = AssetDatabase.GetAssetPath(buddyAsset);
			string folderPath = Path.GetDirectoryName(buddyPath);
			if (string.IsNullOrEmpty(fullNameOrExtension))
				fullNameOrExtension = Path.GetFileName(buddyPath);
			if (fullNameOrExtension.StartsWith("."))
			{
				string assetName = string.IsNullOrWhiteSpace(buddyAsset.name) ? "SomeAsset" : buddyAsset.name;
				fullNameOrExtension = $"{assetName}{fullNameOrExtension}";
			}

			return ReadyAssetPath(folderPath, fullNameOrExtension, makeUnique);
		}

		internal static string LegalizeFullPath(string path)
		{
			if (string.IsNullOrEmpty(path))
			{
				Debug.LogWarning("Legalizing empty path! Returned path as 'EmptyPath'");
				return "EmptyPath";
			}

			var ext = Path.GetExtension(path);
			bool isFolder = string.IsNullOrEmpty(ext);
			if (isFolder) return LegalizePath(path);

			string folderPath = Path.GetDirectoryName(path);
			var fileName = LegalizeName(Path.GetFileNameWithoutExtension(path));

			if (string.IsNullOrEmpty(folderPath)) return $"{fileName}{ext}";
			folderPath = LegalizePath(folderPath);

			return $"{folderPath}/{fileName}{ext}";
		}
		internal static string LegalizePath(string path)
		{
			string regexFolderReplace = Regex.Escape(new string(Path.GetInvalidPathChars()));

			path = path.Replace('\\', '/');
			if (path.IndexOf('/') > 0)
				path = string.Join("/", path.Split('/').Select(s => Regex.Replace(s, $@"[{regexFolderReplace}]", "-")));

			return path;

		}
		internal static string LegalizeName(string name)
		{
			string regexFileReplace = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
			return string.IsNullOrEmpty(name) ? "Unnamed" : Regex.Replace(name, $@"[{regexFileReplace}]", "-");
		}
		#endregion
        
        internal static bool TryGetActiveIndex(this ReorderableList orderList, out int index)
        {
            index = orderList.index;
            if (index < orderList.count && index >= 0) return true;
            index = -1;
            return false;
        }
        public static string GenerateUniqueString(string s, Func<string, bool> PassCondition, bool addNumberIfMissing = true)
        {
            if (PassCondition(s)) return s;
            // [0-9] rather than \d: \d also matches non-ASCII digits, which can't be incremented as numbers.
            var match = Regex.Match(s, @"([0-9]+)$");
            if (!match.Success && !addNumberIfMissing) return s;
            var numberString = match.Success ? match.Groups[1].Value : "1";
            if (!match.Success && !s.EndsWith(" ")) s += " ";
            var newString = Regex.Replace(s, @"[0-9]+$", string.Empty);
            while (!PassCondition($"{newString}{numberString}"))
                numberString = IncrementDigits(numberString);

            return $"{newString}{numberString}";
        }

        /// <summary>Adds 1 to a string of ASCII digits of any length, keeping leading zeros ("009" → "010", "99" → "100").</summary>
        internal static string IncrementDigits(string digits)
        {
            char[] chars = digits.ToCharArray();
            for (int i = chars.Length - 1; i >= 0; i--)
            {
                if (chars[i] != '9')
                {
                    chars[i]++;
                    return new string(chars);
                }
                chars[i] = '0';
            }
            return "1" + new string(chars);
        }
        public static class Container
        {
            public class Vertical : IDisposable
            {
                public Vertical(params GUILayoutOption[] options)
                    => EditorGUILayout.BeginVertical(GUI.skin.GetStyle("helpbox"), options);

                public Vertical(string title, params GUILayoutOption[] options)
                {
                    EditorGUILayout.BeginVertical(GUI.skin.GetStyle("helpbox"), options);

                    EditorGUILayout.LabelField(title, VRCSDKPlusToolbox.Styles.Label.Centered);
                }

                public void Dispose() => EditorGUILayout.EndVertical();
            }
            public class Horizontal : IDisposable
            {
                public Horizontal(params GUILayoutOption[] options)
                    => EditorGUILayout.BeginHorizontal(GUI.skin.GetStyle("helpbox"), options);

                public Horizontal(string title, params GUILayoutOption[] options)
                {
                    EditorGUILayout.BeginHorizontal(GUI.skin.GetStyle("helpbox"), options);

                    EditorGUILayout.LabelField(title, VRCSDKPlusToolbox.Styles.Label.Centered);
                }

                public void Dispose() => EditorGUILayout.EndHorizontal();
            }

            public static void BeginLayout(params GUILayoutOption[] options)
                => EditorGUILayout.BeginVertical(GUI.skin.GetStyle("helpbox"), options);

            public static void BeginLayout(string title, params GUILayoutOption[] options)
            {
                EditorGUILayout.BeginVertical(GUI.skin.GetStyle("helpbox"), options);

                EditorGUILayout.LabelField(title, VRCSDKPlusToolbox.Styles.Label.Centered);
            }
            public static void EndLayout() => EditorGUILayout.EndVertical();

            public static Rect GUIBox(float height)
            {
                var rect = EditorGUILayout.GetControlRect(false, height);
                return GUIBox(ref rect);
            }

            public static Rect GUIBox(ref Rect rect)
            {
                GUI.Box(rect, "", GUI.skin.GetStyle("helpbox"));

                rect.x += 4;
                rect.width -= 8;
                rect.y += 3;
                rect.height -= 6;

                return rect;
            }
        }

        public static class Placeholder
        {

            public static void GUILayout(float height) =>
                GUI(EditorGUILayout.GetControlRect(false, height));

            public static void GUI(Rect rect) => GUI(rect, EditorGUIUtility.isProSkin ? 53 : 182);

            private static void GUI(Rect rect, float color)
            {
                EditorGUI.DrawTextureTransparent(rect, GetColorTexture(color));
            }
        }

        
        public static class Preferences
        {
            public static bool CompactMode
            {
                get => EditorPrefs.GetBool(VRCSDKPlusToolbox.Strings.SettingsCompact, false);
                set => EditorPrefs.SetBool(VRCSDKPlusToolbox.Strings.SettingsCompact, value);
            }
        }

        public static Color BrightnessToColor(float brightness)
        {
            if (brightness > 1) brightness /= 255;
            return new Color(brightness, brightness, brightness, 1);
        }
        private static readonly Texture2D tempTexture = new Texture2D(1, 1) { anisoLevel = 0, filterMode = FilterMode.Point };
        internal static Texture2D GetColorTexture(float rgb, float a = 1)
            => GetColorTexture(rgb, rgb, rgb, a);

        internal static Texture2D GetColorTexture(float r, float g, float b, float a = 1)
        {
            if (r > 1) r /= 255;
            if (g > 1) g /= 255;
            if (b > 1) b /= 255;
            if (a > 1) a /= 255;

            return GetColorTexture(new Color(r, g, b, a));
        }
        /// <summary>Returns a shared 1×1 texture recoloured on every call: only valid until the next call (use for immediate drawing).</summary>
        internal static Texture2D GetColorTexture(Color color)
        {
            tempTexture.SetPixel(0, 0, color);
            tempTexture.Apply();
            return tempTexture;
        }

        /// <summary>A new 1×1 texture of its own, for styles that keep the texture (unlike <see cref="GetColorTexture(Color)"/>).</summary>
        internal static Texture2D CreateColorTexture(Color color)
        {
            var texture = new Texture2D(1, 1) { anisoLevel = 0, filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        // ReSharper disable once InconsistentNaming
        public static VRCExpressionsMenu.Control.ControlType ToControlType(this SerializedProperty property)
        {
            var value = property.enumValueIndex;
            switch (value)
            {
                case 0:
                    return VRCExpressionsMenu.Control.ControlType.Button;
                case 1:
                    return VRCExpressionsMenu.Control.ControlType.Toggle;
                case 2:
                    return VRCExpressionsMenu.Control.ControlType.SubMenu;
                case 3:
                    return VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet;
                case 4:
                    return VRCExpressionsMenu.Control.ControlType.FourAxisPuppet;
                case 5:
                    return VRCExpressionsMenu.Control.ControlType.RadialPuppet;
            }

            return VRCExpressionsMenu.Control.ControlType.Button;
        }

        public static int GetEnumValueIndex(this VRCExpressionsMenu.Control.ControlType type)
        {
            switch (type)
            {
                case VRCExpressionsMenu.Control.ControlType.Button:
                    return 0;
                case VRCExpressionsMenu.Control.ControlType.Toggle:
                    return 1;
                case VRCExpressionsMenu.Control.ControlType.SubMenu:
                    return 2;
                case VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet:
                    return 3;
                case VRCExpressionsMenu.Control.ControlType.FourAxisPuppet:
                    return 4;
                case VRCExpressionsMenu.Control.ControlType.RadialPuppet:
                    return 5;
                default:
                    return -1;
            }
        }

        public static int FindIndex(this IEnumerable array, object target)
        {
            var enumerator = array.GetEnumerator();
            var index = 0;
            while (enumerator.MoveNext())
            {
                if (enumerator.Current != null && enumerator.Current.Equals(target))
                    return index;
                index++;
            }

            return -1;
        }
        internal static bool GetPlayableLayer(this VRCAvatarDescriptor avi, VRCAvatarDescriptor.AnimLayerType type, out AnimatorController controller)
        {
            controller = (from l in (avi.baseAnimationLayers ?? Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>()).Concat(avi.specialAnimationLayers ?? Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>()) where l.type == type select l.animatorController).FirstOrDefault() as AnimatorController;
            return controller != null;
        }

        internal static bool IterateArray(this SerializedProperty property, Func<int, SerializedProperty, bool> func, params int[] skipIndex)
        {
            for (int i = property.arraySize - 1; i >= 0; i--)
            {
                if (skipIndex.Contains(i)) continue;
                if (i >= property.arraySize) continue;
                if (func(i, property.GetArrayElementAtIndex(i)))
                    return true;
            }
            return false;
        }

    }
}
