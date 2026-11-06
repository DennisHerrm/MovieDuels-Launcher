using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace MovieDuelsLauncher
{
    // A news article's HTML, shown as WPF text. Only the simple parts are used, so an article is quick to write and
    // nothing in it can run: <h2> <h3> <p> <p class="note"> <ul>/<ol> <li> <b>/<strong> <i>/<em> <a href> <br>
    // <img src>. Other tags are skipped (their text stays), <script> and <style> are dropped with their content.
    // Links open in the browser; pictures come from the online folder like the article.
    public static class HtmlText
    {
        private static readonly Regex Token = new Regex(@"<!--.*?-->|<(/?)([a-zA-Z][a-zA-Z0-9]*)([^>]*)>|([^<]+)", RegexOptions.Singleline);
        private static readonly Regex Attr = new Regex(@"([a-zA-Z-]+)\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s>]+))");
        private static readonly Regex Space = new Regex(@"\s+");

        public sealed class Styles
        {
            public FontFamily Text;
            public FontFamily Heading;
            public Brush Foreground;
            public Brush HeadingBrush;
            public Brush LinkBrush;
            public Brush NoteBackground;
            public Brush NoteBorder;
        }

        // the <img src> of an article, so they can be loaded before it is shown
        public static List<string> Images(string html)
        {
            var list = new List<string>();
            foreach (Match m in Token.Matches(html ?? ""))
            {
                if (m.Groups[2].Value.Equals("img", StringComparison.OrdinalIgnoreCase) && m.Groups[1].Value.Length == 0)
                {
                    string src = Attribute(m.Groups[3].Value, "src");
                    if (src != null)
                    {
                        list.Add(src);
                    }
                }
            }
            return list;
        }

        public static void AppendTo(FlowDocument doc, string html, Styles styles, Func<string, ImageSource> image, Action<string> openLink)
        {
            Block block = null;           // the paragraph being filled
            Paragraph paragraph = null;
            var lists = new Stack<List>();
            var inlines = new Stack<Span>(); // b, i, a around the text
            string skip = null;           // inside <script> / <style>

            Paragraph NewParagraph(string kind)
            {
                var p = new Paragraph { Margin = new Thickness(0, 0, 0, 10) };
                if (kind == "h2" || kind == "h3")
                {
                    p.FontFamily = styles.Heading;
                    p.FontSize = kind == "h2" ? 24 : 19;
                    p.Foreground = styles.HeadingBrush;
                    p.Margin = new Thickness(0, 6, 0, 6);
                }
                else if (kind == "note")
                {
                    p.Background = styles.NoteBackground;
                    p.BorderBrush = styles.NoteBorder;
                    p.BorderThickness = new Thickness(1);
                    p.Padding = new Thickness(12, 8, 12, 8);
                    p.FontSize = 13;
                }
                if (lists.Count > 0)
                {
                    var item = new ListItem(p);
                    p.Margin = new Thickness(0, 0, 0, 4);
                    lists.Peek().ListItems.Add(item);
                }
                else
                {
                    doc.Blocks.Add(p);
                }
                inlines.Clear();
                block = p;
                return p;
            }

            void AddInline(Inline inline)
            {
                if (paragraph == null)
                {
                    paragraph = NewParagraph("p");
                }
                if (inlines.Count > 0)
                {
                    inlines.Peek().Inlines.Add(inline);
                }
                else
                {
                    paragraph.Inlines.Add(inline);
                }
            }

            foreach (Match m in Token.Matches(html ?? ""))
            {
                if (m.Value.StartsWith("<!--", StringComparison.Ordinal))
                {
                    continue;
                }
                bool closing = m.Groups[1].Value.Length > 0;
                string tag = m.Groups[2].Value.ToLowerInvariant();
                if (skip != null)
                {
                    if (closing && tag == skip)
                    {
                        skip = null;
                    }
                    continue;
                }
                if (m.Groups[4].Success)
                {
                    string text = Space.Replace(WebUtility.HtmlDecode(m.Groups[4].Value), " ");
                    if (paragraph == null && text.Trim().Length == 0)
                    {
                        continue; // the space between blocks
                    }
                    if (paragraph != null && paragraph.Inlines.Count == 0 && inlines.Count == 0)
                    {
                        text = text.TrimStart();
                    }
                    if (text.Length > 0)
                    {
                        AddInline(new Run(text));
                    }
                    continue;
                }
                string attrs = m.Groups[3].Value;
                switch (tag)
                {
                    case "script":
                    case "style":
                        if (!closing && !attrs.TrimEnd().EndsWith("/", StringComparison.Ordinal))
                        {
                            skip = tag;
                        }
                        break;
                    case "p":
                    case "div":
                    case "h1":
                    case "h2":
                    case "h3":
                    case "h4":
                        if (closing)
                        {
                            paragraph = null;
                        }
                        else
                        {
                            string kind = tag == "h1" ? "h2" : tag == "h4" ? "h3" : tag;
                            if (kind == "p" && (Attribute(attrs, "class") ?? "").Contains("note"))
                            {
                                kind = "note";
                            }
                            paragraph = NewParagraph(kind);
                        }
                        break;
                    case "ul":
                    case "ol":
                        if (closing)
                        {
                            if (lists.Count > 0)
                            {
                                lists.Pop();
                            }
                        }
                        else
                        {
                            var list = new List
                            {
                                MarkerStyle = tag == "ol" ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc,
                                Margin = new Thickness(0, 0, 0, 10),
                                Padding = new Thickness(22, 0, 0, 0)
                            };
                            if (lists.Count > 0 && lists.Peek().ListItems.LastListItem != null)
                            {
                                lists.Peek().ListItems.LastListItem.Blocks.Add(list);
                            }
                            else
                            {
                                doc.Blocks.Add(list);
                            }
                            lists.Push(list);
                        }
                        paragraph = null;
                        break;
                    case "li":
                        paragraph = closing ? null : NewParagraph("p");
                        break;
                    case "br":
                        AddInline(new LineBreak());
                        break;
                    case "b":
                    case "strong":
                    case "i":
                    case "em":
                    case "a":
                        if (closing)
                        {
                            if (inlines.Count > 0)
                            {
                                inlines.Pop();
                            }
                            break;
                        }
                        Span span;
                        if (tag == "a")
                        {
                            string href = Attribute(attrs, "href");
                            var link = new Hyperlink { Foreground = styles.LinkBrush, Cursor = System.Windows.Input.Cursors.Hand };
                            if (href != null && href.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                            {
                                link.ToolTip = href;
                                link.Click += (s, e) => openLink(href);
                            }
                            span = link;
                        }
                        else if (tag == "b" || tag == "strong")
                        {
                            span = new Bold();
                        }
                        else
                        {
                            span = new Italic();
                        }
                        AddInline(span);
                        inlines.Push(span);
                        break;
                    case "img":
                        if (!closing)
                        {
                            ImageSource source = image(Attribute(attrs, "src"));
                            if (source != null)
                            {
                                var picture = new Image { Source = source, Stretch = Stretch.Uniform, MaxHeight = 320, HorizontalAlignment = HorizontalAlignment.Left };
                                doc.Blocks.Add(new BlockUIContainer(picture) { Margin = new Thickness(0, 4, 0, 12) });
                                paragraph = null;
                            }
                        }
                        break;
                }
            }
            if (block == null && doc.Blocks.Count == 0)
            {
                doc.Blocks.Add(new Paragraph(new Run("This article is empty.")));
            }
        }

        private static string Attribute(string attrs, string name)
        {
            foreach (Match a in Attr.Matches(attrs ?? ""))
            {
                if (a.Groups[1].Value.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    string v = a.Groups[2].Success ? a.Groups[2].Value : a.Groups[3].Success ? a.Groups[3].Value : a.Groups[4].Value;
                    return WebUtility.HtmlDecode(v).Trim();
                }
            }
            return null;
        }
    }
}
