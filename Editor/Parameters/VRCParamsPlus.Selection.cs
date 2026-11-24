using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace DreadScripts.VRCSDKPlus
{
    internal sealed partial class VRCSDKPlus
    {
        internal sealed partial class VRCParamsPlus : Editor
        {
            private List<int> selectionBeforeClick = new List<int>();

            // Group drag: while several selected rows are dragged, the grabbed row (groupDragRow) is drawn
            // with the height of the whole group and holds all of them. The other selected rows shrink to 0.
            private List<int> pendingGroupRows;
            private int pendingGroupRow = -1;
            private List<int> groupDragRows = new List<int>();
            private int groupDragRow = -1;
            private bool groupDragActive;

            // Set when a mouse-down lands on a row's expand arrow, so the selection can be put back afterwards.
            private bool foldoutClicked;
            // Same for the controls on a row's expanded line (Lerp, lerp seconds, Bits, Signed/Unsigned).
            private bool advancedLineClicked;

            private static readonly MethodInfo invalidateListCache = typeof(ReorderableList).GetMethod("InvalidateCache", BindingFlags.NonPublic | BindingFlags.Instance);
            private static readonly FieldInfo listDragOffset = typeof(ReorderableList).GetField("m_DragOffset", BindingFlags.NonPublic | BindingFlags.Instance);

            /// <summary>The whole selection if it has several rows and includes <paramref name="index"/>, otherwise just that row.</summary>
            private List<int> GetTargetIndices(int index)
            {
                List<int> selected = parametersOrderList.selectedIndices.Where(i => i >= 0 && i < parameterList.arraySize).Distinct().OrderBy(i => i).ToList();
                return selected.Count > 1 && selected.Contains(index) ? selected : new List<int> { index };
            }

            /// <summary>The selected rows, valid and in ascending order. Empty when nothing is selected.</summary>
            private List<int> GetSelectedIndices() =>
                parametersOrderList.selectedIndices.Where(i => i >= 0 && i < parameterList.arraySize).Distinct().OrderBy(i => i).ToList();

            // Expanded state is stored per position, so inserting or deleting rows has to carry it along by hand.
            private List<bool> GetExpandedStates()
            {
                List<bool> states = new List<bool>(parameterList.arraySize);
                for (int i = 0; i < parameterList.arraySize; i++)
                    states.Add(parameterList.GetArrayElementAtIndex(i).isExpanded);
                return states;
            }

            private void SetExpandedStates(List<bool> states)
            {
                for (int i = 0; i < parameterList.arraySize && i < states.Count; i++)
                    parameterList.GetArrayElementAtIndex(i).isExpanded = states[i];
            }

            private void DuplicateParameters(List<int> indices)
            {
                List<bool> expanded = GetExpandedStates();
                // Highest first, so the lower indices stay valid while inserting.
                foreach (int i in indices.OrderByDescending(x => x))
                {
                    parameterList.InsertArrayElementAtIndex(i);
                    MakeParameterUnique(i + 1);
                    expanded.Insert(i + 1, expanded[i]); // the copy opens like its original
                }
                SetExpandedStates(expanded);
                parameterList.serializedObject.ApplyModifiedProperties();

                if (indices.Count > 1)
                {
                    // Select the copies: the copy of the k-th row (ascending) ends up at its index + k + 1.
                    List<int> sorted = indices.OrderBy(i => i).ToList();
                    parametersOrderList.ClearSelection();
                    for (int k = 0; k < sorted.Count; k++)
                        parametersOrderList.Select(sorted[k] + k + 1, true);
                }
                RefreshAllParameterStatus();
            }

            private void DeleteParameter(int index) => DeleteParameters(new List<int> { index });

            private void DeleteParameters(List<int> indices)
            {
                List<int> sorted = indices.Where(i => i >= 0 && i < parameterList.arraySize).Distinct().OrderBy(i => i).ToList();
                if (sorted.Count == 0) return;

                List<bool> expanded = GetExpandedStates();
                for (int k = sorted.Count - 1; k >= 0; k--)
                {
                    parameterList.DeleteArrayElementAtIndex(sorted[k]);
                    expanded.RemoveAt(sorted[k]);
                }
                SetExpandedStates(expanded);
                UpdateEmptyFlag();
                parameterList.serializedObject.ApplyModifiedProperties();

                // Select the row that took the first deleted row's place, so the selection never ends up
                // empty while the list keeps focus (the list would then report the last row as its index).
                parametersOrderList.ClearSelection();
                if (parameterList.arraySize > 0)
                    parametersOrderList.Select(Mathf.Min(sorted[0], parameterList.arraySize - 1));
                RefreshAllParameterStatus();
            }

            /// <summary>
            /// When a control inside a row uses the click, the list selects only that row.
            /// Clicking an expand arrow or a control on the expanded line shouldn't change the selection, so put it back.
            /// For the arrow, keyboard focus also returns to the list (Ctrl+D, Delete and the arrow keys keep working
            /// on the selection); expanded-line controls keep focus so the lerp seconds field can still be typed in.
            /// </summary>
            private void RestoreSelection(bool focusList)
            {
                parametersOrderList.ClearSelection();
                foreach (int i in selectionBeforeClick.Where(i => i >= 0 && i < parameterList.arraySize))
                    parametersOrderList.Select(i, true);
                if (focusList) parametersOrderList.GrabKeyboardFocus();
            }

            /// <summary>Called after the list handled the event: arms a group drag on mouse-down, ends it on mouse-up or cancel.</summary>
            private void HandleGroupDragState(EventType eventType, bool selectModifier)
            {
                if (eventType == EventType.MouseDown)
                {
                    // Pressed (without Ctrl/Shift) on a row that was part of a multi-selection: a drag may follow.
                    int pressed = parametersOrderList.index;
                    bool onSelectedRow = !selectModifier && !foldoutClicked && !advancedLineClicked && selectionBeforeClick.Count > 1 && selectionBeforeClick.Contains(pressed)
                                         && parametersOrderList.HasKeyboardControl() && GUIUtility.hotControl != 0;
                    pendingGroupRows = onSelectedRow ? selectionBeforeClick.Where(i => i >= 0 && i < parameterList.arraySize).Distinct().OrderBy(i => i).ToList() : null;
                    pendingGroupRow = onSelectedRow ? pressed : -1;
                    return;
                }

                bool released = eventType == EventType.MouseUp;
                bool cancelled = groupDragActive && GUIUtility.hotControl == 0; // e.g. Escape
                if (released || cancelled) EndGroupDrag();
            }

            private void BeginGroupDrag()
            {
                if (groupDragActive || pendingGroupRows == null || pendingGroupRows.Count < 2) return;

                groupDragRows = pendingGroupRows;
                groupDragRow = pendingGroupRow;
                groupDragActive = true;
                pendingGroupRows = null;

                // The list draws the grabbed element at (mouse - drag offset). The stack starts with the selected
                // rows above the grabbed one, so grow the offset by their height to keep the grabbed row under the cursor.
                if (listDragOffset != null)
                {
                    float above = groupDragRows.Where(i => i < groupDragRow).Sum(GetRowHeight);
                    listDragOffset.SetValue(parametersOrderList, (float)listDragOffset.GetValue(parametersOrderList) + above);
                }
                invalidateListCache?.Invoke(parametersOrderList, null);
            }

            private void EndGroupDrag()
            {
                pendingGroupRows = null;
                pendingGroupRow = -1;
                if (!groupDragActive) return;

                groupDragActive = false;
                // Dropped in place: there was no reorder, so the list still has only the grabbed row selected.
                if (selectionBeforeClick.Count > 1)
                {
                    parametersOrderList.ClearSelection();
                    foreach (int i in groupDragRows) parametersOrderList.Select(i, true);
                }
                selectionBeforeClick.Clear();
                groupDragRows = new List<int>();
                groupDragRow = -1;
                invalidateListCache?.Invoke(parametersOrderList, null);
                Repaint();
            }

            /// <summary>
            /// The list only drags one row. If that row was part of a multi-selection,
            /// move the rest of the selection next to it, in their original order.
            /// </summary>
            private void MoveSelectionWithDraggedRow(int oldIndex, int newIndex)
            {
                int count = parameterList.arraySize;
                // Only a drag that was started as a group drag moves the group (not, e.g., a Ctrl/Shift press then drag).
                List<int> group = groupDragActive ? groupDragRows.Where(i => i >= 0 && i < count).Distinct().OrderBy(i => i).ToList() : new List<int>();
                selectionBeforeClick.Clear();
                if (group.Count < 2 || !group.Contains(oldIndex)) return;

                // Order after the list's own move, as original indices.
                List<int> current = Enumerable.Range(0, count).ToList();
                current.RemoveAt(oldIndex);
                current.Insert(newIndex, oldIndex);

                // Put the group where the dragged row landed, among the rows that weren't selected.
                HashSet<int> inGroup = new HashSet<int>(group);
                List<int> target = current.Where(i => !inGroup.Contains(i)).ToList();
                int insertAt = current.Take(newIndex).Count(i => !inGroup.Contains(i));
                target.InsertRange(insertAt, group);

                // "target" holds original indices; ApplyOrder wants positions in the array as it is now.
                ApplyOrder(target.Select(i => current.IndexOf(i)).ToList());

                int start = target.IndexOf(group[0]);
                parametersOrderList.ClearSelection();
                parametersOrderList.SelectRange(start, start + group.Count - 1);
            }

            /// <summary>
            /// Reorders the parameter list so that position <c>pos</c> holds the element currently at <c>order[pos]</c>.
            /// Expanded state is stored per position, so it is carried along by hand. Does not touch the selection.
            /// </summary>
            private void ApplyOrder(List<int> order)
            {
                int count = parameterList.arraySize;
                bool[] expanded = new bool[count];
                for (int pos = 0; pos < count; pos++)
                    expanded[pos] = parameterList.GetArrayElementAtIndex(pos).isExpanded;

                List<int> working = Enumerable.Range(0, count).ToList();
                for (int pos = 0; pos < count; pos++)
                {
                    int from = working.IndexOf(order[pos]);
                    if (from == pos) continue;
                    parameterList.MoveArrayElement(from, pos);
                    int item = working[from];
                    working.RemoveAt(from);
                    working.Insert(pos, item);
                }

                for (int pos = 0; pos < count; pos++)
                    parameterList.GetArrayElementAtIndex(pos).isExpanded = expanded[order[pos]];
            }

        }
    }
}
