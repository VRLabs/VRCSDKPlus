using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using AnimatorController = UnityEditor.Animations.AnimatorController;
using AnimatorControllerParameter = UnityEngine.AnimatorControllerParameter;
using AnimatorControllerParameterType = UnityEngine.AnimatorControllerParameterType;

namespace DreadScripts.VRCSDKPlus
{
    internal sealed partial class VRCSDKPlus
    {
        internal sealed partial class VRCParamsPlus : Editor
        {
            #region Parameter Picker
            /// <summary>Shows the avatar's controller parameters that aren't in the list yet.</summary>
            private void ShowParameterPicker(Rect position)
            {
                var filteredParameters = validParameters.Where(conParam => !parameterList.IterateArray((_, prop) => prop.FindPropertyRelative("name").stringValue == conParam.name)).ToArray();
                if (filteredParameters.Length == 0)
                {
                    GreenLog("All of the parameters in the avatar's controllers are already in the list.");
                    return;
                }

                var dropdown = new VRCSDKPlusToolbox.CustomDropdown<AnimatorControllerParameter>(null, filteredParameters, item =>
                {
                    using (new GUILayout.HorizontalScope())
                    {
                        GUILayout.Label(item.value.name);
                        GUILayout.Label(item.value.type.ToString(), VRCSDKPlusToolbox.Styles.Label.TypeLabel, GUILayout.ExpandWidth(false));
                    }
                }, (_, conParam) => AddParameterFromController(conParam));
                dropdown.EnableSearch((conParameter, search) => MatchesSearch(conParameter.name, search));
                dropdown.Show(position);
            }

            /// <summary>The list's own footer, with a second button that adds a parameter from the avatar's controllers.</summary>
            private void DrawFooter(Rect rect)
            {
                // Same sizes as ReorderableList's footer.
                const float buttonWidth = 25;
                const float width = 8 + buttonWidth * 2;
                Rect background = new Rect(rect.xMax - 10 - width, rect.y, width, rect.height);
                Rect pickRect = new Rect(background.x + 4, rect.y, buttonWidth, 16);
                Rect addRect = new Rect(pickRect.xMax, rect.y, buttonWidth, 16);

                ReorderableList.Defaults defaults = ReorderableList.defaultBehaviours;
                if (Event.current.type == EventType.Repaint)
                    defaults.footerBackground.Draw(background, false, false, false, false);

                using (new EditorGUI.DisabledScope(validParameters.Length == 0))
                {
                    w_MakeRectLinkCursor(pickRect);
                    if (GUI.Button(pickRect, VRCSDKPlusToolbox.GUIContent.PickParameter, defaults.preButton))
                        ShowParameterPicker(pickRect);
                }
                DrawTooltip(pickRect, "Add a parameter from the avatar's controllers");

                w_MakeRectLinkCursor(addRect);
                if (GUI.Button(addRect, defaults.iconToolbarPlus, defaults.preButton))
                    AddNewParameter();
            }

            private void AddNewParameter()
            {
                int newIndex = parameterList.arraySize;
                parameterList.InsertArrayElementAtIndex(newIndex);
                MakeParameterUnique(newIndex);
                SerializedProperty parameter = parameterList.GetArrayElementAtIndex(newIndex);
                parameter.isExpanded = false;
                ResetAdvanced(parameter);
                UpdateEmptyFlag();
                // The row heights are cached.
                invalidateListCache?.Invoke(parametersOrderList, null);
            }

            private void AddParameterFromController(AnimatorControllerParameter conParam)
            {
                // Called by the dropdown, outside of OnInspectorGUI.
                if (this == null || target == null) return;
                serializedObject.Update();

                int newIndex = parameterList.arraySize;
                parameterList.InsertArrayElementAtIndex(newIndex);
                SerializedProperty parameter = parameterList.GetArrayElementAtIndex(newIndex);
                SerializedProperty valueType = parameter.FindPropertyRelative("valueType");
                SerializedProperty defaultValue = parameter.FindPropertyRelative("defaultValue");

                // The new element is a copy of the last one, so every field has to be set.
                parameter.isExpanded = false;
                parameter.FindPropertyRelative("name").stringValue = conParam.name;
                parameter.FindPropertyRelative("saved").boolValue = true;
                if (hasSyncingOption) parameter.FindPropertyRelative("networkSynced").boolValue = true;

                switch (conParam.type)
                {
                    case AnimatorControllerParameterType.Int:
                        valueType.enumValueIndex = 0;
                        defaultValue.floatValue = conParam.defaultInt;
                        break;
                    case AnimatorControllerParameterType.Float:
                        valueType.enumValueIndex = 1;
                        defaultValue.floatValue = conParam.defaultFloat;
                        break;
                    default: // Bool, Trigger
                        valueType.enumValueIndex = 2;
                        defaultValue.floatValue = conParam.defaultBool ? 1 : 0;
                        break;
                }

                ResetAdvanced(parameter);
                ClampDefaultValue(valueType, defaultValue, MaxBits, IsSigned(valueType.enumValueIndex, false));

                UpdateEmptyFlag();
                serializedObject.ApplyModifiedProperties();
                invalidateListCache?.Invoke(parametersOrderList, null);
                RefreshAllParameterStatus();
                Repaint();
            }
            #endregion

            #region Add to Animator
            private void ShowAddToAnimatorMenu(Rect position, string parameterName, int typeIndex, float defaultValue)
            {
                GenericMenu menu = new GenericMenu();
                // Only a heading.
                menu.AddDisabledItem(new GUIContent("Add to..."));
                menu.AddSeparator(string.Empty);
                for (int i = 0; i < validPlayables.Length; i++)
                {
                    var playable = (VRCAvatarDescriptor.AnimLayerType) validPlayableIndexes[i];
                    menu.AddItem(new GUIContent(validPlayables[i]), false, () => AddToAnimator(playable, parameterName, typeIndex, defaultValue));
                }
                menu.DropDown(position);
            }

            private void AddToAnimator(VRCAvatarDescriptor.AnimLayerType playable, string parameterName, int typeIndex, float defaultValue)
            {
                // Called by the menu, outside of OnInspectorGUI.
                if (this == null || target == null || !avatar) return;
                if (!avatar.GetPlayableLayer(playable, out AnimatorController c)) return;

                if (c.parameters.All(p => p.name != parameterName))
                {
                    AnimatorControllerParameterType paramType;
                    switch (typeIndex)
                    {
                        case 0:
                            paramType = AnimatorControllerParameterType.Int;
                            break;
                        case 1:
                            paramType = AnimatorControllerParameterType.Float;
                            break;
                        default:
                            paramType = AnimatorControllerParameterType.Bool;
                            break;
                    }

                    c.AddParameter(new AnimatorControllerParameter()
                    {
                        name = parameterName,
                        type = paramType,
                        defaultFloat = defaultValue,
                        defaultInt = (int) defaultValue,
                        defaultBool = defaultValue > 0
                    });

                    GreenLog($"Added {paramType} {parameterName} to {playable} Playable Controller");
                }

                RefreshValidParameters();
                RefreshAllParameterStatus();
                Repaint();
            }
            #endregion
        }
    }
}
