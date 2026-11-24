using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace DreadScripts.VRCSDKPlus
{
    internal sealed partial class VRCSDKPlus
    {
        internal sealed partial class VRCParamsPlus : Editor
        {
            // Case-insensitive ordinal (A, a, B, b... rather than A, B, a, b), with plain ordinal as the tie-break so names
            // that differ only in case still sort the same way every time (uppercase first). Ordinal comparisons don't depend on the
            // system language, so the order is the same on every machine.
            private static readonly IComparer<string> nameComparer = Comparer<string>.Create((x, y) =>
            {
                int ignoringCase = string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
                return ignoringCase != 0 ? ignoringCase : string.CompareOrdinal(x, y);
            });

            /// <summary>Sort A-Z / Z-A by case-insensitive ordinal name order. LINQ ordering is stable, so equal names keep their order.</summary>
            private static List<int> GetSortedOrder(List<string> names, bool descending)
            {
                IEnumerable<int> indices = Enumerable.Range(0, names.Count);
                return (descending ? indices.OrderByDescending(i => names[i], nameComparer) : indices.OrderBy(i => names[i], nameComparer)).ToList();
            }

            /// <summary>Parameter Organizer's Group by Path, with ordinal name order inside folder groups.</summary>
            private static List<int> GetGroupedByPathOrder(List<string> names)
            {
                string RootFolder(string name)
                {
                    int firstSlash = name.IndexOf('/');
                    // Names without a slash are their own root, so they don't get clustered.
                    return firstSlash >= 0 ? name.Substring(0, firstSlash) : name;
                }

                List<int> grouped = new List<int>();
                HashSet<string> processedRoots = new HashSet<string>();
                for (int i = 0; i < names.Count; i++)
                {
                    string root = RootFolder(names[i]);
                    if (!processedRoots.Add(root)) continue;

                    List<int> group = Enumerable.Range(0, names.Count).Where(x => RootFolder(names[x]) == root).ToList();
                    // Folder groups are sorted by name inside the group.
                    if (group.Any(x => names[x].Contains("/")))
                        group = group.OrderBy(x => names[x], nameComparer).ToList();
                    grouped.AddRange(group);
                }
                return grouped;
            }

            private List<string> GetParameterNames()
            {
                List<string> names = new List<string>(parameterList.arraySize);
                for (int i = 0; i < parameterList.arraySize; i++)
                    names.Add(parameterList.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue ?? string.Empty);
                return names;
            }

            private void SortParameters(List<int> order)
            {
                bool unchanged = true;
                for (int i = 0; i < order.Count; i++)
                    if (order[i] != i) { unchanged = false; break; }
                if (unchanged) return;

                Undo.SetCurrentGroupName("Sort Parameters");
                ApplyOrder(order);
                serializedObject.ApplyModifiedProperties();
                parametersOrderList.ClearSelection();
                RefreshAllParameterStatus();
            }
        }
    }
}
