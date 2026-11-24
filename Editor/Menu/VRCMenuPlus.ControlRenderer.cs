using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace DreadScripts.VRCSDKPlus
{
    internal sealed partial class VRCSDKPlus
    {
        internal sealed partial class VRCMenuPlus : Editor, IHasCustomMenu
        {
            private static class ControlRenderer
            {
                private const float IconSize = 96;
                private const float IconSpace = IconSize + 3;

                private const float CompactIconSize = 60;
                private const float CompactIconSpace = CompactIconSize + 3;

                /// <summary>
                /// Like the official editor: only SubMenu controls keep a submenu, so menus that aren't used
                /// don't end up in avatar builds and validation.
                /// </summary>
                private static void ClearUnusedSubMenu(SerializedProperty property)
                {
                    if (property == null || property.FindPropertyRelative("type").ToControlType() == VRCExpressionsMenu.Control.ControlType.SubMenu) return;
                    SerializedProperty subMenu = property.FindPropertyRelative("subMenu");
                    if (subMenu.objectReferenceValue != null) subMenu.objectReferenceValue = null;
                }

                public static void DrawControl(SerializedProperty property, VRCExpressionParameters parameters)
                {
                    ClearUnusedSubMenu(property);
                    MainContainer(property, parameters);
                    EditorGUILayout.Separator();
                    ParameterContainer(property, parameters);

                    if (property != null)
                    {
                        EditorGUILayout.Separator();

                        switch ((VRCExpressionsMenu.Control.ControlType)property.FindPropertyRelative("type").intValue)
                        {
                            case VRCExpressionsMenu.Control.ControlType.RadialPuppet:
                                RadialContainer(property, parameters);
                                break;
                            case VRCExpressionsMenu.Control.ControlType.SubMenu:
                                SubMenuContainer(property);
                                break;
                            case VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet:
                                TwoAxisParametersContainer(property, parameters);
                                EditorGUILayout.Separator();
                                AxisCustomisationContainer(property);
                                break;
                            case VRCExpressionsMenu.Control.ControlType.FourAxisPuppet:
                                FourAxisParametersContainer(property, parameters);
                                EditorGUILayout.Separator();
                                AxisCustomisationContainer(property);
                                break;
                        }
                    }
                }

                public static void DrawControlCompact(SerializedProperty property, VRCExpressionParameters parameters)
                {
                    ClearUnusedSubMenu(property);
                    CompactMainContainer(property, parameters);

                    if (property != null)
                    {
                        switch ((VRCExpressionsMenu.Control.ControlType)property.FindPropertyRelative("type").intValue)
                        {
                            case VRCExpressionsMenu.Control.ControlType.RadialPuppet:
                                RadialContainer(property, parameters);
                                break;
                            case VRCExpressionsMenu.Control.ControlType.SubMenu:
                                SubMenuContainer(property);
                                break;
                            case VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet:
                                CompactTwoAxisParametersContainer(property, parameters);
                                break;
                            case VRCExpressionsMenu.Control.ControlType.FourAxisPuppet:
                                CompactFourAxisParametersContainer(property, parameters);
                                break;
                        }
                    }
                }

                #region Main container

                static void MainContainer(SerializedProperty property, VRCExpressionParameters parameters)
                {
                    var rect = EditorGUILayout
                        .GetControlRect(false, 147);
                    VRCSDKPlusToolbox.Container.GUIBox(ref rect);

                    var nameRect = new Rect(rect.x, rect.y, rect.width - IconSpace, 21);
                    var typeRect = new Rect(rect.x, rect.y + 24, rect.width - IconSpace, 21);
                    var baseStyleRect = new Rect(rect.x, rect.y + 48, rect.width - IconSpace, 21);
                    var iconRect = new Rect(rect.x + rect.width - IconSize, rect.y, IconSize, IconSize);
                    var helpRect = new Rect(rect.x, rect.y + IconSpace, rect.width, 42);

                    DrawName(nameRect, property, true);
                    DrawType(typeRect, property, true);
                    DrawStyle(baseStyleRect, property, true);
                    DrawIcon(iconRect, property);
                    DrawHelp(helpRect, property, parameters);
                }

                static void CompactMainContainer(SerializedProperty property, VRCExpressionParameters parameters)
                {
                    var rect = EditorGUILayout.GetControlRect(false, 66);
                    VRCSDKPlusToolbox.Container.GUIBox(ref rect);

                    var halfWidth = (rect.width - CompactIconSpace) / 2;
                    var nameRect = new Rect(rect.x, rect.y, halfWidth - 3, 18);
                    var typeRect = new Rect(rect.x + halfWidth, rect.y, halfWidth - 19, 18);
                    var helpRect = new Rect(typeRect.x + typeRect.width + 1, rect.y, 18, 18);
                    var parameterRect = new Rect(rect.x, rect.y + 21, rect.width - CompactIconSpace, 18);
                    var styleRect = new Rect(rect.x, rect.y + 42, rect.width - CompactIconSize, 18);
                    var iconRect = new Rect(rect.x + rect.width - CompactIconSize, rect.y, CompactIconSize, CompactIconSize);

                    DrawName(nameRect, property, false);
                    DrawType(typeRect, property, false);
                    DrawStyle(styleRect, property, false);

                    if (property != null)
                        GUI.Label(helpRect, new GUIContent(VRCSDKPlusToolbox.GUIContent.Help) { tooltip = GetHelpMessage(property, parameters) }, GUIStyle.none);

                    ParameterContainer(property, parameters, parameterRect);

                    DrawIcon(iconRect, property);
                }

                static void DrawName(Rect rect, SerializedProperty property, bool drawLabel)
                {
                    if (property == null)
                    {
                        VRCSDKPlusToolbox.Placeholder.GUI(rect);
                        return;
                    }

                    var name = property.FindPropertyRelative("name");

                    if (drawLabel)
                    {
                        var label = new Rect(rect.x, rect.y, 100, rect.height);
                        rect = new Rect(rect.x + 103, rect.y, rect.width - 103, rect.height);

                        GUI.Label(label, "Name");
                    }

                    name.stringValue = EditorGUI.TextField(rect, name.stringValue);
                    if (string.IsNullOrEmpty(name.stringValue)) GUI.Label(rect, "Name", VRCSDKPlusToolbox.Styles.Label.PlaceHolder);
                }

                static void DrawType(Rect rect, SerializedProperty property, bool drawLabel)
                {
                    if (property == null)
                    {
                        VRCSDKPlusToolbox.Placeholder.GUI(rect);
                        return;
                    }

                    if (drawLabel)
                    {
                        var label = new Rect(rect.x, rect.y, 100, rect.height);
                        rect = new Rect(rect.x + 103, rect.y, rect.width - 103, rect.height);

                        GUI.Label(label, "Type");
                    }

                    var controlType = property.FindPropertyRelative("type").ToControlType();
                    var newType = (VRCExpressionsMenu.Control.ControlType)EditorGUI.EnumPopup(rect, controlType);

                    if (newType != controlType)
                        ConversionEntry(property, controlType, newType);
                }

                static void DrawStyle(Rect rect, SerializedProperty property, bool drawLabel)
                {
                    const float toggleSize = 21;

                    if (property == null)
                    {
                        VRCSDKPlusToolbox.Placeholder.GUI(rect);
                        return;
                    }

                    if (drawLabel)
                    {
                        Rect labelRect = new Rect(rect.x, rect.y, 100, rect.height);
                        rect = new Rect(rect.x + 103, rect.y, rect.width - 103, rect.height);
                        GUI.Label(labelRect, "Style");
                    }

                    Rect colorRect = new Rect(rect.x, rect.y, rect.width - (toggleSize + 3) * 2, rect.height);
                    Rect boldRect = new Rect(colorRect.x + colorRect.width, rect.y, toggleSize, rect.height);
                    Rect italicRect = new Rect(boldRect); italicRect.x += italicRect.width + 3; boldRect.width = toggleSize;
                    string rawName = property.FindPropertyRelative("name").stringValue;

                    var isBold = VRCSDKPlusToolbox.RichText.HasTag(rawName, 'b');
                    var isItalic = VRCSDKPlusToolbox.RichText.HasTag(rawName, 'i');
                    VRCSDKPlusToolbox.RichText.TryGetColor(rawName, out Color textColor);

                    EditorGUI.BeginChangeCheck();
                    textColor = EditorGUI.ColorField(colorRect, textColor);
                    if (EditorGUI.EndChangeCheck())
                    {
                        rawName = Regex.Replace(rawName, @"</?color\s*(=[^<>]*)?>|<#[0-9a-f]+>", string.Empty, RegexOptions.IgnoreCase);
                        rawName = $"<color=#{ColorUtility.ToHtmlStringRGB(textColor)}>{rawName}</color>";
                    }
                    
                    void SetCharTag(char c, bool state)
                    {
                        rawName = !state ?
                            Regex.Replace(rawName, $@"</?{c}>", string.Empty, RegexOptions.IgnoreCase) : 
                            $"<{c}>{rawName}</{c}>";
                    }

                    w_MakeRectLinkCursor(boldRect);
                    EditorGUI.BeginChangeCheck();
                    isBold = GUI.Toggle(boldRect, isBold, new GUIContent("<b>b</b>","Bold"), VRCSDKPlusToolbox.Styles.letterButton);
                    if (EditorGUI.EndChangeCheck()) SetCharTag('b', isBold);

                    w_MakeRectLinkCursor(italicRect);
                    EditorGUI.BeginChangeCheck();
                    isItalic = GUI.Toggle(italicRect, isItalic, new GUIContent("<i>i</i>", "Italic"), VRCSDKPlusToolbox.Styles.letterButton);
                    if (EditorGUI.EndChangeCheck()) SetCharTag('i', isItalic);


                    property.FindPropertyRelative("name").stringValue = rawName;
                }
                static void DrawIcon(Rect rect, SerializedProperty property)
                {
                    if (property == null)
                        VRCSDKPlusToolbox.Placeholder.GUI(rect);
                    else
                    {
                        var value = property.FindPropertyRelative("icon");

                        value.objectReferenceValue = EditorGUI.ObjectField(
                            rect,
                            string.Empty,
                            value.objectReferenceValue,
                            typeof(Texture2D),
                            false
                        );
                    }
                }
                static void DrawHelp(Rect rect, SerializedProperty property, VRCExpressionParameters parameters)
                {
                    if (property == null)
                    {
                        VRCSDKPlusToolbox.Placeholder.GUI(rect);
                        return;
                    }

                    string message = GetHelpMessage(property, parameters);
                    EditorGUI.HelpBox(rect, message, MessageType.Info);
                }
                static string GetHelpMessage(SerializedProperty property, VRCExpressionParameters parameters)
                {
                    string message = GetHelpMessage(property);
                    string parameterName = property.FindPropertyRelative("parameter").FindPropertyRelative("name").stringValue;
                    bool isBool = !string.IsNullOrEmpty(parameterName) && parameters?.parameters != null &&
                                  parameters.parameters.Any(p => p.name == parameterName && p.valueType == VRCExpressionParameters.ValueType.Bool);
                    if (!isBool) return message;

                    message = message.Replace("reset to zero", "reset to false");
                    return hasValueSwitch ? message : message.Replace("set to (Value)", "set to true");
                }

                static string GetHelpMessage(SerializedProperty property)
                {
                    switch (property.FindPropertyRelative("type").ToControlType())
                    {
                        case VRCExpressionsMenu.Control.ControlType.Button:
                            return "Click or hold to activate. The button remains active for a minimum 0.2s.\nWhile active the (Parameter) is set to (Value).\nWhen inactive the (Parameter) is reset to zero.";
                        case VRCExpressionsMenu.Control.ControlType.Toggle:
                            return "Click to toggle on or off.\nWhen turned on the (Parameter) is set to (Value).\nWhen turned off the (Parameter) is reset to zero.";
                        case VRCExpressionsMenu.Control.ControlType.SubMenu:
                            return "Opens another expression menu.\nWhen opened the (Parameter) is set to (Value).\nWhen closed (Parameter) is reset to zero.";
                        case VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet:
                            return "Puppet menu that maps the joystick to two parameters (-1 to +1).\nWhen opened the (Parameter) is set to (Value).\nWhen closed (Parameter) is reset to zero.";
                        case VRCExpressionsMenu.Control.ControlType.FourAxisPuppet:
                            return "Puppet menu that maps the joystick to four parameters (0 to 1).\nWhen opened the (Parameter) is set to (Value).\nWhen closed (Parameter) is reset to zero.";
                        case VRCExpressionsMenu.Control.ControlType.RadialPuppet:
                            return "Puppet menu that sets a value based on joystick rotation. (0 to 1)\nWhen opened the (Parameter) is set to (Value).\nWhen closed (Parameter) is reset to zero.";
                        default:
                            return "ERROR: Unable to load message - Invalid control type";
                    }
                }

                #endregion

                #region Type Conversion

                private static void ConversionEntry(SerializedProperty property, VRCExpressionsMenu.Control.ControlType tOld, VRCExpressionsMenu.Control.ControlType tNew)
                {
                    // The main parameter is kept on every conversion: submenus and puppets also set it (to Value) while open.
                    if (
                        (tOld != VRCExpressionsMenu.Control.ControlType.Button && tOld != VRCExpressionsMenu.Control.ControlType.Toggle) &&
                        (tNew == VRCExpressionsMenu.Control.ControlType.Button || tNew == VRCExpressionsMenu.Control.ControlType.Toggle)
                    )
                        SetupSubParameters(property, tNew);

                    // Is either a submenu
                    if (tOld == VRCExpressionsMenu.Control.ControlType.SubMenu || tNew == VRCExpressionsMenu.Control.ControlType.SubMenu)
                        SetupSubParameters(property, tNew);

                    // Is either Puppet)
                    if (IsPuppetConversion(tOld, tNew))
                        DoPuppetConversion(property, tNew);
                    else if (
                        tNew == VRCExpressionsMenu.Control.ControlType.RadialPuppet ||
                        tNew == VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet ||
                        tNew == VRCExpressionsMenu.Control.ControlType.FourAxisPuppet
                    )
                        SetupSubParameters(property, tNew);

                    property.FindPropertyRelative("type").enumValueIndex = tNew.GetEnumValueIndex();
                    ClearUnusedSubMenu(property);
                }

                private static bool IsPuppetConversion(VRCExpressionsMenu.Control.ControlType tOld, VRCExpressionsMenu.Control.ControlType tNew)
                {
                    return (
                               tOld == VRCExpressionsMenu.Control.ControlType.RadialPuppet ||
                               tOld == VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet ||
                               tOld == VRCExpressionsMenu.Control.ControlType.FourAxisPuppet
                           ) &&
                           (
                               tNew == VRCExpressionsMenu.Control.ControlType.RadialPuppet ||
                               tNew == VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet ||
                               tNew == VRCExpressionsMenu.Control.ControlType.FourAxisPuppet
                           );
                }

                private static void DoPuppetConversion(SerializedProperty property, VRCExpressionsMenu.Control.ControlType tNew)
                {
                    // Sub-parameter order: Radial [Rotation], Two-Axis [Horizontal, Vertical], Four-Axis [Up, Right, Down, Left].
                    // Map by meaning, not by position: Horizontal <-> Right, Vertical <-> Up.
                    var tOld = property.FindPropertyRelative("type").ToControlType();
                    var subParameters = property.FindPropertyRelative("subParameters");
                    string Sub(int i) => subParameters.arraySize > i
                        ? subParameters.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue
                        : string.Empty;

                    string first = Sub(0);
                    string horizontal = tOld == VRCExpressionsMenu.Control.ControlType.FourAxisPuppet ? Sub(1) : Sub(0);
                    string vertical = tOld == VRCExpressionsMenu.Control.ControlType.FourAxisPuppet ? Sub(0) : Sub(1);
                    if (tOld == VRCExpressionsMenu.Control.ControlType.RadialPuppet) vertical = string.Empty;

                    string[] names;
                    switch (tNew)
                    {
                        case VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet:
                            names = new[] { horizontal, vertical };
                            break;
                        case VRCExpressionsMenu.Control.ControlType.FourAxisPuppet:
                            names = tOld == VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet
                                ? new[] { vertical, horizontal, string.Empty, string.Empty } // Up, Right, Down, Left
                                : new[] { first, string.Empty, string.Empty, string.Empty };
                            break;
                        default: // Radial
                            names = new[] { first };
                            break;
                    }

                    subParameters.ClearArray();
                    subParameters.arraySize = names.Length;
                    for (int i = 0; i < names.Length; i++)
                        subParameters.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue = names[i];
                }

                private static void SetupSubParameters(SerializedProperty property, VRCExpressionsMenu.Control.ControlType type)
                {
                    var subParameters = property.FindPropertyRelative("subParameters");
                    subParameters.ClearArray();

                    switch (type)
                    {
                        case VRCExpressionsMenu.Control.ControlType.RadialPuppet:
                        case VRCExpressionsMenu.Control.ControlType.SubMenu:
                            subParameters.InsertArrayElementAtIndex(0);
                            break;
                        case VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet:
                            subParameters.InsertArrayElementAtIndex(0);
                            subParameters.InsertArrayElementAtIndex(1);
                            break;
                        case VRCExpressionsMenu.Control.ControlType.FourAxisPuppet:
                            subParameters.InsertArrayElementAtIndex(0);
                            subParameters.InsertArrayElementAtIndex(1);
                            subParameters.InsertArrayElementAtIndex(2);
                            subParameters.InsertArrayElementAtIndex(3);
                            break;
                    }
                }

                #endregion



                #region BuildParameterArray

                static void BuildParameterArray(
                    string name,
                    VRCExpressionParameters parameters,
                    out int index,
                    out string[] parametersAsString
                )
                {
                    index = -2;
                    if (!parameters)
                    {
                        parametersAsString = Array.Empty<string>();
                        return;
                    }

                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        for (var i = 0; i < parameters.parameters.Length; i++)
                        {
                            if (parameters.parameters[i].name != name) continue;

                            index = i + 1;
                            break;
                        }
                    }
                    else
                        index = -1;

                    parametersAsString = new string[parameters.parameters.Length + 1];
                    parametersAsString[0] = "[None]";
                    for (var i = 0; i < parameters.parameters.Length; i++)
                    {
                        switch (parameters.parameters[i].valueType)
                        {
                            case VRCExpressionParameters.ValueType.Int:
                                parametersAsString[i + 1] = $"{parameters.parameters[i].name} [int]";
                                break;
                            case VRCExpressionParameters.ValueType.Float:
                                parametersAsString[i + 1] = $"{parameters.parameters[i].name} [float]";
                                break;
                            case VRCExpressionParameters.ValueType.Bool:
                                parametersAsString[i + 1] = $"{parameters.parameters[i].name} [bool]";
                                break;
                        }
                    }
                }

                static void BuildParameterArray(
                    string name,
                    VRCExpressionParameters parameters,
                    out int index,
                    out VRCExpressionParameters.Parameter[] filteredParameters,
                    out string[] filteredParametersAsString
                )
                {
                    index = -2;
                    if (!parameters)
                    {
                        filteredParameters = Array.Empty<VRCExpressionParameters.Parameter>();
                        filteredParametersAsString = Array.Empty<string>();
                        return;
                    }

                    filteredParameters = parameters.parameters.Where(p => p.valueType != VRCExpressionParameters.ValueType.Bool).ToArray();

                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        for (var i = 0; i < filteredParameters.Length; i++)
                        {
                            if (filteredParameters[i].name != name) continue;

                            index = i + 1;
                            break;
                        }
                    }
                    else
                        index = -1;

                    filteredParametersAsString = new string[filteredParameters.Length + 1];
                    filteredParametersAsString[0] = "[None]";
                    for (var i = 0; i < filteredParameters.Length; i++)
                    {
                        switch (filteredParameters[i].valueType)
                        {
                            case VRCExpressionParameters.ValueType.Int:
                                filteredParametersAsString[i + 1] = $"{filteredParameters[i].name} [int]";
                                break;
                            case VRCExpressionParameters.ValueType.Float:
                                filteredParametersAsString[i + 1] = $"{filteredParameters[i].name} [float]";
                                break;
                            case VRCExpressionParameters.ValueType.Bool:
                                filteredParametersAsString[i + 1] = $"{filteredParameters[i].name} [bool]";
                                break;
                        }
                    }
                }

                #endregion

                #region DrawParameterSelector

                struct ParameterSelectorOptions
                {
                    public Action extraGUI;
                    public Rect rect;
                    public bool required;

                    public ParameterSelectorOptions(Rect rect, bool required, Action extraGUI = null)
                    {
                        this.required = required;
                        this.rect = rect;
                        this.extraGUI = extraGUI;
                    }

                    public ParameterSelectorOptions(Rect rect, Action extraGUI = null)
                    {
                        this.required = false;
                        this.rect = rect;
                        this.extraGUI = extraGUI;
                    }

                    public ParameterSelectorOptions(bool required, Action extraGUI = null)
                    {
                        this.required = required;
                        this.rect = default;
                        this.extraGUI = extraGUI;
                    }
                }

                private static bool DrawParameterSelector(
                    string label,
                    SerializedProperty property,
                    VRCExpressionParameters parameters,
                    ParameterSelectorOptions options = default
                )
                {
                    BuildParameterArray(
                        property.FindPropertyRelative("name").stringValue,
                        parameters,
                        out var index,
                        out var parametersAsString
                    );
                    return DrawParameterSelection__BASE(
                        label,
                        property,
                        index,
                        parameters,
                        parameters?.parameters,
                        parametersAsString,
                        false,
                        options
                    );
                }

                /// <summary>For the axes of puppets, which can't be Bools.</summary>
                private static bool DrawPuppetParameterSelector(
                    string label,
                    SerializedProperty property,
                    VRCExpressionParameters parameters,
                    ParameterSelectorOptions options = default
                )
                {
                    BuildParameterArray(
                        property.FindPropertyRelative("name").stringValue,
                        parameters,
                        out var index,
                        out var filteredParameters,
                        out var parametersAsString
                    );
                    return DrawParameterSelection__BASE(
                        label,
                        property,
                        index,
                        parameters,
                        filteredParameters,
                        parametersAsString,
                        true,
                        options
                    );
                }

                /// <summary>
                /// Growing the array copies the last parameter (or gives zeroed fields when the list is empty), so every
                /// field is set here. Fields the installed SDK doesn't have are skipped.
                /// </summary>
                private static void AddExpressionParameter(VRCExpressionParameters target, string parameterName, int valueTypeIndex)
                {
                    if (!target) return;
                    SerializedObject so = new SerializedObject(target);
                    SerializedProperty list = so.FindProperty("parameters");
                    SerializedProperty parameter = list.GetArrayElementAtIndex(list.arraySize++);

                    parameter.FindPropertyRelative("name").stringValue = parameterName;
                    parameter.FindPropertyRelative("valueType").enumValueIndex = valueTypeIndex;
                    parameter.FindPropertyRelative("defaultValue").floatValue = 0;
                    parameter.FindPropertyRelative("saved").boolValue = true;

                    SerializedProperty synced = parameter.FindPropertyRelative("networkSynced");
                    if (synced != null) synced.boolValue = true;
                    SerializedProperty bitWidth = parameter.FindPropertyRelative("bitWidth");
                    if (bitWidth != null) bitWidth.intValue = valueTypeIndex == 2 ? 1 : 8; // Bool : Int/Float
                    SerializedProperty flippedRepresentation = parameter.FindPropertyRelative("flippedRepresentation");
                    if (flippedRepresentation != null) flippedRepresentation.boolValue = false;
                    SerializedProperty useCustomLerp = parameter.FindPropertyRelative("useCustomLerpDuration");
                    if (useCustomLerp != null) useCustomLerp.boolValue = false;
                    SerializedProperty customLerp = parameter.FindPropertyRelative("customLerpDuration");
                    if (customLerp != null) customLerp.floatValue = 0;

                    SerializedProperty isEmpty = so.FindProperty("isEmpty");
                    if (isEmpty != null) isEmpty.boolValue = false;

                    so.ApplyModifiedProperties();
                }

                private static bool DrawParameterSelection__BASE(
                    string label,
                    SerializedProperty property,
                    int index,
                    VRCExpressionParameters targetParameters,
                    VRCExpressionParameters.Parameter[] parameters,
                    string[] parametersAsString,
                    bool isFiltered,
                    ParameterSelectorOptions options
                )
                {
                    var isEmpty = index == -1;
                    var isMissing = index == -2;
                    bool willWarn = isMissing || options.required && isEmpty;
                    string parameterName = property.FindPropertyRelative("name").stringValue;
                    // It exists, but it's a Bool. Adding it again would only create a duplicate.
                    bool isInvalidBool = isFiltered && isMissing && targetParameters && targetParameters.parameters.Any(p => p.name == parameterName);
                    bool canAdd = isMissing && !isInvalidBool;
                    string warnMsg = targetParameters ? isMissing ? isInvalidBool ?
                                $"Parameter ({parameterName}) is a Bool, which can't be used here" :
                                $"Parameter ({parameterName}) not found on the active avatar descriptor" :
                            "Parameter is blank. Control may be dysfunctional." :
                        VRCSDKPlusToolbox.GUIContent.MissingParametersTooltip;

                    var rectNotProvided = options.rect == default;
                    using (new GUILayout.HorizontalScope())
                    {
                        const float CONTENT_ADD_WIDTH = 50;
                        const float CONTENT_WARN_WIDTH = 18;
                        const float CONTENT_DROPDOWN_WIDTH = 20;
                        float missingFullWidth = CONTENT_ADD_WIDTH + CONTENT_WARN_WIDTH + 2;

                        bool hasLabel = !string.IsNullOrEmpty(label);

                        if (rectNotProvided) options.rect = EditorGUILayout.GetControlRect(false, 18);

                        var name = property.FindPropertyRelative("name");

                        Rect labelRect = new Rect(options.rect) { width = hasLabel ? 120 : 0 };
                        Rect textfieldRect = new Rect(labelRect) { x = labelRect.x + labelRect.width, width = options.rect.width - labelRect.width - CONTENT_DROPDOWN_WIDTH - 2 };
                        Rect dropdownRect = new Rect(textfieldRect) { x = textfieldRect.x + textfieldRect.width, width = CONTENT_DROPDOWN_WIDTH };
                        Rect addRect = Rect.zero;
                        Rect warnRect = Rect.zero;

                        if (targetParameters && canAdd)
                        {
                            textfieldRect.width -= missingFullWidth;
                            dropdownRect.x -= missingFullWidth;
                            addRect = new Rect(options.rect) { x = textfieldRect.x + textfieldRect.width + CONTENT_DROPDOWN_WIDTH + 2, width = CONTENT_ADD_WIDTH };
                            warnRect = new Rect(addRect) { x = addRect.x + addRect.width, width = CONTENT_WARN_WIDTH };
                        }
                        else if (!targetParameters || isInvalidBool || options.required && isEmpty)
                        {
                            textfieldRect.width -= CONTENT_WARN_WIDTH;
                            dropdownRect.x -= CONTENT_WARN_WIDTH;
                            warnRect = new Rect(dropdownRect) { x = dropdownRect.x + dropdownRect.width, width = CONTENT_WARN_WIDTH };
                        }

                        if (hasLabel) GUI.Label(labelRect, label);
                        using (new EditorGUI.DisabledScope(!targetParameters || parametersAsString.Length <= 1))
                        {
                            var newIndex = EditorGUI.Popup(dropdownRect, string.Empty, index, parametersAsString);
                            if (index != newIndex)
                                name.stringValue = newIndex == 0 ? string.Empty : parameters[newIndex - 1].name;
                        }

                        name.stringValue = EditorGUI.TextField(textfieldRect, name.stringValue);
                        if (string.IsNullOrEmpty(name.stringValue)) GUI.Label(textfieldRect, "Parameter", VRCSDKPlusToolbox.Styles.Label.PlaceHolder);
                        if (willWarn) GUI.Label(warnRect, new GUIContent(VRCSDKPlusToolbox.GUIContent.Warn) { tooltip = warnMsg });

                        if (canAdd)
                        {
                            int dummy;

                            if (!isFiltered)
                            {
                                dummy = EditorGUI.Popup(addRect, -1, Enum.GetNames(typeof(VRCExpressionParameters.ValueType)));

                                addRect.x += 3;
                                GUI.Label(addRect, "Add");
                            }
                            else dummy = GUI.Button(addRect, "Add") ? 1 : -1;

                            if (dummy != -1)
                            {
                                AddExpressionParameter(targetParameters, name.stringValue, dummy);
                            }
                        }

                        options.extraGUI?.Invoke();
                    }

                    return isMissing;
                }

                #endregion

                #region Parameter conainer

                // Only newer SDKs let the value of a Bool be switched off. Their menu editor has this toggle.
                private static readonly bool hasValueSwitch = ExtendedGetType("ExpressionsControlOptions")
                    ?.GetField("ValueToggle", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public) != null;

                private static readonly GUIContent boolValueContent = new GUIContent("Value",
                    "The value the parameter is set to while the control is active.");

                /// <summary>
                /// Same rules as the official editor: ticked when above 0, stored as 1 or 0.
                /// On SDKs without the switch, Bool values are always 1.
                /// </summary>
                private static void DrawBoolValue(Rect rect, SerializedProperty value)
                {
                    if (!hasValueSwitch)
                    {
                        using (new EditorGUI.DisabledScope(true))
                            EditorGUI.TextField(rect, string.Empty);
                        if (value.floatValue != 1f) value.floatValue = 1f;
                        return;
                    }

                    bool isOn = value.floatValue > 0;
                    bool newValue = EditorGUI.ToggleLeft(rect, boolValueContent, isOn);

                    float newFloat = newValue ? 1f : 0f;
                    if (value.floatValue != newFloat) value.floatValue = newFloat;
                }

                // Looked up by name, so SDK+ still compiles on SDKs that don't have these fields.
                private static readonly FieldInfo bitWidthField = typeof(VRCExpressionParameters.Parameter).GetField("bitWidth");
                private static readonly FieldInfo flippedField = typeof(VRCExpressionParameters.Parameter).GetField("flippedRepresentation");

                /// <summary>The lowest and highest value the parameter can hold.</summary>
                private static void GetParameterRange(VRCExpressionParameters.Parameter parameter, out float min, out float max)
                {
                    int type = (int)parameter.valueType;
                    int bits = bitWidthField == null ? 8 : (byte)bitWidthField.GetValue(parameter);
                    bool flipped = flippedField != null && (bool)flippedField.GetValue(parameter);
                    VRCParamsPlus.GetRange(type, VRCParamsPlus.EffectiveBits(type, bits), VRCParamsPlus.IsSigned(type, flipped), out min, out max);
                }

                /// <summary>A warning for the same cases as the official editor, or null when the value is fine.</summary>
                private static string GetValueWarning(SerializedProperty value, VRCExpressionParameters.Parameter parameter)
                {
                    if (parameter == null) return null;
                    if (parameter.valueType == VRCExpressionParameters.ValueType.Bool && !hasValueSwitch) return null;

                    float current = value.floatValue;
                    if (current == 0)
                    {
                        return parameter.valueType == VRCExpressionParameters.ValueType.Bool
                            ? "The parameter will be false both when this control is activated and when it's deactivated. Value should usually be ticked for Bool parameters."
                            : "The value should usually not be zero, because the parameter is also set to zero when this control is deactivated.";
                    }

                    if (parameter.valueType == VRCExpressionParameters.ValueType.Bool) return null;

                    GetParameterRange(parameter, out float min, out float max);
                    if (current < min) return FormattableString.Invariant($"The value ({current}) is lower than the parameter's minimum of {min}. It will be clamped.");
                    if (current > max) return FormattableString.Invariant($"The value ({current}) is higher than the parameter's maximum of {max}. It will be clamped.");
                    return null;
                }

                static void ParameterContainer(
                    SerializedProperty property,
                    VRCExpressionParameters parameters,
                    Rect rect = default
                )
                {
                    var rectProvided = rect != default;

                    if (property?.FindPropertyRelative("parameter") == null)
                    {
                        if (rectProvided)
                            VRCSDKPlusToolbox.Placeholder.GUI(rect);
                        else
                        {
                            VRCSDKPlusToolbox.Container.BeginLayout();
                            VRCSDKPlusToolbox.Placeholder.GUILayout(18);
                            VRCSDKPlusToolbox.Container.EndLayout();
                        }
                    }
                    else
                    {
                        if (!rectProvided) VRCSDKPlusToolbox.Container.BeginLayout();

                        float CONTENT_VALUE_SELECTOR_WIDTH = 50;
                        Rect selectorRect = default;
                        Rect valueRect = default;

                        if (rectProvided)
                        {
                            selectorRect = new Rect(rect.x, rect.y, rect.width - CONTENT_VALUE_SELECTOR_WIDTH - 3,
                                rect.height);
                            valueRect = new Rect(selectorRect.x + selectorRect.width + 3, rect.y,
                                CONTENT_VALUE_SELECTOR_WIDTH, rect.height);
                        }

                        var parameter = property.FindPropertyRelative("parameter");
                        var value = property.FindPropertyRelative("value");
                        string selectedName = parameter.FindPropertyRelative("name").stringValue;
                        var param = string.IsNullOrEmpty(selectedName) ? null : parameters?.parameters?.FirstOrDefault(p => p.name == selectedName);

                        // Worked out before anything is drawn, so an edit can't change the layout halfway through an event.
                        string valueWarning = GetValueWarning(value, param);

                        var t = (VRCExpressionsMenu.Control.ControlType)property.FindPropertyRelative("type").intValue;
                        bool isRequired = t == VRCExpressionsMenu.Control.ControlType.Button || t == VRCExpressionsMenu.Control.ControlType.Toggle;
                        DrawParameterSelector(rectProvided ? string.Empty : "Parameter", parameter, parameters, new ParameterSelectorOptions()
                        {
                            rect = selectorRect,
                            required = isRequired,
                            extraGUI = () =>
                            {
                                #region Value selector

                                switch (param?.valueType)
                                {
                                    case VRCExpressionParameters.ValueType.Bool:
                                        Rect toggleRect = rectProvided ? valueRect : EditorGUILayout.GetControlRect(false, 18, GUILayout.Width(CONTENT_VALUE_SELECTOR_WIDTH));
                                        DrawBoolValue(toggleRect, value);
                                        break;

                                    default:
                                        bool isInt = param != null && param.valueType == VRCExpressionParameters.ValueType.Int;

                                        // Only written when edited, so viewing a control never changes its value.
                                        EditorGUI.BeginChangeCheck();
                                        float newValue;
                                        if (isInt)
                                            newValue = rectProvided ?
                                                EditorGUI.IntField(valueRect, (int)value.floatValue) :
                                                EditorGUILayout.IntField((int)value.floatValue, GUILayout.Width(CONTENT_VALUE_SELECTOR_WIDTH));
                                        else
                                            newValue = rectProvided ?
                                                EditorGUI.FloatField(valueRect, value.floatValue) :
                                                EditorGUILayout.FloatField(value.floatValue, GUILayout.Width(CONTENT_VALUE_SELECTOR_WIDTH));

                                        if (EditorGUI.EndChangeCheck())
                                        {
                                            float min = -128, max = 255; // Unknown parameter: anything a parameter could hold.
                                            if (param != null) GetParameterRange(param, out min, out max);
                                            value.floatValue = Mathf.Clamp(newValue, min, max);
                                        }
                                        break;
                                }
                                #endregion
                            }
                        });

                        if (valueWarning != null) EditorGUILayout.HelpBox(valueWarning, MessageType.Warning);

                        if (!rectProvided)
                            VRCSDKPlusToolbox.Container.EndLayout();
                    }
                }

                #endregion

                #region Miscellaneous containers

                static void RadialContainer(SerializedProperty property, VRCExpressionParameters parameters)
                {
                    using (new VRCSDKPlusToolbox.Container.Vertical())
                        DrawPuppetParameterSelector(
                            "Rotation",
                            SafeGetSubParameters(property, 1).GetArrayElementAtIndex(0),
                            parameters,
                            new ParameterSelectorOptions(true)
                        );
                }

                static void SubMenuContainer(SerializedProperty property)
                {
                    using (new VRCSDKPlusToolbox.Container.Vertical())
                    {
                        var subMenu = property.FindPropertyRelative("subMenu");
                        var nameProperty = property.FindPropertyRelative("name");
                        bool emptySubmenu = subMenu.objectReferenceValue == null;

                        using (new GUILayout.HorizontalScope())
                        {
                            EditorGUILayout.PropertyField(subMenu);
                            if (emptySubmenu)
                            {
                                using (new EditorGUI.DisabledScope(_currentNode?.Value == null))
                                    if (GUILayout.Button("New", GUILayout.Width(40)))
                                    {
                                        var m = _currentNode.Value;
                                        var path = AssetDatabase.GetAssetPath(m);
                                        if (string.IsNullOrEmpty(path))
                                            path = $"Assets/{m.name}.asset";
                                        var parentPath = Path.GetDirectoryName(path);
                                        var assetName = string.IsNullOrEmpty(nameProperty?.stringValue) ? $"{m.name} SubMenu.asset" : $"{nameProperty.stringValue} Menu.asset";
                                        var newMenuPath = VRCSDKPlusToolbox.ReadyAssetPath(parentPath, assetName, true);

                                        var newMenu = CreateInstance<VRCExpressionsMenu>();
                                        if (newMenu.controls == null)
                                            newMenu.controls = new List<VRCExpressionsMenu.Control>();

                                        AssetDatabase.CreateAsset(newMenu, newMenuPath);
                                        subMenu.objectReferenceValue = newMenu;
                                    }
                                GUILayout.Label(new GUIContent(VRCSDKPlusToolbox.GUIContent.Warn) { tooltip = "Submenu is empty. This control has no use." }, VRCSDKPlusToolbox.Styles.icon);
                            }
                            using (new EditorGUI.DisabledScope(emptySubmenu))
                            {
                                if (ClickableButton(VRCSDKPlusToolbox.GUIContent.Folder, VRCSDKPlusToolbox.Styles.icon))
                                    Selection.activeObject = subMenu.objectReferenceValue;
                                if (ClickableButton(VRCSDKPlusToolbox.GUIContent.Clear, VRCSDKPlusToolbox.Styles.icon))
                                    subMenu.objectReferenceValue = null;
                            }
                        }
                    }
                }

                static void CompactTwoAxisParametersContainer(SerializedProperty property, VRCExpressionParameters parameters)
                {
                    using (new VRCSDKPlusToolbox.Container.Vertical())
                    {
                        using (new GUILayout.HorizontalScope())
                        {
                            using (new GUILayout.HorizontalScope())
                                GUILayout.Label("Axis Parameters", VRCSDKPlusToolbox.Styles.Label.Centered);


                            using (new GUILayout.HorizontalScope())
                            {
                                GUILayout.Label("Name -", VRCSDKPlusToolbox.Styles.Label.Centered);
                                GUILayout.Label("Name +", VRCSDKPlusToolbox.Styles.Label.Centered);
                            }
                        }

                        var subs = SafeGetSubParameters(property, 2);
                        var sub0 = subs.GetArrayElementAtIndex(0);
                        var sub1 = subs.GetArrayElementAtIndex(1);

                        var labels = SafeGetLabels(property);

                        using (new GUILayout.HorizontalScope())
                        {
                            var rect = EditorGUILayout.GetControlRect();
                            using (new GUILayout.HorizontalScope())
                            {
                                DrawPuppetParameterSelector(
                                    "Horizontal",
                                    sub0,
                                    parameters,
                                    new ParameterSelectorOptions(rect, true)
                                );
                            }

                            using (new GUILayout.HorizontalScope())
                            {
                                DrawLabel(labels.GetArrayElementAtIndex(0), "Left");
                                DrawLabel(labels.GetArrayElementAtIndex(1), "Right");
                            }
                        }

                        using (new GUILayout.HorizontalScope())
                        {
                            var rect = EditorGUILayout.GetControlRect();
                            using (new GUILayout.HorizontalScope())
                            {
                                DrawPuppetParameterSelector(
                                    "Vertical",
                                    sub1,
                                    parameters,
                                    new ParameterSelectorOptions(rect, true)
                                );
                            }

                            using (new GUILayout.HorizontalScope())
                            {
                                DrawLabel(labels.GetArrayElementAtIndex(2), "Down");
                                DrawLabel(labels.GetArrayElementAtIndex(3), "Up");
                            }
                        }
                    }

                }
                static void CompactFourAxisParametersContainer(SerializedProperty property, VRCExpressionParameters parameters)
                {
                    using (new VRCSDKPlusToolbox.Container.Vertical())
                    {
                        using (new GUILayout.HorizontalScope())
                        {
                            var headerRect = EditorGUILayout.GetControlRect();
                            var r1 = new Rect(headerRect) { width = headerRect.width / 2 };
                            var r2 = new Rect(r1) { x = r1.x + r1.width };
                            GUI.Label(r1, "Axis Parameters", VRCSDKPlusToolbox.Styles.Label.Centered);
                            GUI.Label(r2, "Name", VRCSDKPlusToolbox.Styles.Label.Centered);
                        }

                        var subs = SafeGetSubParameters(property, 4);
                        var sub0 = subs.GetArrayElementAtIndex(0);
                        var sub1 = subs.GetArrayElementAtIndex(1);
                        var sub2 = subs.GetArrayElementAtIndex(2);
                        var sub3 = subs.GetArrayElementAtIndex(3);

                        var labels = SafeGetLabels(property);

                        using (new GUILayout.HorizontalScope())
                        {
                            var r = EditorGUILayout.GetControlRect();
                            using (new GUILayout.HorizontalScope())
                            {
                                DrawPuppetParameterSelector(
                                    "Up",
                                    sub0,
                                    parameters,
                                    new ParameterSelectorOptions(r, true)
                                );
                            }

                            using (new GUILayout.HorizontalScope())
                                DrawLabel(labels.GetArrayElementAtIndex(0), "Name");
                        }

                        using (new GUILayout.HorizontalScope())
                        {
                            var r = EditorGUILayout.GetControlRect();
                            using (new GUILayout.HorizontalScope())
                            {
                                DrawPuppetParameterSelector(
                                    "Right",
                                    sub1,
                                    parameters,
                                    new ParameterSelectorOptions(r, true)
                                );
                            }

                            using (new GUILayout.HorizontalScope())
                                DrawLabel(labels.GetArrayElementAtIndex(1), "Name");
                        }

                        using (new GUILayout.HorizontalScope())
                        {
                            var r = EditorGUILayout.GetControlRect();
                            using (new GUILayout.HorizontalScope())
                            {
                                DrawPuppetParameterSelector(
                                    "Down",
                                    sub2,
                                    parameters,
                                    new ParameterSelectorOptions(r, true)
                                );
                            }

                            using (new GUILayout.HorizontalScope())
                                DrawLabel(labels.GetArrayElementAtIndex(2), "Name");
                        }

                        using (new GUILayout.HorizontalScope())
                        {
                            var r = EditorGUILayout.GetControlRect();
                            using (new GUILayout.HorizontalScope())
                            {
                                DrawPuppetParameterSelector(
                                    "Left",
                                    sub3,
                                    parameters,
                                    new ParameterSelectorOptions(r, true)
                                );
                            }

                            using (new GUILayout.HorizontalScope())
                                DrawLabel(labels.GetArrayElementAtIndex(3), "Name");
                        }
                    }

                }
                static void TwoAxisParametersContainer(SerializedProperty property, VRCExpressionParameters parameters)
                {
                    VRCSDKPlusToolbox.Container.BeginLayout();

                    GUILayout.Label("Axis Parameters", VRCSDKPlusToolbox.Styles.Label.Centered);

                    var subs = SafeGetSubParameters(property, 2);
                    var sub0 = subs.GetArrayElementAtIndex(0);
                    var sub1 = subs.GetArrayElementAtIndex(1);

                    DrawPuppetParameterSelector(
                        "Horizontal",
                        sub0,
                        parameters,
                        new ParameterSelectorOptions(true)
                    );

                    DrawPuppetParameterSelector(
                        "Vertical",
                        sub1,
                        parameters,
                        new ParameterSelectorOptions(true)
                    );

                    VRCSDKPlusToolbox.Container.EndLayout();
                }

                static void FourAxisParametersContainer(SerializedProperty property, VRCExpressionParameters parameters)
                {
                    VRCSDKPlusToolbox.Container.BeginLayout("Axis Parameters");

                    var subs = SafeGetSubParameters(property, 4);
                    var sub0 = subs.GetArrayElementAtIndex(0);
                    var sub1 = subs.GetArrayElementAtIndex(1);
                    var sub2 = subs.GetArrayElementAtIndex(2);
                    var sub3 = subs.GetArrayElementAtIndex(3);

                    DrawPuppetParameterSelector(
                        "Up",
                        sub0,
                        parameters,
                        new ParameterSelectorOptions(true)
                    );

                    DrawPuppetParameterSelector(
                        "Right",
                        sub1,
                        parameters,
                        new ParameterSelectorOptions(true)
                    );

                    DrawPuppetParameterSelector(
                        "Down",
                        sub2,
                        parameters,
                        new ParameterSelectorOptions(true)
                    );

                    DrawPuppetParameterSelector(
                        "Left",
                        sub3,
                        parameters,
                        new ParameterSelectorOptions(true)
                    );

                    VRCSDKPlusToolbox.Container.EndLayout();
                }

                static void AxisCustomisationContainer(SerializedProperty property)
                {
                    var labels = SafeGetLabels(property);

                    using (new VRCSDKPlusToolbox.Container.Vertical("Customization"))
                    {
                        DrawLabel(labels.GetArrayElementAtIndex(0), "Up");
                        DrawLabel(labels.GetArrayElementAtIndex(1), "Right");
                        DrawLabel(labels.GetArrayElementAtIndex(2), "Down");
                        DrawLabel(labels.GetArrayElementAtIndex(3), "Left");
                    }
                }

                /// <summary>
                /// A puppet control made by a script or another tool can have fewer sub-parameters than its type needs;
                /// reading a missing one returns null and throws inside a layout group. Grow the array to the needed size.
                /// </summary>
                static SerializedProperty SafeGetSubParameters(SerializedProperty property, int count)
                {
                    var subs = property.FindPropertyRelative("subParameters");
                    if (subs.arraySize < count) subs.arraySize = count;
                    return subs;
                }

                static SerializedProperty SafeGetLabels(SerializedProperty property)
                {
                    var labels = property.FindPropertyRelative("labels");

                    labels.arraySize = 4;
                    var l0 = labels.GetArrayElementAtIndex(0);
                    if (l0 == null)
                    {
                        var menu = (VRCExpressionsMenu)labels.serializedObject.targetObject;
                        var index = menu.controls.FindIndex(property.objectReferenceValue);
                        menu.controls[index].labels = new[]
                        {
                            new VRCExpressionsMenu.Control.Label(),
                            new VRCExpressionsMenu.Control.Label(),
                            new VRCExpressionsMenu.Control.Label(),
                            new VRCExpressionsMenu.Control.Label()
                        };
                    }

                    if (labels.GetArrayElementAtIndex(0) == null)
                        Debug.Log("ITEM IS NULL");

                    return labels;
                }

                static void DrawLabel(SerializedProperty property, string type)
                {
                    bool compact = VRCSDKPlusToolbox.Preferences.CompactMode;
                    float imgWidth = compact ? 28 : 58;
                    float imgHeight = compact ? EditorGUIUtility.singleLineHeight : 58;

                    var imgProperty = property.FindPropertyRelative("icon");
                    var nameProperty = property.FindPropertyRelative("name");
                    if (!compact) EditorGUILayout.BeginVertical("helpbox");

                    using (new GUILayout.HorizontalScope())
                    {
                        using (new GUILayout.VerticalScope())
                        {
                            if (!compact)
                                using (new EditorGUI.DisabledScope(true))
                                    EditorGUILayout.LabelField("Axis", type, VRCSDKPlusToolbox.Styles.Label.LabelDropdown);

                            EditorGUILayout.PropertyField(nameProperty, compact ? GUIContent.none : new GUIContent("Name"));
                            var nameRect = GUILayoutUtility.GetLastRect();
                            if (compact && string.IsNullOrEmpty(nameProperty.stringValue)) GUI.Label(nameRect, $"{type}", VRCSDKPlusToolbox.Styles.Label.PlaceHolder);
                        }

                        imgProperty.objectReferenceValue = EditorGUILayout.ObjectField(imgProperty.objectReferenceValue, typeof(Texture2D), false, GUILayout.Width(imgWidth), GUILayout.Height(imgHeight));
                    }

                    if (!compact) EditorGUILayout.EndVertical(); // matches the BeginVertical above

                }

                #endregion
            }

        }
    }
}
