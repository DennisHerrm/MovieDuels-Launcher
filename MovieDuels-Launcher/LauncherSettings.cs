using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace MovieDuelsLauncher
{
    // The launcher's options, remembered in MovieDuels-Launcher.ini in Documents\My Games\MovieDuels,
    // next to the game's own configs and saves (a game installed under Program Files can't write next
    // to its exe). Same file and keys as the earlier launchers, so players keep their options.
    //
    // Renderer, display and resolution come from the game's own config: the one in Documents\My Games\MovieDuels\MD
    // when it exists, else the one in the install's MD folder (MovieDuels-MP.cfg, else MovieDuels-SP.cfg). Changing
    // them on the OPTIONS page writes them into both the SP and the MP config: only those "seta" lines are replaced
    // (or added), every other line stays as it was.
    public sealed class LauncherSettings
    {
        private const string IniName = "MovieDuels-Launcher.ini";
        private const string HomeFolder = "MovieDuels"; // the game's folder in My Games (HOMEPATH_NAME_WIN)
        private const string ModFolder = "MD";          // fs_game
        private const string SPConfig = "MovieDuels-SP.cfg";
        private const string MPConfig = "MovieDuels-MP.cfg";
        private const string Section = "Options";

        // resolutions the OPTIONS page cycles through (0 x 0 = the desktop resolution, r_mode -2)
        private static readonly int[,] Resolutions =
        {
            { 0, 0 }, { 1280, 720 }, { 1366, 768 }, { 1600, 900 }, { 1920, 1080 }, { 2560, 1440 }, { 3840, 2160 }
        };

        // the engine's fixed r_mode list (shared/sdl/sdl_window.cpp r_vidModes, r_mode 0..20)
        private static readonly int[,] RModes =
        {
            { 320, 240 }, { 400, 300 }, { 512, 384 }, { 640, 480 }, { 800, 600 }, { 856, 480 }, { 960, 720 }, { 1024, 768 },
            { 1152, 864 }, { 1280, 720 }, { 1280, 1024 }, { 1366, 768 }, { 1440, 900 }, { 1600, 900 }, { 1600, 1200 },
            { 1680, 1050 }, { 1920, 1080 }, { 2048, 1536 }, { 2400, 600 }, { 2560, 1440 }, { 3840, 2160 }
        };

        // the configs are plain 8-bit text: Latin-1 reads and writes every byte back unchanged
        private static readonly Encoding CfgEncoding = Encoding.GetEncoding("ISO-8859-1");

        public bool Rend2;
        public bool Windowed;
        public bool Controller;       // in_joystick
        public bool Music = true;     // launcher background music (speaker button)
        public int ResWidth;          // 0 = desktop resolution
        public int ResHeight;
        public bool CheckUpdates = true; // look for a new update (online/latest.json) when the launcher opens
        public int LastUpdate;        // the update the launcher last saw installed (0 = never ran): a higher one gets checked
        public string SeenNews = "";  // ids of the news articles already shown, comma separated (the NEWS button flashes for new ones)

        // the launcher's own files (news cache) live next to the ini
        public string DataFolder => Path.GetDirectoryName(iniPath);

        // the game's own folder in Documents (configs, saves): My Games\MovieDuels\MD, or null without Documents
        public string ConfigFolder => homeModFolder;

        private readonly string iniPath;
        private readonly string launcherFolder;
        private readonly string homeModFolder; // Documents\My Games\MovieDuels\MD, or null

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern uint GetPrivateProfileIntW(string section, string key, int fallback, string file);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern uint GetPrivateProfileStringW(string section, string key, string fallback, StringBuilder value, uint size, string file);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern bool WritePrivateProfileStringW(string section, string key, string value, string file);

        // launcherFolder: where the launcher (and the game exes) are
        public LauncherSettings(string launcherFolder)
        {
            this.launcherFolder = launcherFolder;
            iniPath = Path.Combine(launcherFolder, IniName); // no Documents folder: next to the launcher
            try
            {
                string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (!string.IsNullOrEmpty(docs))
                {
                    string dir = Path.Combine(docs, "My Games", HomeFolder);
                    homeModFolder = Path.Combine(dir, ModFolder);
                    Directory.CreateDirectory(dir);
                    string ini = Path.Combine(dir, IniName);

                    // the first time, take over the options from an ini next to the launcher (older launchers)
                    if (!File.Exists(ini) && File.Exists(iniPath))
                    {
                        File.Copy(iniPath, ini);
                    }
                    iniPath = ini;
                }
            }
            catch (Exception)
            {
                // can't use Documents: keep the ini next to the launcher
            }
            Load();
            LoadGameConfig();
        }

        private void Load()
        {
            Rend2 = GetPrivateProfileIntW(Section, "Rend2", 0, iniPath) != 0;
            Windowed = GetPrivateProfileIntW(Section, "Windowed", 0, iniPath) != 0;
            Controller = GetPrivateProfileIntW(Section, "Controller", 0, iniPath) != 0;
            Music = GetPrivateProfileIntW(Section, "Music", 1, iniPath) != 0;
            CheckUpdates = GetPrivateProfileIntW(Section, "CheckUpdates", 1, iniPath) != 0;
            LastUpdate = (int)GetPrivateProfileIntW(Section, "LastUpdate", 0, iniPath);
            var seen = new StringBuilder(8192);
            GetPrivateProfileStringW(Section, "SeenNews", "", seen, (uint)seen.Capacity, iniPath);
            SeenNews = seen.ToString();
        }

        public void Save()
        {
            WritePrivateProfileStringW(Section, "Rend2", Rend2 ? "1" : "0", iniPath);
            WritePrivateProfileStringW(Section, "Windowed", Windowed ? "1" : "0", iniPath);
            WritePrivateProfileStringW(Section, "Controller", Controller ? "1" : "0", iniPath);
            WritePrivateProfileStringW(Section, "Music", Music ? "1" : "0", iniPath);
            WritePrivateProfileStringW(Section, "CheckUpdates", CheckUpdates ? "1" : "0", iniPath);
            WritePrivateProfileStringW(Section, "LastUpdate", LastUpdate.ToString(), iniPath);
            WritePrivateProfileStringW(Section, "SeenNews", SeenNews, iniPath);
        }

        // ------------------------------------------------------------------------------------------
        // The game's configs
        // ------------------------------------------------------------------------------------------

        // the config of SP or MP: Documents first, then the install folder; when neither exists, the Documents one
        // (written on save; the game reads it when it starts)
        public string GameConfigPath(bool multiplayer)
        {
            string name = multiplayer ? MPConfig : SPConfig;
            string home = homeModFolder != null ? Path.Combine(homeModFolder, name) : null;
            string install = Path.Combine(launcherFolder, ModFolder, name);
            if (home != null && File.Exists(home))
            {
                return home;
            }
            if (File.Exists(install))
            {
                return install;
            }
            return home ?? install;
        }

        private static List<string> ReadLines(string path)
        {
            var lines = new List<string>();
            try
            {
                if (!File.Exists(path))
                {
                    return lines;
                }
                string text = CfgEncoding.GetString(File.ReadAllBytes(path));
                foreach (string line in text.Split('\n'))
                {
                    lines.Add(line.EndsWith("\r") ? line.Substring(0, line.Length - 1) : line);
                }
            }
            catch (Exception)
            {
                lines.Clear(); // unreadable: as if there were none
            }
            return lines;
        }

        // the cvar of a "seta name value" / "set name value" line (and its value), else null
        private static string CfgLineCvar(string line, out string value)
        {
            value = null;
            string[] parts = line.Trim().Split(new[] { ' ', '\t' }, 3, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || (!parts[0].Equals("seta", StringComparison.OrdinalIgnoreCase)
                && !parts[0].Equals("set", StringComparison.OrdinalIgnoreCase)))
            {
                return null;
            }
            if (parts.Length > 2)
            {
                string v = parts[2].Trim();
                if (v.Length >= 2 && v[0] == '"')
                {
                    int close = v.IndexOf('"', 1);
                    v = close < 0 ? v.Substring(1) : v.Substring(1, close - 1);
                }
                value = v;
            }
            return parts[1];
        }

        private static string ReadCfgValue(List<string> lines, string cvar)
        {
            string found = null;
            foreach (string line in lines)
            {
                if (string.Equals(CfgLineCvar(line, out string v), cvar, StringComparison.OrdinalIgnoreCase))
                {
                    found = v; // the last one wins, as when the game execs it
                }
            }
            return found;
        }

        // replaces the lines of these cvars (or adds them at the end); the rest of the file is kept as it was
        private static void WriteCfgValues(string path, List<KeyValuePair<string, string>> values)
        {
            List<string> lines = ReadLines(path);
            foreach (var kv in values)
            {
                string line = "seta " + kv.Key + " \"" + kv.Value + "\"";
                bool replaced = false;
                for (int i = 0; i < lines.Count; i++)
                {
                    if (string.Equals(CfgLineCvar(lines[i], out string unused), kv.Key, StringComparison.OrdinalIgnoreCase))
                    {
                        lines[i] = line;
                        replaced = true;
                    }
                }
                if (!replaced)
                {
                    // keep a last empty line (the file's final newline) last
                    if (lines.Count > 0 && lines[lines.Count - 1].Length == 0)
                    {
                        lines.Insert(lines.Count - 1, line);
                    }
                    else
                    {
                        lines.Add(line);
                    }
                }
            }
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var text = new StringBuilder();
                for (int i = 0; i < lines.Count; i++)
                {
                    text.Append(lines[i]);
                    if (i + 1 < lines.Count || lines[i].Length > 0)
                    {
                        text.Append("\r\n");
                    }
                }
                File.WriteAllBytes(path, CfgEncoding.GetBytes(text.ToString()));
            }
            catch (Exception)
            {
                // read-only install folder or similar: the launcher still passes the values on the command line
            }
        }

        // renderer, display and resolution from the game's config (MP's, else SP's); keeps the launcher's values when
        // there is no config yet
        private void LoadGameConfig()
        {
            List<string> lines = ReadLines(GameConfigPath(true));
            if (lines.Count == 0)
            {
                lines = ReadLines(GameConfigPath(false));
            }
            if (lines.Count == 0)
            {
                return;
            }
            string v = ReadCfgValue(lines, "cl_renderer");
            if (v != null)
            {
                Rend2 = v.IndexOf("rend2", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            v = ReadCfgValue(lines, "r_fullscreen");
            if (v != null)
            {
                Windowed = ToInt(v) == 0;
            }
            v = ReadCfgValue(lines, "r_mode");
            if (v != null)
            {
                int mode = ToInt(v);
                ResWidth = 0;
                ResHeight = 0;
                if (mode == -1)
                {
                    ResWidth = ToInt(ReadCfgValue(lines, "r_customwidth"));
                    ResHeight = ToInt(ReadCfgValue(lines, "r_customheight"));
                }
                else if (mode >= 0 && mode < RModes.GetLength(0))
                {
                    ResWidth = RModes[mode, 0];
                    ResHeight = RModes[mode, 1];
                }
                if (ResWidth <= 0 || ResHeight <= 0)
                {
                    ResWidth = 0;
                    ResHeight = 0;
                }
            }
        }

        private static int ToInt(string s)
        {
            return int.TryParse(s?.Trim(), out int n) ? n : 0;
        }

        // the cvars the OPTIONS page sets, for the SP or the MP config / command line
        public List<KeyValuePair<string, string>> GameConfigValues(bool multiplayer)
        {
            string renderer = multiplayer
                ? (Rend2 ? "MovieDuels-rdmp-rend2" : "MovieDuels-rdmp")
                : (Rend2 ? "MovieDuels-rdsp-rend2" : "MovieDuels-rdsp");
            var values = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("cl_renderer", renderer),
                new KeyValuePair<string, string>("com_rend2", Rend2 ? "1" : "0"),
                new KeyValuePair<string, string>("r_fullscreen", Windowed ? "0" : "1")
            };
            if (ResWidth > 0)
            {
                values.Add(new KeyValuePair<string, string>("r_mode", "-1"));
                values.Add(new KeyValuePair<string, string>("r_customwidth", ResWidth.ToString()));
                values.Add(new KeyValuePair<string, string>("r_customheight", ResHeight.ToString()));
            }
            else
            {
                values.Add(new KeyValuePair<string, string>("r_mode", "-2"));
            }
            return values;
        }

        public void SaveGameConfig()
        {
            WriteCfgValues(GameConfigPath(false), GameConfigValues(false));
            WriteCfgValues(GameConfigPath(true), GameConfigValues(true));
        }

        // the next resolution of the list (from a resolution that is not in it: the first)
        public void NextResolution()
        {
            int count = Resolutions.GetLength(0);
            int next = 0;
            for (int i = 0; i < count; i++)
            {
                if (Resolutions[i, 0] == ResWidth && Resolutions[i, 1] == ResHeight)
                {
                    next = (i + 1) % count;
                    break;
                }
            }
            ResWidth = Resolutions[next, 0];
            ResHeight = Resolutions[next, 1];
        }

        // the resolution before this one in the list
        public void PreviousResolution()
        {
            int count = Resolutions.GetLength(0);
            int previous = 0;
            for (int i = 0; i < count; i++)
            {
                if (Resolutions[i, 0] == ResWidth && Resolutions[i, 1] == ResHeight)
                {
                    previous = (i + count - 1) % count;
                    break;
                }
            }
            ResWidth = Resolutions[previous, 0];
            ResHeight = Resolutions[previous, 1];
        }

        public string ResolutionText => ResWidth > 0 ? ResWidth + " x " + ResHeight : "Desktop";
    }
}