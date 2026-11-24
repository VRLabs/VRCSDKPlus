using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
using Object = UnityEngine.Object;

namespace DreadScripts.VRCSDKPlus
{
    internal sealed partial class VRCSDKPlus
    {
        internal sealed partial class VRCMenuPlus : Editor, IHasCustomMenu
        {
            #region Make Unique
            [MenuItem("CONTEXT/VRCExpressionsMenu/[SDK+] Make Unique Copy", true)]
            private static bool MakeUniqueValidate(MenuCommand command) => EditorUtility.IsPersistent(command.context);

            [MenuItem("CONTEXT/VRCExpressionsMenu/[SDK+] Make Unique Copy", false, 900)]
            private static void MakeUniqueCommand(MenuCommand command)
            {
                VRCExpressionsMenu menu = (VRCExpressionsMenu)command.context;
                int submenuCount = CollectMenus(menu).Count - 1;

                string message = $"This creates a unique copy of \"{menu.name}\" and its {submenuCount} submenu(s), in a new folder:\n\n" +
                                 $"{GetUniqueFolderParent(menu)}/{GetUniqueFolderName(menu)}\n\n" +
                                 "The copies link to each other instead of the original menus";

                if (!EditorUtility.DisplayDialog("Make Unique Copy", message, "Make Unique Copy", "Cancel")) return;

                try
                {
                    VRCExpressionsMenu copy = MakeUnique(menu);
                    Selection.activeObject = copy;
                    EditorGUIUtility.PingObject(copy);
                    GreenLog($"Made a unique copy of {menu.name} with {submenuCount} submenu(s) at {AssetDatabase.GetAssetPath(copy)}");
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    EditorUtility.DisplayDialog("Make Unique Copy", $"The menu couldn't be copied, so nothing was created.\n\n{e.Message}", "OK");
                }
            }

            /// <summary>Copies the menu and all of its submenus into a new folder, and links the copies to each other. Returns the copied root.</summary>
            private static VRCExpressionsMenu MakeUnique(VRCExpressionsMenu root)
            {
                List<VRCExpressionsMenu> menus = CollectMenus(root);
                string folder = AssetDatabase.GUIDToAssetPath(AssetDatabase.CreateFolder(GetUniqueFolderParent(root), GetUniqueFolderName(root)));
                if (string.IsNullOrEmpty(folder)) throw new IOException("The folder for the copies couldn't be created.");

                try
                {
                    // Every copy has to be an asset before the links are set, otherwise they'd be saved as empty.
                    Dictionary<VRCExpressionsMenu, VRCExpressionsMenu> copies = new Dictionary<VRCExpressionsMenu, VRCExpressionsMenu>();
                    foreach (VRCExpressionsMenu menu in menus)
                    {
                        // Instantiate instead of copying the file, so menus stored inside another asset work too.
                        VRCExpressionsMenu copy = Object.Instantiate(menu);
                        AssetDatabase.CreateAsset(copy, AssetDatabase.GenerateUniqueAssetPath($"{folder}/{VRCSDKPlusToolbox.LegalizeName(menu.name)}.asset"));
                        if (!EditorUtility.IsPersistent(copy)) throw new IOException($"The copy of {menu.name} couldn't be created.");
                        copies.Add(menu, copy);
                    }

                    foreach (VRCExpressionsMenu copy in copies.Values)
                    {
                        if (copy.controls != null)
                        {
                            foreach (VRCExpressionsMenu.Control control in copy.controls)
                            {
                                if (IsSubMenuControl(control) && copies.TryGetValue(control.subMenu, out VRCExpressionsMenu submenuCopy))
                                    control.subMenu = submenuCopy;
                            }
                        }

                        EditorUtility.SetDirty(copy);
                        // Only the copies, so other unsaved assets in the project are left alone.
                        AssetDatabase.SaveAssetIfDirty(copy);
                    }

                    return copies[root];
                }
                catch
                {
                    // The folder was just created here, so it only holds the unfinished copies.
                    AssetDatabase.DeleteAsset(folder);
                    throw;
                }
            }

            /// <summary>The menu itself followed by every menu below it, each one once (menus can link back to each other).</summary>
            private static List<VRCExpressionsMenu> CollectMenus(VRCExpressionsMenu root)
            {
                List<VRCExpressionsMenu> menus = new List<VRCExpressionsMenu> { root };
                HashSet<VRCExpressionsMenu> found = new HashSet<VRCExpressionsMenu> { root };
                for (int i = 0; i < menus.Count; i++)
                {
                    if (menus[i].controls == null) continue;
                    foreach (VRCExpressionsMenu.Control control in menus[i].controls)
                    {
                        if (IsSubMenuControl(control) && found.Add(control.subMenu))
                            menus.Add(control.subMenu);
                    }
                }
                return menus;
            }

            private static bool IsSubMenuControl(VRCExpressionsMenu.Control control) =>
                control != null && control.type == VRCExpressionsMenu.Control.ControlType.SubMenu && control.subMenu;

            private static string GetUniqueFolderName(VRCExpressionsMenu menu) => $"{VRCSDKPlusToolbox.LegalizeName(menu.name)}_Unique";

            // Next to the original, or in Assets when the original is inside a package.
            private static string GetUniqueFolderParent(VRCExpressionsMenu menu)
            {
                string parent = Path.GetDirectoryName(AssetDatabase.GetAssetPath(menu))?.Replace('\\', '/');
                return parent == "Assets" || (parent != null && parent.StartsWith("Assets/", StringComparison.Ordinal)) ? parent : "Assets";
            }
            #endregion
        }
    }
}
