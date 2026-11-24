using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;
using AnimatorControllerParameter = UnityEngine.AnimatorControllerParameter;

namespace DreadScripts.VRCSDKPlus
{
    internal sealed partial class VRCSDKPlus
    {
        internal sealed partial class VRCParamsPlus : Editor
        {
            private static int _MAX_MEMORY_COST;
            private static int MAX_MEMORY_COST
            {
                get
                {
                    if (_MAX_MEMORY_COST == 0)
                    {
                        try
                        { _MAX_MEMORY_COST = (int) typeof(VRCExpressionParameters).GetField("MAX_PARAMETER_COST", BindingFlags.Static | BindingFlags.Public).GetValue(null); }
                        catch 
                        {
                            Debug.LogError("Failed to dynamically get MAX_PARAMETER_COST. Falling back to 256");
                            _MAX_MEMORY_COST = 256;
                        }
                    }

                    return _MAX_MEMORY_COST;
                }
            }

            private static readonly bool hasSyncingOption = typeof(VRCExpressionParameters.Parameter).GetField("networkSynced") != null;
            private static readonly bool hasAdvancedOptions = typeof(VRCExpressionParameters.Parameter).GetField("bitWidth") != null;
            private static bool editorActive = true;
            private static bool canCleanup;
            private int currentCost;
            private string searchValue;

            private SerializedProperty parameterList;
            private ReorderableList parametersOrderList;

            private ParameterStatus[] _parameterStatus;

            private static VRCExpressionParameters mergeParams;
            
            protected override void OnHeaderGUI()
            {
                OverrideHelpIcon();
                base.OnHeaderGUI();
            }

            public override void OnInspectorGUI()
            {
                EditorGUI.BeginChangeCheck();
                using (new GUILayout.HorizontalScope("helpbox"))
                    DrawAdvancedAvatarFull(ref avatar, validAvatars, RefreshAvatarInfo, false, false, false, "Active Avatar");

                canCleanup = _parameterStatus != null && _parameterStatus.Any(s => s.hasWarning);
                serializedObject.Update();
                HandleParameterEvents();
                // The list resets the selection to the clicked row on mouse-down, so remember it first
                // to move the whole group when one of its rows is dragged.
                EventType eventType = Event.current.type;
                bool selectModifier = EditorGUI.actionKey || Event.current.shift;
                if (eventType == EventType.MouseDown)
                    selectionBeforeClick = parametersOrderList.selectedIndices.ToList();
                foldoutClicked = false;
                advancedLineClicked = false;
                parametersOrderList.DoLayoutList();
                if (eventType == EventType.MouseDown && (foldoutClicked || advancedLineClicked))
                    RestoreSelection(focusList: foldoutClicked);
                HandleGroupDragState(eventType, selectModifier);
                serializedObject.ApplyModifiedProperties();
                
                if (canCleanup)
                {
                    using (new GUILayout.HorizontalScope("helpbox"))
                    {
                        GUILayout.Label("Cleanup Invalid, Blank, and Duplicate Parameters");
                        if (ClickableButton("Cleanup"))
                            CleanupParameters();
                    }
                }

                EditorGUI.BeginChangeCheck();
                using (new GUILayout.HorizontalScope("helpbox"))
                    mergeParams = (VRCExpressionParameters)EditorGUILayout.ObjectField("Merge Parameters", null, typeof(VRCExpressionParameters), true);
                if (EditorGUI.EndChangeCheck())
                {
                    if (mergeParams)
                    {
                        if (mergeParams.parameters != null)
                        {
                            VRCExpressionParameters myParams = (VRCExpressionParameters) target;
                            Undo.RecordObject(myParams, "Merge Parameters");
                            myParams.parameters = myParams.parameters.Concat(mergeParams.parameters.Select(p => JsonUtility.FromJson<VRCExpressionParameters.Parameter>(JsonUtility.ToJson(p)))).ToArray();
                            EditorUtility.SetDirty(myParams);
                            // The asset was changed directly. Resync so later edits this frame don't overwrite it.
                            serializedObject.Update();
                            UpdateEmptyFlag();
                            serializedObject.ApplyModifiedProperties();
                        }
                        mergeParams = null;
                    }
                }

                using (new GUILayout.HorizontalScope("helpbox"))
                using (new EditorGUI.DisabledScope(parameterList.arraySize < 2))
                {
                    if (GUILayout.Button(new GUIContent("Sort A-Z", "Sort all parameters in alphabetical order.")))
                        SortParameters(GetSortedOrder(GetParameterNames(), false));
                    if (GUILayout.Button(new GUIContent("Sort Z-A", "Sort all parameters in reverse alphabetical order.")))
                        SortParameters(GetSortedOrder(GetParameterNames(), true));
                    if (GUILayout.Button(new GUIContent("Group by Path", "Groups parameters together, that share the same prefix (for ex: Clothes/Top/1, Clothes/Top/2)")))
                        SortParameters(GetGroupedByPathOrder(GetParameterNames()));
                }

                CalculateTotalCost();
                try
                {
                    using (new EditorGUILayout.HorizontalScope("helpbox"))
                        DrawMemoryBar(GUILayoutUtility.GetRect(0, 20, GUILayout.ExpandWidth(true)));
                } catch{}
                using (new GUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    GUILayout.Label("Made By @Dreadrith ♡", VRCSDKPlusToolbox.Styles.Label.Watermark);
                }
                DrawMaintainedBy();

                if (EditorGUI.EndChangeCheck()) RefreshAllParameterStatus();
            }

            private void OnEnable()
            {
                InitConstants();
                RefreshAvatar(a => a.expressionParameters == target);

                parameterList = serializedObject.FindProperty("parameters");
                RefreshParametersOrderList();
                RefreshAllParameterStatus();
                Undo.undoRedoPerformed -= OnUndoRedo;
                Undo.undoRedoPerformed += OnUndoRedo;
            }

            private void OnDisable()
            {
                Undo.undoRedoPerformed -= OnUndoRedo;
            }

            // Warnings, type labels and Add buttons are cached per row. Rebuild them after undo/redo.
            private void OnUndoRedo()
            {
                if (this == null || target == null) return;
                serializedObject.Update();
                RefreshAllParameterStatus();
                Repaint();
            }

            // Remembers that the list was emptied on purpose, so the official editor doesn't add its default parameters again.
            // Older SDKs don't have it.
            private void UpdateEmptyFlag()
            {
                SerializedProperty isEmpty = serializedObject.FindProperty("isEmpty");
                if (isEmpty != null) isEmpty.boolValue = parameterList.arraySize == 0;
            }

            private void CleanupParameters()
            {
                RefreshValidParameters();

                // Only treat "not found in the avatar's controllers" as invalid when the active avatar actually uses
                // this asset. Otherwise another avatar's valid parameters would be deleted.
                bool checkAgainstAvatar = avatar && avatar.expressionParameters == target;
                HashSet<string> seen = new HashSet<string>();
                List<int> toDelete = new List<int>();
                for (int i = 0; i < parameterList.arraySize; i++)
                {
                    string name = parameterList.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue;
                    if (string.IsNullOrEmpty(name))
                    {
                        GreenLog($"Deleted blank parameter at index {i}");
                        toDelete.Add(i);
                    }
                    else if (checkAgainstAvatar && validParameters.All(p => p.name != name))
                    {
                        GreenLog($"Deleted invalid parameter {name}");
                        toDelete.Add(i);
                    }
                    else if (!seen.Add(name))
                    {
                        GreenLog($"Deleted duplicate parameter {name}");
                        toDelete.Add(i);
                    }
                }

                if (avatar && !checkAgainstAvatar)
                    GreenLog($"Kept parameters missing from {avatar.name}'s controllers: that avatar doesn't use this asset.");
                if (toDelete.Count > 0) DeleteParameters(toDelete);
                GreenLog("Finished Cleanup!");
            }

          

            private void DrawHeader(Rect rect)
            {
                #region Rects
                rect.y += 1;
                rect.height = 18;


                Rect UseNext(float width, bool fixedWidth = false, float position = -1, bool fixedPosition = false)
                {
                    Rect currentRect = rect;
                    currentRect.width = fixedWidth ? width : width * rect.width / 100;
                    currentRect.height = rect.height;
                    currentRect.x = position == -1 ? rect.x : fixedPosition ? position : rect.x + position * rect.width / 100;
                    currentRect.y = rect.y;
                    rect.x += currentRect.width;
                    return currentRect;
                }

                Rect UseEnd(ref Rect r, float width, bool fixedWidth = false, float positionOffset = -1, bool fixedPosition = false)
                {
                    Rect returnRect = r;
                    returnRect.width = fixedWidth ? width : width * r.width / 100;
                    float positionAdjust = positionOffset == -1 ? 0 : fixedPosition ? positionOffset : positionOffset * r.width / 100;
                    returnRect.x = r.x + r.width - returnRect.width - positionAdjust;
                    r.width -= returnRect.width + positionAdjust;
                    return returnRect;
                }

                UseEnd(ref rect, 32, true, 4, true);
                Rect syncedRect = hasSyncingOption ? UseEnd(ref rect, 54, true) : Rect.zero;
                Rect savedRect = UseEnd(ref rect, 54, true);
                Rect defaultRect = UseEnd(ref rect, 92, true);
                Rect typeRect = UseEnd(ref rect, 42, true);
                UseEnd(ref rect, 48, true);
                Rect nameRect = UseNext(100);

                Rect searchIconRect = nameRect;
                searchIconRect.x += searchIconRect.width / 2 - 40;
                searchIconRect.width = 18;
                Rect searchRect = Rect.zero;
                Rect searchClearRect = Rect.zero;
                #endregion
                
                const string controlName = "VRCSDKParameterSearch";
                if (VRCSDKPlusToolbox.HasReceivedCommand(VRCSDKPlusToolbox.EventCommands.Find)) GUI.FocusControl(controlName);
                VRCSDKPlusToolbox.HandleTextFocusConfirmCommands(controlName, onCancel: () => searchValue = string.Empty);
                bool isFocused = GUI.GetNameOfFocusedControl() == controlName;
                bool isSearching = isFocused || !string.IsNullOrEmpty(searchValue);
                if (isSearching)
                {
                    searchRect = nameRect; searchRect.x += 14; searchRect.width -= 14;
                    searchClearRect = searchRect; searchClearRect.x += searchRect.width - 18; searchClearRect.y -= 1; searchClearRect.width = 16;
                }

                w_MakeRectLinkCursor(searchIconRect);
                if (GUI.Button(searchIconRect, VRCSDKPlusToolbox.GUIContent.Search, centeredLabel))
                    EditorGUI.FocusTextInControl(controlName);
                
                GUI.Label(nameRect, new GUIContent("Name","Name of the Parameter. This must match the name of the parameter that it is controlling in the playable layers. Case sensitive."), centeredLabel);


                // With an empty rect, this would use the last layout rect, which is the list's whole footer.
                if (isSearching) w_MakeRectLinkCursor(searchClearRect);
                if (GUI.Button(searchClearRect, string.Empty, GUIStyle.none))
                {
                    searchValue = string.Empty;
                    if (isFocused) GUI.FocusControl(string.Empty);
                }
                GUI.SetNextControlName(controlName);
                searchValue = GUI.TextField(searchRect, searchValue, "SearchTextField");
                GUI.Button(searchClearRect, VRCSDKPlusToolbox.GUIContent.Clear, centeredLabel);
                GUI.Label(typeRect, new GUIContent("Type", "Type of the Parameter."), centeredLabel);
                GUI.Label(defaultRect, new GUIContent("Default", "The default/start value of this parameter."), centeredLabel);
                GUI.Label(savedRect, new GUIContent("Saved","Value will stay when loading avatar or changing worlds"), centeredLabel);
               
                if (hasSyncingOption) 
                    GUI.Label(syncedRect, new GUIContent("Synced", "Value will be sent over the network to remote users. This is needed if this value should be the same locally and remotely. Synced parameters count towards the total memory usage."), centeredLabel);

            }

            private void HandleParameterEvents()
            {
                if (!parametersOrderList.HasKeyboardControl()) return;
                // The list reports the last row as its index when nothing is selected, so use the selection itself.
                List<int> selected = GetSelectedIndices();
                if (selected.Count == 0) return;
                if (VRCSDKPlusToolbox.HasReceivedCommand(VRCSDKPlusToolbox.EventCommands.Duplicate))
                    DuplicateParameters(selected);
                else if (VRCSDKPlusToolbox.HasReceivedAnyDelete())
                    DeleteParameters(selected);
            }

            
            #region Automated Methods
            [MenuItem("CONTEXT/VRCExpressionParameters/[SDK+] Toggle Editor", false, 899)]
            private static void ToggleEditor()
            {
                editorActive = !editorActive;

                var targetType = ExtendedGetType("VRCExpressionParameters");
                if (targetType == null)
                {
                    Debug.LogError("[VRCSDK+] VRCExpressionParameters was not found! Could not apply custom editor.");
                    return;
                }
                if (editorActive) OverrideEditor(targetType, typeof(VRCParamsPlus));
                else
                {
                    var expressionsEditor = ExtendedGetType("VRCExpressionParametersEditor");
                    if (expressionsEditor == null)
                    {
                        Debug.LogWarning("[VRCSDK+] VRCExpressionParametersEditor was not found! Could not apply custom editor");
                        return;
                    }
                    OverrideEditor(targetType, expressionsEditor);
                }

            }

            private void RefreshAllParameterStatus()
            {
                var expressionParameters = (VRCExpressionParameters)target;
                if (expressionParameters.parameters == null)
                {
                    expressionParameters.parameters = Array.Empty<VRCExpressionParameters.Parameter>();
                    EditorUtility.SetDirty(expressionParameters);
                }
                var parameters = expressionParameters.parameters;
                _parameterStatus = new ParameterStatus[parameters.Length];

                for (int index = 0; index < parameters.Length; index++)
                {
                    var exParameter = expressionParameters.parameters[index];
                    AnimatorControllerParameter matchedParameter = validParameters.FirstOrDefault(conParam => conParam.name == exParameter.name);
                    bool parameterEmpty = string.IsNullOrEmpty(exParameter.name);
                    bool parameterIsValid = matchedParameter != null;
                    bool parameterAddable = avatar && !parameterIsValid && !parameterEmpty;
                    bool parameterIsDuplicate = !parameterEmpty && expressionParameters.parameters.Where((p2, i) => index != i && exParameter.name == p2.name).Any(); ;
                    bool hasWarning = (avatar && !parameterIsValid) || parameterEmpty || parameterIsDuplicate;
                    _parameterStatus[index] = new ParameterStatus()
                    {
                        parameterEmpty = parameterEmpty,
                        parameterAddable = parameterAddable,
                        parameterIsDuplicate = parameterIsDuplicate,
                        hasWarning = hasWarning,
                        matchedParameter = matchedParameter
                    };
                }
            }

            private void CalculateTotalCost()
            {
                currentCost = ((VRCExpressionParameters)target).CalcTotalCost();
            }

            // Same colours as the official VRCExpressionParametersEditor memory bar.
            private static readonly Color memoryFreeColor = new Color(0, 0.5f, 1, 0.5f);
            private static readonly Color memoryLowColor = new Color(0.8f, 0.7f, 0, 0.5f);
            private static readonly Color memoryOutColor = new Color(1, 0, 0, 0.5f);

            private void DrawMemoryBar(Rect rect)
            {
                if (Event.current.type != EventType.Repaint) return;

                int max = MAX_MEMORY_COST;
                EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? new Color(0.16f, 0.16f, 0.16f) : new Color(0.6f, 0.6f, 0.6f));

                Color fillColor = currentCost > max ? memoryOutColor : currentCost > max / 2 ? memoryLowColor : memoryFreeColor;
                Rect fill = rect;
                fill.width = rect.width * Mathf.Clamp01(currentCost / (float)max);
                EditorGUI.DrawRect(fill, fillColor);

                GUI.Label(rect, $"{currentCost}/{max} Synced Bits", centeredLabel);
            }

            private void RefreshParametersOrderList()
            {
                parametersOrderList = new ReorderableList(serializedObject, parameterList, true, true, true, false)
                {
                    drawElementCallback = DrawElement,
                    drawHeaderCallback = DrawHeader,
                    drawFooterCallback = DrawFooter,
                    elementHeightCallback = GetElementHeight,
                    multiSelect = true
                };
                // When this is set, the list no longer calls onReorderCallback.
                parametersOrderList.onReorderCallbackWithDetails = (_, oldIndex, newIndex) =>
                {
                    MoveSelectionWithDraggedRow(oldIndex, newIndex);
                    RefreshAllParameterStatus();
                };
                parametersOrderList.onMouseDragCallback = _ => BeginGroupDrag();
            }

            private void MakeParameterUnique(int index)
            {
                var newElement = parameterList.GetArrayElementAtIndex(index);
                var nameProp = newElement.FindPropertyRelative("name");
                nameProp.stringValue = VRCSDKPlusToolbox.GenerateUniqueString(nameProp.stringValue, newName =>
                {
                    for (int i = 0; i < parameterList.arraySize; i++)
                    {
                        if (i == index) continue;
                        var p = parameterList.GetArrayElementAtIndex(i);
                        if (p.FindPropertyRelative("name").stringValue == newName) return false;
                    }
                    return true;
                });
            }

            #endregion


            private struct ParameterStatus
            {
                internal bool parameterEmpty;
                internal bool parameterAddable;
                internal bool parameterIsDuplicate;
                internal bool hasWarning;
                internal AnimatorControllerParameter matchedParameter;
            }

        }
    }
}
