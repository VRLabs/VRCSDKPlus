using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DreadScripts.VRCSDKPlus
{
    internal static partial class VRCSDKPlusToolbox
    {
        internal abstract class CustomDropdownBase : PopupWindowContent
        {
            internal static readonly GUIStyle backgroundStyle = new GUIStyle()
            {
                // Own textures: the shared GetColorTexture one is recoloured whenever something else draws with it.
                hover = { background = VRCSDKPlusToolbox.CreateColorTexture(new Color(0.3020f, 0.3020f, 0.3020f)) },
                active = { background = VRCSDKPlusToolbox.CreateColorTexture(new Color(0.1725f, 0.3647f, 0.5294f)) }
            };

            internal static readonly GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };

        }
        internal class CustomDropdown<T> : CustomDropdownBase
        {

            private readonly string title;
            private string search;
            internal DropDownItem[] items;
            private readonly Action<DropDownItem> itemGUI;
            private readonly Action<int, T> onSelected;
            private Func<T, string, bool> onSearchChanged;

            private bool hasSearch;
            private float width;
            private bool firstPass = true;
            private Vector2 scroll;
            private readonly Rect[] selectionRects;

            public CustomDropdown(string title, IEnumerable<T> itemArray, Action<DropDownItem> itemGUI, Action<int, T> onSelected)
            {
                this.title = title;
                this.onSelected = onSelected;
                this.itemGUI = itemGUI;
                items = itemArray.Select((item, i) => new DropDownItem(item, i)).ToArray();
                selectionRects = new Rect[items.Length];
            }

            public void EnableSearch(Func<T, string, bool> onSearchChanged)
            {
                hasSearch = true;
                this.onSearchChanged = onSearchChanged;
            }

            public void OrderBy(Func<T, object> orderFunc)
            {
                items = orderFunc != null ? items.OrderBy(item => orderFunc(item.value)).ToArray() : items;
            }

            public void SetExtraOptions(Func<T, object[]> argReturn)
            {
                foreach (var i in items)
                    i.args = argReturn(i.value);
            }

            public override void OnGUI(Rect rect)
            {

                using (new GUILayout.AreaScope(rect))
                {
                    var e = Event.current;
                    scroll = GUILayout.BeginScrollView(scroll);
                    if (!string.IsNullOrEmpty(title))
                    {
                        GUILayout.Label(title, titleStyle);
                        DrawSeparator();
                    }
                    if (hasSearch)
                    {
                        EditorGUI.BeginChangeCheck();
                        if (firstPass) GUI.SetNextControlName($"{title}SearchBar");
                        search = EditorGUILayout.TextField(search, GUI.skin.GetStyle("SearchTextField"));
                        if (EditorGUI.EndChangeCheck())
                        {
                            foreach (var i in items)
                                i.displayed = onSearchChanged(i.value, search);
                        }
                    }

                    var t = e.type;
                    for (int i = 0; i < items.Length; i++)
                    {
                        var item = items[i];
                        if (!item.displayed) continue;
                        if (!firstPass)
                        {
                            if (GUI.Button(selectionRects[i], string.Empty, backgroundStyle))
                            {
                                onSelected(item.itemIndex, item.value);
                                editorWindow.Close();
                            }
                        }
                        using (new GUILayout.VerticalScope()) itemGUI(item);

                        if (t == EventType.Repaint)
                        {
                            selectionRects[i] = GUILayoutUtility.GetLastRect();

                            if (firstPass && selectionRects[i].width > width)
                                width = selectionRects[i].width;
                        }
                    }

                    if (t == EventType.Repaint && firstPass)
                    {
                        firstPass = false;
                        GUI.FocusControl($"{title}SearchBar");
                    }
                    GUILayout.EndScrollView();
                    if (rect.Contains(e.mousePosition))
                        editorWindow.Repaint();
                }
            }

            public override Vector2 GetWindowSize()
            {
                Vector2 ogSize = base.GetWindowSize();
                if (!firstPass) ogSize.x = width + 21;
                return ogSize;
            }

            public void Show(Rect position) => PopupWindow.Show(position, this);
            internal class DropDownItem
            {
                internal readonly int itemIndex;
                internal readonly T value;

                internal object[] args;
                internal bool displayed = true;

                internal object extra
                {
                    get => args[0];
                    set => args[0] = value;
                }

                internal DropDownItem(T value, int itemIndex)
                {
                    this.value = value;
                    this.itemIndex = itemIndex;
                }

                public static implicit operator T(DropDownItem i) => i.value;
            }

            private static void DrawSeparator(int thickness = 2, int padding = 10)
            {
                Rect r = EditorGUILayout.GetControlRect(GUILayout.Height(thickness + padding));
                r.height = thickness;
                r.y += padding / 2f;
                r.x -= 2;
                r.width += 6;
                ColorUtility.TryParseHtmlString(EditorGUIUtility.isProSkin ? "#595959" : "#858585", out Color lineColor);
                EditorGUI.DrawRect(r, lineColor);
            }

        }

    }
}
