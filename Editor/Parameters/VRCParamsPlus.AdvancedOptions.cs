using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DreadScripts.VRCSDKPlus
{
    internal sealed partial class VRCSDKPlus
    {
        internal sealed partial class VRCParamsPlus : Editor
        {
            #region Advanced Options
            // Hard-coded copies of the SDK's Parameter.MinBitWidth/MaxBitWidth and MinLerpDuration/MaxLerpDuration,
            // so SDK+ still compiles on SDKs that don't have them.
            private const int MinBits = 2;
            private const int MaxBits = 8;
            private const float MinLerp = 0f;
            private const float MaxLerp = 300f;

            private static readonly string[] lerpOptions = { "Default", "Custom" };
            private static readonly string[] representationOptions = { "Unsigned", "Signed" };
            private static readonly string[] bitOptions = { "2", "3", "4", "5", "6", "7", "8" };
            private static readonly int[] bitValues = { 2, 3, 4, 5, 6, 7, 8 };
            private static readonly string[] boolBitOptions = { "1" };

            private const string LerpTooltip = "Default or custom lerp duration for this parameter. Bools never lerp.";
            private const string BitsTooltip = "How many bits this parameter uses when synced.";
            private const string IntSignTooltip = "Whether this Int can sync negative values.\n\nUnsigned: Starts at 0.\nSigned: Half of the values are negative, so the highest value is lower.";
            private const string FloatSignTooltip = "Whether this Float can sync negative values.\n\nUnsigned: From 0 to 1, in smaller steps.\nSigned: From -1 to 1.";
            private const string BoolSignTooltip = "Bools are always unsigned.";
            private const string IntRangeTooltip = "The lowest and highest value this parameter can sync with the chosen bits and sign.";
            private const string FloatRangeTooltip = IntRangeTooltip + "\n\nStep is the smallest difference between two synced values. More bits make it smaller.";
            private const string BoolRangeTooltip = "Bools always use 1 bit and are either True or False.";

            /// <summary>
            /// A parameter added to an empty list has every field zeroed, including bitWidth = 0, which the SDK clamps to
            /// 2 bits. Give it the width a new parameter should have (8, or 1 for Bool), as the official editor does.
            /// </summary>
            private static void FixNewParameterBitWidth(SerializedProperty parameter)
            {
                if (!hasAdvancedOptions || parameter == null) return;
                SerializedProperty bitWidth = parameter.FindPropertyRelative("bitWidth");
                SerializedProperty valueType = parameter.FindPropertyRelative("valueType");
                if (bitWidth == null || valueType == null) return;

                if (valueType.enumValueIndex == 2) bitWidth.intValue = 1;
                else if (bitWidth.intValue < MinBits || bitWidth.intValue > MaxBits) bitWidth.intValue = MaxBits;
            }

            private float GetElementHeight(int index)
            {
                if (groupDragActive && groupDragRows.Contains(index))
                    return index == groupDragRow ? groupDragRows.Sum(GetRowHeight) : 0;
                return GetRowHeight(index);
            }

            private float GetRowHeight(int index)
            {
                float height = parametersOrderList.elementHeight;
                if (!hasAdvancedOptions || index < 0 || index >= parameterList.arraySize) return height;
                return parameterList.GetArrayElementAtIndex(index).isExpanded ? height * 2 : height;
            }

            private static void DrawAdvancedLine(Rect rect, SerializedProperty valueType, SerializedProperty defaultValue, SerializedProperty bitWidth, SerializedProperty flippedRep, SerializedProperty useLerp, SerializedProperty lerpDuration)
            {
                int typeIndex = valueType.enumValueIndex;
                bool isBool = typeIndex == 2;
                int bits = EffectiveBits(typeIndex, bitWidth.intValue);
                bool signed = IsSigned(typeIndex, flippedRep.boolValue);
                const float gap = 10;

                Rect Take(float width)
                {
                    Rect r = new Rect(rect.x, rect.y, Mathf.Max(0, Mathf.Min(width, rect.width)), rect.height);
                    rect.x += width;
                    rect.width -= width;
                    return r;
                }

                using (new EditorGUI.DisabledScope(isBool))
                {
                    GUI.Label(Take(32), new GUIContent("Lerp", LerpTooltip));
                    Rect lerpRect = Take(70);
                    EditorGUI.BeginChangeCheck();
                    int lerpIndex = EditorGUI.Popup(lerpRect, useLerp.boolValue ? 1 : 0, lerpOptions);
                    if (EditorGUI.EndChangeCheck()) useLerp.boolValue = lerpIndex == 1;
                    DrawTooltip(lerpRect, LerpTooltip);

                    if (useLerp.boolValue)
                    {
                        Take(2);
                        EditorGUI.BeginChangeCheck();
                        float seconds = EditorGUI.FloatField(Take(40), lerpDuration.floatValue);
                        if (EditorGUI.EndChangeCheck()) lerpDuration.floatValue = Mathf.Clamp(seconds, MinLerp, MaxLerp);
                        GUI.Label(Take(12), "s");
                    }

                    Take(gap);

                    GUI.Label(Take(30), new GUIContent("Bits", BitsTooltip));
                    Rect bitsRect = Take(40);
                    if (isBool)
                        EditorGUI.Popup(bitsRect, 0, boolBitOptions);
                    else
                    {
                        EditorGUI.BeginChangeCheck();
                        int newBits = EditorGUI.IntPopup(bitsRect, bits, bitOptions, bitValues);
                        if (EditorGUI.EndChangeCheck())
                        {
                            bitWidth.intValue = newBits;
                            ClampDefaultValue(valueType, defaultValue, newBits, signed);
                        }
                    }
                    DrawTooltip(bitsRect, BitsTooltip);

                    Take(gap);

                    Rect signRect = Take(80);
                    EditorGUI.BeginChangeCheck();
                    int repIndex = EditorGUI.Popup(signRect, signed ? 1 : 0, representationOptions);
                    if (EditorGUI.EndChangeCheck() && !isBool)
                    {
                        bool newSigned = repIndex == 1;
                        // Float defaults to Signed, Int to Unsigned. flippedRepresentation inverts that default.
                        flippedRep.boolValue = typeIndex == 1 ? !newSigned : newSigned;
                        ClampDefaultValue(valueType, defaultValue, EffectiveBits(typeIndex, bitWidth.intValue), newSigned);
                    }
                    DrawTooltip(signRect, isBool ? BoolSignTooltip : typeIndex == 1 ? FloatSignTooltip : IntSignTooltip);

                    Take(gap);
                }

                string rangeLabel = GetRangeLabel(typeIndex, EffectiveBits(typeIndex, bitWidth.intValue), IsSigned(typeIndex, flippedRep.boolValue));
                string rangeTooltip = isBool ? BoolRangeTooltip : typeIndex == 1 ? FloatRangeTooltip : IntRangeTooltip;
                GUI.Label(rect, new GUIContent(rangeLabel, rangeTooltip), EditorStyles.miniLabel);
            }

            /// <summary>Dropdowns without a label can't have a tooltip of their own, so an empty label is drawn over them.</summary>
            private static void DrawTooltip(Rect rect, string tooltip) => GUI.Label(rect, new GUIContent(string.Empty, tooltip));

            private static void ClampDefaultValue(SerializedProperty valueType, SerializedProperty defaultValue, int bits, bool signed)
            {
                int typeIndex = valueType.enumValueIndex;
                if (typeIndex == 2) return;
                GetRange(typeIndex, bits, signed, out float min, out float max);
                defaultValue.floatValue = Mathf.Clamp(defaultValue.floatValue, min, max);
            }

            internal static bool IsSigned(int valueType, bool flipped)
            {
                switch (valueType)
                {
                    case 1: return !flipped; // Float
                    case 0: return flipped;  // Int
                    default: return false;   // Bool
                }
            }

            internal static int EffectiveBits(int valueType, int storedBits) => valueType == 2 ? 1 : Mathf.Clamp(storedBits, MinBits, MaxBits);

            internal static void GetRange(int valueType, int bits, bool signed, out float min, out float max)
            {
                switch (valueType)
                {
                    case 1: // Float
                        min = signed ? -1f : 0f;
                        max = 1f;
                        break;
                    case 0: // Int
                        min = signed ? -(1 << (bits - 1)) : 0;
                        max = signed ? (1 << (bits - 1)) - 1 : (1 << bits) - 1;
                        break;
                    default: // Bool
                        min = 0f;
                        max = 1f;
                        break;
                }
            }

            private static bool HasNonDefaultAdvanced(int valueType, int bits, bool flipped, bool useLerp) =>
                valueType != 2 && (bits != MaxBits || flipped || useLerp);

            private static string GetAdvancedSummary(int valueType, int bits, bool flipped, bool useLerp, float lerp)
            {
                string summary = $"{bits} bits · {(IsSigned(valueType, flipped) ? "Signed" : "Unsigned")}";
                // Invariant formatting so numbers match Unity's own fields ("0.5", not "0,5").
                if (useLerp) summary += FormattableString.Invariant($" · {lerp:0.##} s lerp");
                return summary;
            }

            private static string GetRangeLabel(int valueType, int bits, bool signed)
            {
                if (valueType == 2) return "1 bit · True or False";
                GetRange(valueType, bits, signed, out float min, out float max);
                if (valueType == 0) return FormattableString.Invariant($"Range [{min},{max}]");
                float step = (max - min) / ((1 << bits) - 2);
                return FormattableString.Invariant($"Range [{min},{max}] · step {step:G4}");
            }
            #endregion
        }
    }
}
