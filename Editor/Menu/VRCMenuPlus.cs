using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace DreadScripts.VRCSDKPlus
{
    internal sealed partial class VRCSDKPlus
    {
        internal sealed partial class VRCMenuPlus : Editor, IHasCustomMenu
        {
            private static bool editorActive = true;
            private static VRCAvatarDescriptor _avatar;
            private VRCAvatarDescriptor[] _validAvatars;
            private ReorderableList _controlsList;

            private static readonly LinkedList<VRCExpressionsMenu> _menuHistory = new LinkedList<VRCExpressionsMenu>();
            private static LinkedListNode<VRCExpressionsMenu> _currentNode;
            private static VRCExpressionsMenu _lastMenu;

            private static VRCExpressionsMenu moveSourceMenu;
            private static VRCExpressionsMenu.Control moveTargetControl;
            private static bool isMoving;

            #region Initialization
            private void ReInitializeAll()
            {
                CheckAvatar();
                CheckMenu();
                InitializeList();
                FillParameters(false);
            }

            private void CheckAvatar()
            {
                // Include inactive avatars, so deactivating the active avatar doesn't make the inspector re-initialize every frame.
                _validAvatars = FindAvatars();
                if (_validAvatars.Length == 0) _avatar = null;
                else if (!_avatar) _avatar = _validAvatars[0];
            }

            private void CheckMenu()
            {
                var currentMenu = target as VRCExpressionsMenu;
                if (!currentMenu || currentMenu == _lastMenu) return;

                if (_currentNode != null && _menuHistory.Last != _currentNode)
                {
                    var node = _currentNode.Next;
                    while (node != null)
                    {
                        var nextNode = node.Next;
                        _menuHistory.Remove(node);
                        node = nextNode;
                    }
                }

                _lastMenu = currentMenu;
                _currentNode = _menuHistory.AddLast(currentMenu);
            }

            private void InitializeList()
            {
                var l = serializedObject.FindProperty("controls");
                _controlsList = new ReorderableList(serializedObject, l, true, true, true, false);
                _controlsList.onCanAddCallback += reorderableList => reorderableList.count < 8;
                _controlsList.onAddCallback = _ =>
                {
                    var controlsProp = _controlsList.serializedProperty;
                    var index = controlsProp.arraySize++;
                    _controlsList.index = index;

                    var c = controlsProp.GetArrayElementAtIndex(index);
                    c.FindPropertyRelative("name").stringValue = "New Control";
                    c.FindPropertyRelative("icon").objectReferenceValue = null;
                    c.FindPropertyRelative("parameter").FindPropertyRelative("name").stringValue = "";
                    c.FindPropertyRelative("type").enumValueIndex = 1;
                    c.FindPropertyRelative("subMenu").objectReferenceValue = null;
                    c.FindPropertyRelative("labels").ClearArray();
                    c.FindPropertyRelative("subParameters").ClearArray();
                    c.FindPropertyRelative("value").floatValue = 1;
                };
                _controlsList.drawHeaderCallback = rect =>
                {
                    if (isMoving && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
                    {
                        isMoving = false;
                        Repaint();
                    }
                    EditorGUI.LabelField(rect, $"Controls ({_controlsList.count} / 8)");

                    // Draw copy, paste, duplicate, and move buttons
                    #region Rects
                    var copyRect = new Rect(
                        rect.x + rect.width - rect.height - ((rect.height + VRCSDKPlusToolbox.Styles.Padding) * 3),
                        rect.y,
                        rect.height,
                        rect.height);

                    var pasteRect = new Rect(
                        copyRect.x + copyRect.width + VRCSDKPlusToolbox.Styles.Padding,
                        copyRect.y,
                        copyRect.height,
                        copyRect.height);

                    var duplicateRect = new Rect(
                        pasteRect.x + pasteRect.width + VRCSDKPlusToolbox.Styles.Padding,
                        pasteRect.y,
                        pasteRect.height,
                        pasteRect.height);

                    var moveRect = new Rect(
                        duplicateRect.x + duplicateRect.width + VRCSDKPlusToolbox.Styles.Padding,
                        duplicateRect.y,
                        duplicateRect.height,
                        duplicateRect.height);
                    
                    #endregion

                    bool isFull = _controlsList.count >= 8;
                    bool isEmpty = _controlsList.count == 0;
                    bool hasIndex = _controlsList.TryGetActiveIndex(out int index);
                    bool hasFocus = _controlsList.HasKeyboardControl();
                    if (!hasIndex) index = _controlsList.count;
                    using (new EditorGUI.DisabledScope(isEmpty || !hasFocus || !hasIndex))
                    {
                        #region Copy

                        w_MakeRectLinkCursor(copyRect);
                        if (GUI.Button(copyRect, VRCSDKPlusToolbox.GUIContent.Copy, GUI.skin.label))
                            CopyControl(index);
                                

                        #endregion

                        #region Duplicate

                        using (new EditorGUI.DisabledScope(isFull))
                        {
                            w_MakeRectLinkCursor(duplicateRect);
                            if (GUI.Button(duplicateRect, isFull ? new GUIContent(VRCSDKPlusToolbox.GUIContent.Duplicate) { tooltip = VRCSDKPlusToolbox.GUIContent.MenuFullTooltip } : VRCSDKPlusToolbox.GUIContent.Duplicate, GUI.skin.label))
                                DuplicateControl(index);
                        }

                        #endregion
                    }

                    #region Paste
                    using (new EditorGUI.DisabledScope(!CanPasteControl()))
                    {
                        w_MakeRectLinkCursor(pasteRect);
                        if (GUI.Button(pasteRect, VRCSDKPlusToolbox.GUIContent.Paste, GUI.skin.label))
                        {
                            var menu = new GenericMenu();
                            menu.AddItem(new GUIContent("Paste values"), false, isEmpty || !hasFocus ? (GenericMenu.MenuFunction)null : () => PasteControl(index, false));
                            menu.AddItem(
                                new GUIContent("Insert as new"),
                                false,
                                isFull ? (GenericMenu.MenuFunction)null : () => PasteControl(index, true)
                            );
                            menu.ShowAsContext();
                        }
                    }
                    #endregion


                    #region Move
                    using (new EditorGUI.DisabledScope((isMoving && isFull) || (!isMoving && (!hasFocus || isEmpty))))
                    {
                        w_MakeRectLinkCursor(moveRect);
                        if (GUI.Button(moveRect, isMoving ? isFull ? new GUIContent(VRCSDKPlusToolbox.GUIContent.Place) {tooltip = VRCSDKPlusToolbox.GUIContent.MenuFullTooltip} : VRCSDKPlusToolbox.GUIContent.Place : VRCSDKPlusToolbox.GUIContent.Move, GUI.skin.label))
                        {
                            if (!isMoving) MoveControl(index);
                            else PlaceControl(index);
                        }
                    }

                    #endregion


                };
                _controlsList.drawElementCallback = (rect2, index, _, focused) =>
                {
                    if (!(index < l.arraySize && index >= 0)) return;
                    var controlProp = l.GetArrayElementAtIndex(index);
                    var controlType = controlProp.FindPropertyRelative("type").ToControlType();
                    Rect removeRect = new Rect(rect2.width + 3, rect2.y + 1, 32, 18);
                    rect2.width -= 48;
                    // Draw control type
                    EditorGUI.LabelField(rect2, controlType.ToString(), focused
                            ? VRCSDKPlusToolbox.Styles.Label.TypeFocused
                            : VRCSDKPlusToolbox.Styles.Label.Type);

                    // Draw control name
                    var nameGuiContent = new GUIContent(controlProp.FindPropertyRelative("name").stringValue);
                    bool emptyName = string.IsNullOrEmpty(nameGuiContent.text);
                    if (emptyName) nameGuiContent.text = "[Unnamed]";

                    var nameRect = new Rect(rect2.x, rect2.y, VRCSDKPlusToolbox.Styles.Label.RichText.CalcSize(nameGuiContent).x, rect2.height);

                    EditorGUI.LabelField(nameRect,
                        new GUIContent(nameGuiContent),
                        emptyName ? VRCSDKPlusToolbox.Styles.Label.PlaceHolder : VRCSDKPlusToolbox.Styles.Label.RichText);

                    w_MakeRectLinkCursor(removeRect);
                    if (GUI.Button(removeRect, VRCSDKPlusToolbox.GUIContent.Remove, VRCSDKPlusToolbox.Styles.Label.RemoveIcon))
                        DeleteControl(index);

                    var e = Event.current;
                    
                    if (controlType == VRCExpressionsMenu.Control.ControlType.SubMenu)
                    {
                        if (e.clickCount == 2 && e.type == EventType.MouseDown && rect2.Contains(e.mousePosition))
                        {
                            var sm = controlProp.FindPropertyRelative("subMenu").objectReferenceValue;
                            if (sm) Selection.activeObject = sm;
                            e.Use();
                        }
                    }

                    if (e.type == EventType.ContextClick && rect2.Contains(e.mousePosition))
                    {
                        e.Use();
                        var menu = new GenericMenu();
                        menu.AddItem(new GUIContent("Cut"), false, () => MoveControl(index));
                        menu.AddItem(new GUIContent("Copy"), false, () => CopyControl(index));
                        bool menuFull = l.arraySize >= MaxControls;
                        if (!CanPasteControl()) menu.AddDisabledItem(new GUIContent("Paste"));
                        else
                        {
                            menu.AddItem(new GUIContent("Paste/Values"), false, () =>  PasteControl(index, false));
                            if (menuFull) menu.AddDisabledItem(new GUIContent("Paste/As New"));
                            else menu.AddItem(new GUIContent("Paste/As New"), false, () =>  PasteControl(index, true));
                        }
                        menu.AddSeparator(string.Empty);
                        if (menuFull) menu.AddDisabledItem(new GUIContent("Duplicate"));
                        else menu.AddItem(new GUIContent("Duplicate"), false, () => DuplicateControl(index));
                        menu.AddItem(new GUIContent("Delete"), false, () => DeleteControl(index));
                        menu.ShowAsContext();
                    }
                    
                };
            }

            private VRCExpressionParameters.Parameter FetchParameter(string name)
            {
                var par = ActiveParameters;
                if (!par) return null;
                return par.parameters?.FirstOrDefault(p => p.name == name);
            }
            #endregion

            protected override void OnHeaderGUI()
            {
                OverrideHelpIcon();
                base.OnHeaderGUI();
            }

            public override void OnInspectorGUI()
            {
                serializedObject.Update();
                HandleControlEvents();
                DrawHistory();
                DrawHead();
                DrawBody();
                DrawFooter();
                serializedObject.ApplyModifiedProperties();
            }

            private void OnEnable() => ReInitializeAll();

            private void DrawHistory()
            {
                using (new GUILayout.HorizontalScope("helpbox"))
                {
                    void CheckHistory()
                    {
                        for (LinkedListNode<VRCExpressionsMenu> node = _menuHistory.First; node != null;)
                        {
                            LinkedListNode<VRCExpressionsMenu> next = node.Next;
                            if (node.Value == null) _menuHistory.Remove(node);
                            node = next;
                        }
                    }

                    void SetCurrentNode(LinkedListNode<VRCExpressionsMenu> node)
                    {
                        if (node.Value == null) return;
                        _currentNode = node;
                        Selection.activeObject = _lastMenu = _currentNode.Value;
                    }

                    using (new EditorGUI.DisabledScope(_currentNode.Previous == null))
                    {
                        using (new EditorGUI.DisabledScope(_currentNode.Previous == null))
                        {
                            if (ClickableButton("<<", GUILayout.ExpandWidth(false)))
                            {
                                CheckHistory();
                                SetCurrentNode(_menuHistory.First);
                            }

                            if (ClickableButton("<", GUILayout.ExpandWidth(false)))
                            {
                                CheckHistory();
                                SetCurrentNode(_currentNode.Previous);
                            }
                        }
                    }

                    if (ClickableButton(_lastMenu.name, VRCSDKPlusToolbox.Styles.Label.Centered, GUILayout.ExpandWidth(true)))
                        EditorGUIUtility.PingObject(_lastMenu);

                    using (new EditorGUI.DisabledScope(_currentNode.Next == null))
                    {
                        if (ClickableButton(">", GUILayout.ExpandWidth(false)))
                        {
                            CheckHistory();
                            SetCurrentNode(_currentNode.Next);
                        }

                        if (ClickableButton(">>", GUILayout.ExpandWidth(false)))
                        {
                            CheckHistory();
                            SetCurrentNode(_menuHistory.Last);
                        }
                    }

                }
            }
            private void DrawHead()
            {
                #region Avatar Selector

                // Generate name string array
                var targetsAsString = _validAvatars.Select(t => t.gameObject.name).ToArray();

                // Draw selection
                using (new EditorGUI.DisabledScope(_validAvatars.Length <= 1))
                {
                    using (new VRCSDKPlusToolbox.Container.Horizontal())
                    {
                        var content = new GUIContent("Active Avatar", "The auto-fill and warnings will be based on this avatar's expression parameters");
                        if (_validAvatars.Length >= 1)
                        {
                            using (var change = new EditorGUI.ChangeCheckScope())
                            {
                                var targetIndex = EditorGUILayout.Popup(
                                    content,
                                    _validAvatars.FindIndex(_avatar),
                                    targetsAsString);

                                if (targetIndex == -1)
                                    ReInitializeAll();
                                else if (change.changed)
                                {
                                    _avatar = _validAvatars[targetIndex];
                                    ReInitializeAll();
                                    FillParameters(true);
                                }
                            }
                        }
                        else EditorGUILayout.LabelField(content, new GUIContent("No Avatar Descriptors found"), VRCSDKPlusToolbox.Styles.Label.LabelDropdown);

                        if (!ActiveParameters)
                            GUILayout.Label(new GUIContent(VRCSDKPlusToolbox.GUIContent.Error) { tooltip = VRCSDKPlusToolbox.GUIContent.MissingParametersTooltip }, GUILayout.Width(18));
                    }
                }

                #endregion
            }

            // The Active Avatar's parameters, or the ones assigned to the menu itself when there are none.
            private VRCExpressionParameters ActiveParameters =>
                _avatar && _avatar.expressionParameters ? _avatar.expressionParameters : CurrentMenu.Parameters;

            /// <summary>
            /// Fills in the menu's own parameters (shown by the official editor) from the Active Avatar.
            /// Parameters that are already set are only replaced when that avatar uses this menu, or was just picked,
            /// so viewing another avatar's menu doesn't change it.
            /// </summary>
            private void FillParameters(bool avatarPicked)
            {
                VRCExpressionsMenu menu = CurrentMenu;
                if (!menu || !_avatar || !_avatar.expressionParameters || menu.Parameters == _avatar.expressionParameters) return;

                bool usesMenu = _avatar.expressionsMenu && CollectMenus(_avatar.expressionsMenu).Contains(menu);
                if (avatarPicked || !menu.Parameters || usesMenu)
                    menu.Parameters = _avatar.expressionParameters;
            }

            void DrawBody()
            {

                if (_controlsList == null)
                    InitializeList();

                // Keep the selected index valid (e.g. after undoing an add it can point past the end).
                if (_controlsList.count != 0)
                {
                    if (_controlsList.index < 0) _controlsList.index = 0;
                    else if (_controlsList.index >= _controlsList.count) _controlsList.index = _controlsList.count - 1;
                }
                
                _controlsList.DoLayoutList();
                if (_controlsList.count == 0)
                    _controlsList.index = -1;

                var control = _controlsList.index < 0 || _controlsList.index >= _controlsList.count ? null : _controlsList.serializedProperty.GetArrayElementAtIndex(_controlsList.index);
                var expressionParameters = ActiveParameters;

                if (VRCSDKPlusToolbox.Preferences.CompactMode)
                    ControlRenderer.DrawControlCompact(control, expressionParameters);
                else
                    ControlRenderer.DrawControl(control, expressionParameters);

            }

            void DrawFooter()
            {
                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    GUILayout.Label("Editor by", VRCSDKPlusToolbox.Styles.Label.Watermark);
                    Link("@fox_score","https://github.com/foxscore");
                    GUILayout.Label("&", VRCSDKPlusToolbox.Styles.Label.Watermark);
                    GUILayout.Label("@Dreadrith", VRCSDKPlusToolbox.Styles.Label.Watermark);
                }
                DrawMaintainedBy();
            }

            private void HandleControlEvents()
            {
                if (!_controlsList.HasKeyboardControl()) return;
                if (!_controlsList.TryGetActiveIndex(out int index)) return;
                bool fullMenu = _controlsList.count >= 8;

                bool WarnIfFull()
                {
                    if (fullMenu)
                    {
                        Debug.LogWarning(VRCSDKPlusToolbox.GUIContent.MenuFullTooltip);
                        return true;
                    }

                    return false;
                }
                
                if (VRCSDKPlusToolbox.HasReceivedAnyDelete())
                    DeleteControl(index);
                
                if (VRCSDKPlusToolbox.HasReceivedCommand(VRCSDKPlusToolbox.EventCommands.Duplicate))
                    if (!WarnIfFull()) DuplicateControl(index);
                
                if (VRCSDKPlusToolbox.HasReceivedCommand(VRCSDKPlusToolbox.EventCommands.Copy))
                    CopyControl(index);
                
                if (VRCSDKPlusToolbox.HasReceivedCommand(VRCSDKPlusToolbox.EventCommands.Cut))
                    MoveControl(index);
                
                if (VRCSDKPlusToolbox.HasReceivedCommand(VRCSDKPlusToolbox.EventCommands.Paste))
                    if (isMoving && !WarnIfFull()) PlaceControl(index);
                    else if (CanPasteControl() && !WarnIfFull()) PasteControl(index, true);
            }
            
            #region Control Methods
            private const int MaxControls = 8;

            // The menu this inspector shows. (The static _lastMenu is the history's current menu, which differs
            // from target when more than one menu inspector is open, e.g. a locked one.)
            private VRCExpressionsMenu CurrentMenu => (VRCExpressionsMenu)target;

            private static bool IsShiftHeld() => Event.current != null && Event.current.shift; // null inside some menu callbacks

            private void CopyControl(int index)
            {
                if (index < 0 || index >= CurrentMenu.controls.Count) return;
                // EditorJsonUtility stores asset references (icons, submenus) by GUID, so they survive editor restarts.
                EditorGUIUtility.systemCopyBuffer =
                    VRCSDKPlusToolbox.Strings.ClipboardPrefixControl +
                    EditorJsonUtility.ToJson(CurrentMenu.controls[index]);
            }

            private static bool CanPasteControl() => (EditorGUIUtility.systemCopyBuffer ?? string.Empty).StartsWith(VRCSDKPlusToolbox.Strings.ClipboardPrefixControl, StringComparison.Ordinal);

            private static bool TryReadClipboardControl(out VRCExpressionsMenu.Control control)
            {
                control = null;
                if (!CanPasteControl()) return false;
                string json = EditorGUIUtility.systemCopyBuffer.Substring(VRCSDKPlusToolbox.Strings.ClipboardPrefixControl.Length);
                try
                {
                    control = new VRCExpressionsMenu.Control();
                    EditorJsonUtility.FromJsonOverwrite(json, control);
                    return true;
                }
                catch (ArgumentException)
                {
                    Debug.LogWarning("[VRCSDK+] The copied control couldn't be read, so nothing was pasted.");
                    control = null;
                    return false;
                }
            }

            private void PasteControl(int index, bool asNew)
            {
                if (!TryReadClipboardControl(out VRCExpressionsMenu.Control control)) return;
                VRCExpressionsMenu menu = CurrentMenu;
                if (!asNew)
                {
                    if (index < 0 || index >= menu.controls.Count) return;
                    Undo.RecordObject(menu, "Paste control values");
                    menu.controls[index] = control;
                }
                else
                {
                    if (menu.controls.Count >= MaxControls)
                    {
                        Debug.LogWarning(VRCSDKPlusToolbox.GUIContent.MenuFullTooltip);
                        return;
                    }

                    Undo.RecordObject(menu, "Insert control as new");
                    int insertIndex = menu.controls.Count == 0 ? 0 : Mathf.Clamp(index + 1, 0, menu.controls.Count);
                    menu.controls.Insert(insertIndex, control);
                    _controlsList.index = insertIndex;
                }
                EditorUtility.SetDirty(menu);
                // The asset was changed directly. Resync so this frame's serialized data doesn't overwrite it.
                serializedObject.Update();
            }

            private void DuplicateControl(int index)
            {
                var controlsProp = _controlsList.serializedProperty;
                if (index < 0 || index >= controlsProp.arraySize) return;
                if (controlsProp.arraySize >= MaxControls)
                {
                    Debug.LogWarning(VRCSDKPlusToolbox.GUIContent.MenuFullTooltip);
                    return;
                }

                bool keepParameter = IsShiftHeld();
                controlsProp.InsertArrayElementAtIndex(index);
                _controlsList.index = index + 1;

                var newElement = controlsProp.GetArrayElementAtIndex(index+1);
                var lastName = newElement.FindPropertyRelative("name").stringValue;
                newElement.FindPropertyRelative("name").stringValue = VRCSDKPlusToolbox.GenerateUniqueString(lastName, newName => newName != lastName, false);

                if (!keepParameter) MakeDuplicateParameterUnique(newElement);
                // Also called from context-menu callbacks, which run outside OnInspectorGUI.
                serializedObject.ApplyModifiedProperties();
            }

            private void MakeDuplicateParameterUnique(SerializedProperty newElement)
            {
                var menuParameter = newElement.FindPropertyRelative("parameter");
                if (menuParameter == null) return;
                var parName = menuParameter.FindPropertyRelative("name").stringValue;
                if (string.IsNullOrEmpty(parName)) return;
                var matchedParameter = FetchParameter(parName);
                if (matchedParameter == null) return;
                var controlType = newElement.FindPropertyRelative("type").ToControlType();
                if (controlType != VRCExpressionsMenu.Control.ControlType.Button && controlType != VRCExpressionsMenu.Control.ControlType.Toggle) return;

                if (matchedParameter.valueType == VRCExpressionParameters.ValueType.Bool)
                {
                    menuParameter.FindPropertyRelative("name").stringValue = VRCSDKPlusToolbox.GenerateUniqueString(parName, s => s != parName, false);
                }
                else
                {
                    var controlValueProp = newElement.FindPropertyRelative("value");
                    if (Mathf.RoundToInt(controlValueProp.floatValue) == controlValueProp.floatValue)
                        controlValueProp.floatValue++;
                }
            }

            private void DeleteControl(int index)
            {
                var controlsProp = _controlsList.serializedProperty;
                if (index < 0 || index >= controlsProp.arraySize) return;
                if (_controlsList.index == index) _controlsList.index--;
                controlsProp.DeleteArrayElementAtIndex(index);
                // Also called from context-menu callbacks, which run outside OnInspectorGUI.
                serializedObject.ApplyModifiedProperties();
            }

            private void MoveControl(int index)
            {
                if (index < 0 || index >= CurrentMenu.controls.Count) return;
                isMoving = true;
                moveSourceMenu = CurrentMenu;
                moveTargetControl = CurrentMenu.controls[index];
            }

            private void PlaceControl(int index)
            {
                isMoving = false;
                if (!moveSourceMenu || moveTargetControl == null) return;

                VRCExpressionsMenu menu = CurrentMenu;
                bool sameMenu = moveSourceMenu == menu;
                int sourceIndex = moveSourceMenu.controls.IndexOf(moveTargetControl);
                if (sourceIndex < 0)
                {
                    // E.g. the source menu was changed by undo after the cut. Placing now would duplicate the control.
                    Debug.LogWarning("[VRCSDK+] The cut control is no longer in its menu, so it wasn't moved.");
                    return;
                }
                if (!sameMenu && menu.controls.Count >= MaxControls)
                {
                    Debug.LogWarning(VRCSDKPlusToolbox.GUIContent.MenuFullTooltip);
                    return;
                }

                Undo.RecordObject(menu, "Move control");
                if (!sameMenu) Undo.RecordObject(moveSourceMenu, "Move control");

                // Remove first, then insert, so moving up within the same menu works too.
                int insertIndex = menu.controls.Count == 0 ? 0 : index + 1;
                moveSourceMenu.controls.RemoveAt(sourceIndex);
                if (sameMenu && sourceIndex < insertIndex) insertIndex--;
                insertIndex = Mathf.Clamp(insertIndex, 0, menu.controls.Count);
                menu.controls.Insert(insertIndex, moveTargetControl);
                _controlsList.index = insertIndex;

                EditorUtility.SetDirty(moveSourceMenu);
                EditorUtility.SetDirty(menu);
                serializedObject.Update();

                if (IsShiftHeld()) Selection.activeObject = moveSourceMenu;
                moveTargetControl = null;
            }

            #endregion

            public void AddItemsToMenu(GenericMenu menu) => menu.AddItem(new GUIContent("Compact Mode"), VRCSDKPlusToolbox.Preferences.CompactMode, ToggleCompactMode);
            private static void ToggleCompactMode() => VRCSDKPlusToolbox.Preferences.CompactMode = !VRCSDKPlusToolbox.Preferences.CompactMode;

            [MenuItem("CONTEXT/VRCExpressionsMenu/[SDK+] Toggle Editor", false, 899)]
            private static void ToggleEditor()
            {
                editorActive = !editorActive;
                var targetType = ExtendedGetType("VRCExpressionsMenu");
                if (targetType == null)
                {
                    Debug.LogError("[VRCSDK+] VRCExpressionsMenu was not found! Could not apply custom editor.");
                    return;
                }
                if (editorActive) OverrideEditor(targetType, typeof(VRCMenuPlus));
                else
                {
                    var menuEditor = ExtendedGetType("VRCExpressionsMenuEditor");
                    if (menuEditor == null)
                    {
                        Debug.LogWarning("[VRCSDK+] VRCExpressionsMenuEditor was not found! Could not apply custom editor.");
                        return;
                    }
                    OverrideEditor(targetType, menuEditor);
                }
            }

        }
    }
}
