using System;
using System.IO;
using System.Runtime.InteropServices;

namespace MovieDuelsLauncher
{
    // The launcher's options, remembered in MovieDuels-Launcher.ini in Documents\My Games\MovieDuels,
    // next to the game's own configs and saves (a game installed under Program Files can't write next
    // to its exe). Same file and keys as the earlier launchers, so players keep their options.
    public sealed class LauncherSettings
    {
        private const string IniName = "MovieDuels-Launcher.ini";
        private const string HomeFolder = "MovieDuels"; // the game's folder in My Games (HOMEPATH_NAME_WIN)
        private const string Section = "Options";

        public bool Rend2;
        public bool Windowed;
        public bool Controller;       // in_joystick
        public bool Music = true;     // launcher background music (speaker button)

        private readonly string iniPath;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern uint GetPrivateProfileIntW(string section, string key, int fallback, string file);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern bool WritePrivateProfileStringW(string section, string key, string value, string file);

        // launcherFolder: where the launcher (and the game exes) are
        public LauncherSettings(string launcherFolder)
        {
            iniPath = Path.Combine(launcherFolder, IniName); // no Documents folder: next to the launcher
            try
            {
                string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (!string.IsNullOrEmpty(docs))
                {
                    string dir = Path.Combine(docs, "My Games", HomeFolder);
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
        }

        private void Load()
        {
            Rend2 = GetPrivateProfileIntW(Section, "Rend2", 0, iniPath) != 0;
            Windowed = GetPrivateProfileIntW(Section, "Windowed", 0, iniPath) != 0;
            Controller = GetPrivateProfileIntW(Section, "Controller", 0, iniPath) != 0;
            Music = GetPrivateProfileIntW(Section, "Music", 1, iniPath) != 0;
        }

        public void Save()
        {
            WritePrivateProfileStringW(Section, "Rend2", Rend2 ? "1" : "0", iniPath);
            WritePrivateProfileStringW(Section, "Windowed", Windowed ? "1" : "0", iniPath);
            WritePrivateProfileStringW(Section, "Controller", Controller ? "1" : "0", iniPath);
            WritePrivateProfileStringW(Section, "Music", Music ? "1" : "0", iniPath);
        }
    }
}