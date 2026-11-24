using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using AnimatorControllerParameter = UnityEngine.AnimatorControllerParameter;

namespace DreadScripts.VRCSDKPlus
{
    internal sealed partial class VRCSDKPlus
    {
        internal sealed partial class VRCParamsPlus : Editor
        {
            private void DrawElement(Rect rect, int index, bool active, bool focused)
            {
                if (groupDragActive && groupDragRows.Contains(index))
                {
                    // The grabbed row carries the whole group. The other selected rows are drawn inside it.
                    if (index != groupDragRow) return;
                    float y = rect.y;
                    foreach (int member in groupDragRows)
                    {
                        float height = GetRowHeight(member);
                        DrawParameterRow(new Rect(rect.x, y, rect.width, height), member);
                        y += height;
                    }
                    return;
                }

                DrawParameterRow(rect, index);
            }

            // UnityEngine.GUIClip.visibleRect: the visible part of the current clip (the inspector's scroll view), in GUI space.
            private static readonly PropertyInfo guiClipVisibleRect = typeof(GUI).Assembly.GetType("UnityEngine.GUIClip")?.GetProperty("visibleRect", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

            private static bool IsRowVisible(Rect rect)
            {
                if (guiClipVisibleRect == null) return true;
                try { return rect.Overlaps((Rect)guiClipVisibleRect.GetValue(null)); }
                catch { return true; }
            }

            private static string lastSearchPattern;
            private static Regex lastSearchRegex;

            /// <summary>
            /// Case-insensitive search. Valid regular expressions still work as before; text that isn't a valid
            /// regular expression (e.g. "(" or "[") is matched literally instead of throwing.
            /// </summary>
            private static bool MatchesSearch(string text, string search)
            {
                if (string.IsNullOrEmpty(search)) return true;
                text = text ?? string.Empty;
                if (search != lastSearchPattern)
                {
                    lastSearchPattern = search;
                    try { lastSearchRegex = new Regex(search, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50)); }
                    catch (ArgumentException) { lastSearchRegex = null; }
                }

                if (lastSearchRegex != null)
                {
                    try { return lastSearchRegex.IsMatch(text); }
                    catch (RegexMatchTimeoutException) { }
                }
                return text.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
            }

            private void DrawParameterRow(Rect rect, int index)
            {
                if (!(index < parameterList.arraySize && index >= 0)) return;
                // DoLayoutList doesn't cull, so skip rows outside the inspector's visible area (long lists).
                if (!IsRowVisible(rect)) return;

                SerializedProperty parameter = parameterList.GetArrayElementAtIndex(index);
                SerializedProperty name = parameter.FindPropertyRelative("name");
                SerializedProperty valueType = parameter.FindPropertyRelative("valueType");
                SerializedProperty defaultValue = parameter.FindPropertyRelative("defaultValue");
                SerializedProperty saved = parameter.FindPropertyRelative("saved");
                SerializedProperty synced = hasSyncingOption ? parameter.FindPropertyRelative("networkSynced") : null;
                SerializedProperty bitWidth = parameter.FindPropertyRelative("bitWidth");
                SerializedProperty flippedRep = parameter.FindPropertyRelative("flippedRepresentation");
                SerializedProperty useLerp = parameter.FindPropertyRelative("useCustomLerpDuration");
                SerializedProperty lerpDuration = parameter.FindPropertyRelative("customLerpDuration");
                bool showAdvanced = hasAdvancedOptions && bitWidth != null && flippedRep != null && useLerp != null && lerpDuration != null;

                // If a parameter gets removed from the list, or added to the list while it is open,
                // then the 2 lists won't match up, and that leads to an exception
                if (parameterList.arraySize != _parameterStatus.Length)
                {
                    RefreshAllParameterStatus();
                }

                var status = _parameterStatus[index];
                bool parameterEmpty = status.parameterEmpty;
                bool parameterAddable = status.parameterAddable;
                bool parameterIsDuplicate = status.parameterIsDuplicate;
                bool hasWarning = status.hasWarning;
                string warnMsg = parameterEmpty ? "Blank Parameter" : parameterIsDuplicate ? "Duplicate Parameter! May cause issues!" : "Parameter not found in any playable controller of Active Avatar";
                AnimatorControllerParameter matchedParameter = status.matchedParameter;

                // Untouched element rect; "rect" is shrunk to the first line below.
                Rect fullRect = rect;

                #region Rects
                rect.y += 1;
                rect.height = 18;


                Rect UseNext( float width, bool fixedWidth = false, float position = -1, bool fixedPosition = false)
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
                
                Rect contextRect = rect;
                contextRect.x -= 20;
                contextRect.width = 20;
                
                Rect removeRect = UseEnd(ref rect, 32, true, 4, true);
                Rect syncedRect = hasSyncingOption ? UseEnd(ref rect, 18, true, 16f, true) : Rect.zero;
                Rect savedRect = UseEnd(ref rect, 18, true, hasSyncingOption ? 34f : 16, true);
                Rect defaultRect = UseEnd(ref rect, 60, true, 32, true);
                Rect typeRect = UseEnd(ref rect, 52, true, 12, true);
                Rect warnRect = UseEnd(ref rect, 18, true, 4, true);
                Rect addRect = hasWarning && parameterAddable ? UseEnd(ref rect, 32, true, 4, true) : Rect.zero;
                Rect foldoutRect = showAdvanced ? UseNext(14, true) : Rect.zero;
                // UseNext moves the rect without shrinking it, so the arrow's width has to come off the name.
                Rect nameRect = UseNext(rect.width - foldoutRect.width - 4, true);
                #endregion

                using (new EditorGUI.DisabledScope(!string.IsNullOrEmpty(searchValue) && !MatchesSearch(name.stringValue, searchValue)))
                {
                    if (showAdvanced)
                    {
                        int typeIndex = valueType.enumValueIndex;
                        int bits = EffectiveBits(typeIndex, bitWidth.intValue);
                        bool nonDefault = HasNonDefaultAdvanced(typeIndex, bits, flippedRep.boolValue, useLerp.boolValue);
                        string tooltip = nonDefault
                            ? GetAdvancedSummary(typeIndex, bits, flippedRep.boolValue, useLerp.boolValue, lerpDuration.floatValue)
                            : "Advanced options";

                        // In the inspector (hierarchyMode) Foldout draws its arrow ~12px left of the rect,
                        // which lands on the list's drag handle. Turn that off for this one control.
                        Event current = Event.current;
                        if (current.type == EventType.MouseDown && current.button == 0 && foldoutRect.Contains(current.mousePosition))
                            foldoutClicked = true;

                        bool hierarchyMode = EditorGUIUtility.hierarchyMode;
                        EditorGUIUtility.hierarchyMode = false;
                        parameter.isExpanded = EditorGUI.Foldout(foldoutRect, parameter.isExpanded, new GUIContent(string.Empty, tooltip), false);
                        EditorGUIUtility.hierarchyMode = hierarchyMode;
                        if (nonDefault)
                            EditorGUI.DrawRect(new Rect(foldoutRect.xMax - 4, foldoutRect.y + 2, 4, 4), new Color(0.3f, 0.7f, 1f));
                    }

                    //Hacky way to avoid proper UI Layout
                    string parameterFieldName = $"namefield{index}";
                    
                    GUI.SetNextControlName(parameterFieldName);
                    EditorGUI.PropertyField(nameRect, name, GUIContent.none);
                    EditorGUI.BeginChangeCheck();
                    EditorGUI.PropertyField(typeRect, valueType, GUIContent.none);
                    if (EditorGUI.EndChangeCheck() && showAdvanced)
                    {
                        // Same reset the official editor does when the type changes.
                        bool isBool = valueType.enumValueIndex == 2;
                        bitWidth.intValue = isBool ? 1 : 8;
                        flippedRep.boolValue = false;
                        if (isBool) useLerp.boolValue = false;
                    }
                    EditorGUI.PropertyField(savedRect, saved, GUIContent.none);

                    if (matchedParameter != null)
                        GUI.Label(nameRect, $"({matchedParameter.type})", VRCSDKPlusToolbox.Styles.Label.RightPlaceHolder);

                    if (hasSyncingOption) EditorGUI.PropertyField(syncedRect, synced, GUIContent.none);

                    if (parameterAddable)
                    {
                        w_MakeRectLinkCursor(addRect);
                        if (EditorGUI.DropdownButton(addRect, new GUIContent("+", "Add this parameter to one of the avatar's controllers"), FocusType.Keyboard, EditorStyles.popup))
                            ShowAddToAnimatorMenu(addRect, name.stringValue, valueType.enumValueIndex, defaultValue.floatValue);
                    }

                    if (hasWarning) GUI.Label(warnRect, new GUIContent(yellowWarnIcon) {tooltip = warnMsg});

                    switch (valueType.enumValueIndex)
                    {
                        case 2:
                            EditorGUI.BeginChangeCheck();
                            int dummy = EditorGUI.Popup(defaultRect, defaultValue.floatValue == 0 ? 0 : 1, new[] {"False", "True"});
                            if (EditorGUI.EndChangeCheck())
                                defaultValue.floatValue = dummy;
                            break;
                        default:
                            EditorGUI.BeginChangeCheck();
                            EditorGUI.PropertyField(defaultRect, defaultValue, GUIContent.none);
                            if (EditorGUI.EndChangeCheck())
                            {
                                // Ints are stored as a float; keep them whole.
                                if (valueType.enumValueIndex == 0)
                                    defaultValue.floatValue = Mathf.Round(defaultValue.floatValue);
                                if (showAdvanced)
                                {
                                    int typeIndex = valueType.enumValueIndex;
                                    ClampDefaultValue(valueType, defaultValue, EffectiveBits(typeIndex, bitWidth.intValue), IsSigned(typeIndex, flippedRep.boolValue));
                                }
                            }
                            break;
                    }

                    if (showAdvanced && parameter.isExpanded)
                    {
                        Rect advancedRect = new Rect(nameRect.x, fullRect.y + parametersOrderList.elementHeight + 1, removeRect.xMax - nameRect.x, 18);
                        Event current = Event.current;
                        if (current.type == EventType.MouseDown && current.button == 0 && advancedRect.Contains(current.mousePosition))
                            advancedLineClicked = true;
                        DrawAdvancedLine(advancedRect, valueType, defaultValue, bitWidth, flippedRep, useLerp, lerpDuration);
                    }

                    w_MakeRectLinkCursor(removeRect);
                    if (GUI.Button(removeRect, VRCSDKPlusToolbox.GUIContent.Remove, VRCSDKPlusToolbox.Styles.Label.RemoveIcon))
                        DeleteParameter(index);
                }

                var e = Event.current;
                if (e.type == EventType.ContextClick && contextRect.Contains(e.mousePosition))
                {
                    e.Use();
                    List<int> targets = GetTargetIndices(index);
                    string suffix = targets.Count > 1 ? $" {targets.Count} Parameters" : string.Empty;
                    var menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Duplicate" + suffix), false, () => DuplicateParameters(targets));
                    menu.AddSeparator(string.Empty);
                    menu.AddItem(new GUIContent("Delete" + suffix), false, () => DeleteParameters(targets));
                    menu.ShowAsContext();
                }
            }
        }
    }
}
