using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace MovieDuelsLauncher
{
    // The texts of the ABOUT page, in the plain formats the team writes them in:
    //
    //   Assets/Text/About.txt  a paragraph per line, "- " lines are a list, the last paragraph is the small print
    //   Assets/Text/FAQ.txt    "## Group", "Q: question", "A: answer" (more lines belong to the answer)
    //   Assets/Text/Team.txt   a heading line, then "- Name - Role" (team) or "- Name" (thanks)
    //   MD/ReadMe/ReadMe_Credits.txt (the game's own): **Section**, ==Group==, /Entry/, "Key: value" lines
    //
    // Lines starting with "# " are notes for whoever edits the file and are not shown.
    public static class TextContent
    {
        public sealed class AboutText
        {
            public List<string> Paragraphs = new List<string>();
            public List<string> Bullets = new List<string>();
            public string SmallPrint;
        }

        public sealed class FaqGroup
        {
            public string Name;
            public List<KeyValuePair<string, List<string>>> Questions = new List<KeyValuePair<string, List<string>>>();
        }

        public sealed class TeamGroup
        {
            public string Name;
            public List<KeyValuePair<string, string>> People = new List<KeyValuePair<string, string>>(); // name, role (or null)
        }

        public sealed class CreditEntry
        {
            public string Name;
            public string Note; // a line like "/Special thanks for HD textures: .../": shown as a note, not a card
            public List<KeyValuePair<string, string>> Fields = new List<KeyValuePair<string, string>>();
            public List<string> Links = new List<string>();
        }

        public sealed class CreditGroup
        {
            public string Name;
            public List<CreditEntry> Entries = new List<CreditEntry>();
        }

        public sealed class CreditSection
        {
            public string Name;
            public List<CreditGroup> Groups = new List<CreditGroup>();
        }

        private static IEnumerable<string> Lines(string text)
        {
            foreach (string raw in (text ?? "").Replace("\r\n", "\n").Split('\n'))
            {
                string line = raw.Trim();
                if (!line.StartsWith("# ", StringComparison.Ordinal) && line != "#")
                {
                    yield return line;
                }
            }
        }

        public static AboutText ParseAbout(string text)
        {
            var about = new AboutText();
            foreach (string line in Lines(text))
            {
                if (line.Length == 0)
                {
                    continue;
                }
                if (line.StartsWith("- ", StringComparison.Ordinal))
                {
                    about.Bullets.Add(line.Substring(2).Trim());
                }
                else
                {
                    about.Paragraphs.Add(line);
                }
            }
            // the last paragraph after the list is the small print (the disclaimer)
            if (about.Paragraphs.Count > 1)
            {
                about.SmallPrint = about.Paragraphs[about.Paragraphs.Count - 1];
                about.Paragraphs.RemoveAt(about.Paragraphs.Count - 1);
            }
            return about;
        }

        public static List<FaqGroup> ParseFaq(string text)
        {
            var groups = new List<FaqGroup>();
            FaqGroup group = null;
            List<string> answer = null;
            foreach (string line in Lines(text))
            {
                if (line.StartsWith("## ", StringComparison.Ordinal))
                {
                    group = new FaqGroup { Name = line.Substring(3).Trim() };
                    groups.Add(group);
                    answer = null;
                }
                else if (line.StartsWith("Q:", StringComparison.Ordinal))
                {
                    if (group == null)
                    {
                        group = new FaqGroup { Name = "" };
                        groups.Add(group);
                    }
                    answer = new List<string>();
                    group.Questions.Add(new KeyValuePair<string, List<string>>(line.Substring(2).Trim(), answer));
                }
                else if (line.StartsWith("A:", StringComparison.Ordinal) && answer != null)
                {
                    answer.Add(line.Substring(2).Trim());
                }
                else if (line.Length > 0 && answer != null)
                {
                    answer.Add(line);
                }
            }
            return groups;
        }

        public static List<TeamGroup> ParseTeam(string text)
        {
            var groups = new List<TeamGroup>();
            TeamGroup group = null;
            foreach (string line in Lines(text))
            {
                if (line.Length == 0)
                {
                    continue;
                }
                if (line.StartsWith("- ", StringComparison.Ordinal))
                {
                    if (group == null)
                    {
                        group = new TeamGroup { Name = "" };
                        groups.Add(group);
                    }
                    string item = line.Substring(2).Trim();
                    int dash = item.IndexOf(" - ", StringComparison.Ordinal);
                    group.People.Add(dash > 0
                        ? new KeyValuePair<string, string>(item.Substring(0, dash).Trim(), item.Substring(dash + 3).Trim())
                        : new KeyValuePair<string, string>(item, null));
                }
                else
                {
                    group = new TeamGroup { Name = line };
                    groups.Add(group);
                }
            }
            groups.RemoveAll(g => g.People.Count == 0);
            return groups;
        }

        // ------------------------------------------------------------------------------------------
        // The game's ReadMe_Credits.txt
        // ------------------------------------------------------------------------------------------

        private static readonly Regex SectionLine = new Regex(@"^\*\*(.+)\*\*$");
        private static readonly Regex GroupLine = new Regex(@"^==(.*)==$");
        private static readonly Regex EntryLine = new Regex(@"^/(.+)/$");
        private static readonly Regex FieldLine = new Regex(@"^([^:]+?):\s*(.*)$");

        // the installed file is Windows-1250 (it has "Gergő"); a UTF-8 one is read as UTF-8
        public static string ReadCreditsFile(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return null;
                }
                byte[] data = File.ReadAllBytes(path);
                try
                {
                    return new UTF8Encoding(false, true).GetString(data).TrimStart('﻿');
                }
                catch (DecoderFallbackException)
                {
                    return Encoding.GetEncoding(1250).GetString(data);
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static List<CreditSection> ParseCredits(string text)
        {
            var sections = new List<CreditSection>();
            CreditSection section = null;
            CreditGroup group = null;
            CreditEntry entry = null;
            foreach (string line in (text ?? "").Replace("\r\n", "\n").Split('\n'))
            {
                string l = line.Trim();
                if (l.Length == 0)
                {
                    entry = null;
                    continue;
                }
                Match m = SectionLine.Match(l);
                if (m.Success)
                {
                    section = new CreditSection { Name = m.Groups[1].Value.Trim() };
                    sections.Add(section);
                    group = new CreditGroup { Name = "" };
                    section.Groups.Add(group);
                    entry = null;
                    continue;
                }
                if (section == null)
                {
                    continue; // the title line
                }
                m = GroupLine.Match(l);
                if (m.Success)
                {
                    group = new CreditGroup { Name = m.Groups[1].Value.Trim() };
                    section.Groups.Add(group);
                    entry = null;
                    continue;
                }
                m = EntryLine.Match(l);
                if (m.Success)
                {
                    string name = m.Groups[1].Value.Trim();
                    if (name.StartsWith("Special thanks", StringComparison.OrdinalIgnoreCase))
                    {
                        group.Entries.Add(new CreditEntry { Note = name });
                        entry = null;
                    }
                    else
                    {
                        entry = new CreditEntry { Name = name };
                        group.Entries.Add(entry);
                    }
                    continue;
                }
                if (entry == null)
                {
                    continue;
                }
                if (l.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || l.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                {
                    entry.Links.Add(l);
                    continue;
                }
                m = FieldLine.Match(l);
                if (m.Success)
                {
                    string key = m.Groups[1].Value.Trim(), value = m.Groups[2].Value.Trim();
                    if (key.StartsWith("Link", StringComparison.OrdinalIgnoreCase))
                    {
                        if (value.Length > 0)
                        {
                            entry.Links.Add(value);
                        }
                    }
                    else if (value.Length > 0)
                    {
                        entry.Fields.Add(new KeyValuePair<string, string>(key, value));
                    }
                    continue;
                }
                entry.Fields.Add(new KeyValuePair<string, string>("", l));
            }
            foreach (CreditSection s in sections)
            {
                s.Groups.RemoveAll(g => g.Entries.Count == 0);
            }
            sections.RemoveAll(s => s.Groups.Count == 0);
            return sections;
        }

        // the name of a link for its button: the site
        public static string LinkName(string url)
        {
            Match m = Regex.Match(url ?? "", @"^https?://(?:www\.)?([^/]+)", RegexOptions.IgnoreCase);
            string host = m.Success ? m.Groups[1].Value.ToLowerInvariant() : url;
            switch (host)
            {
                case "jkhub.org": return "JKHub";
                case "community.moviebattles.org": return "Movie Battles II";
                case "mrwonko.de": return "JK3Files archive";
                default: return host;
            }
        }

        // "MODELS / SKINS [Prequel Trilogy]" -> group "Models / Skins", name "Prequel Trilogy"
        public static void SectionLabel(string name, out string group, out string label)
        {
            Match m = Regex.Match(name, @"^(.*?)\s*\[(.+)\]$");
            Match w = Regex.Match(name, @"^Weapons \((.+)\)$");
            if (m.Success)
            {
                group = "Models / Skins";
                label = m.Groups[2].Value;
            }
            else if (w.Success)
            {
                group = "Weapons";
                label = w.Groups[1].Value.Replace("Lightsaber Hilts/Melee Weapons", "Hilts and melee");
            }
            else if (name.Equals("Maps", StringComparison.OrdinalIgnoreCase) || name.Equals("Missions", StringComparison.OrdinalIgnoreCase))
            {
                group = "Maps & missions";
                label = name;
            }
            else
            {
                group = "Other";
                label = name.Replace("Movie Duels Source Code", "Source code");
            }
        }
    }
}
