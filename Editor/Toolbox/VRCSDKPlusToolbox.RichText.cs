using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DreadScripts.VRCSDKPlus
{
    internal static partial class VRCSDKPlusToolbox
    {
        /// <summary>
        /// VRChat draws control names with TextMeshPro, where tags don't have to be closed or nested properly.
        /// Unity's inspector only formats text with the few tags it knows, and only when all of them are.
        /// </summary>
        public static class RichText
        {
            // Every other text between < and > isn't a tag, so it's shown as it is.
            private static readonly HashSet<string> knownTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                // Shown
                "b", "i", "color", "alpha", "font-weight", "lowercase", "uppercase", "allcaps", "smallcaps", "noparse", "br", "cr",
                // Not shown: position and size
                "size", "pos", "space", "hoffset", "voffset", "line-height", "indent", "line-indent", "margin", "margin-left", "margin-right",
                "align", "width", "cspace", "mspace", "monospace", "rotate", "sup", "sub", "page", "nobr",
                // Not shown: not possible in the inspector
                "u", "s", "mark", "font", "gradient", "material", "sprite", "quad", "style", "link", "a", "class", "action",
                "scale", "table", "tr", "th", "td", "zwsp", "zwj", "nbsp", "shy"
            };

            private static readonly Regex tagRegex = new Regex(@"<(?<close>/?)(?<name>#[0-9a-f]+|[a-z][a-z-]*)(?:\s*=\s*(?<value>[^<>]*)|\s+[^<>]*)?\s*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            /// <summary>The first color in a name, from a color tag or its short form (&lt;#FF0000&gt;).</summary>
            public static bool TryGetColor(string text, out Color color)
            {
                foreach (Match match in tagRegex.Matches(text ?? string.Empty))
                    if (match.Groups["close"].Length == 0 && TryGetTagColor(match, out color))
                        return true;

                color = Color.white;
                return false;
            }

            private static bool TryGetTagColor(Match match, out Color color)
            {
                string name = match.Groups["name"].Value;
                string value = name[0] == '#' ? name : string.Equals(name, "color", StringComparison.OrdinalIgnoreCase) ? TrimValue(match) : string.Empty;
                color = Color.white;
                return value.Length > 0 && ColorUtility.TryParseHtmlString(value, out color);
            }

            private static string TrimValue(Match match) => match.Groups["value"].Value.Trim().Trim('"');

            /// <summary>Whether a name has the opening tag, closed or not.</summary>
            public static bool HasTag(string text, char tag) => Regex.IsMatch(text ?? string.Empty, $"<{tag}>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            /// <summary>
            /// Converts a name to properly closed tags that the inspector can show. Only weight, italics, color and
            /// letter case are kept. All other tags are left out.
            /// </summary>
            public static string ToInspector(string text, Color defaultColor)
            {
                if (string.IsNullOrEmpty(text)) return string.Empty;

                StringBuilder output = new StringBuilder();
                Stack<Color> colors = new Stack<Color>();
                float? alpha = null;
                int bold = 0, italic = 0, upper = 0, lower = 0;
                bool heavy = false, noParse = false;

                void Append(string part)
                {
                    if (part.Length == 0) return;
                    part = part.Replace("\r", string.Empty).Replace('\n', ' ');
                    if (upper > 0) part = part.ToUpperInvariant();
                    else if (lower > 0) part = part.ToLowerInvariant();
                    // Text that only looks like a tag. The invisible character stops the inspector from using it as one.
                    part = part.Replace("<", "<\u200B");

                    bool isBold = bold > 0 || heavy;
                    bool hasColor = colors.Count > 0 || alpha.HasValue;
                    if (hasColor)
                    {
                        Color color = colors.Count > 0 ? colors.Peek() : defaultColor;
                        if (alpha.HasValue) color.a = alpha.Value;
                        output.Append("<color=#").Append(ColorUtility.ToHtmlStringRGBA(color)).Append('>');
                    }
                    if (isBold) output.Append("<b>");
                    if (italic > 0) output.Append("<i>");
                    output.Append(part);
                    if (italic > 0) output.Append("</i>");
                    if (isBold) output.Append("</b>");
                    if (hasColor) output.Append("</color>");
                }

                int Count(int current, bool close) => close ? Mathf.Max(0, current - 1) : current + 1;

                int position = 0;
                foreach (Match match in tagRegex.Matches(text))
                {
                    string name = match.Groups["name"].Value.ToLowerInvariant();
                    bool close = match.Groups["close"].Length > 0;
                    bool isShortColor = name[0] == '#';
                    if (noParse && !(close && name == "noparse")) continue;
                    if (!isShortColor && !knownTags.Contains(name)) continue;

                    Color tagColor = Color.white;
                    if (isShortColor && (close || !TryGetTagColor(match, out tagColor))) continue;

                    Append(text.Substring(position, match.Index - position));
                    position = match.Index + match.Length;

                    switch (name)
                    {
                        case "b": bold = Count(bold, close); break;
                        case "i": italic = Count(italic, close); break;
                        case "uppercase":
                        case "allcaps":
                        case "smallcaps": upper = Count(upper, close); break;
                        case "lowercase": lower = Count(lower, close); break;
                        case "noparse": noParse = !close; break;
                        case "br":
                        case "cr": Append(" "); break;
                        case "font-weight":
                            heavy = !close && int.TryParse(TrimValue(match), NumberStyles.Integer, CultureInfo.InvariantCulture, out int weight) && weight >= 600;
                            break;
                        case "alpha":
                            alpha = null;
                            if (!close && int.TryParse(TrimValue(match).TrimStart('#'), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int alphaValue))
                                alpha = Mathf.Clamp01(alphaValue / 255f);
                            break;
                        case "color":
                            if (close)
                            {
                                if (colors.Count > 0) colors.Pop();
                                alpha = null;
                            }
                            else if (TryGetTagColor(match, out tagColor))
                            {
                                colors.Push(tagColor);
                                alpha = null;
                            }
                            break;
                        default:
                            if (isShortColor)
                            {
                                colors.Push(tagColor);
                                alpha = null;
                            }
                            break;
                    }
                }
                Append(text.Substring(position));
                return output.ToString();
            }
        }
    }
}
