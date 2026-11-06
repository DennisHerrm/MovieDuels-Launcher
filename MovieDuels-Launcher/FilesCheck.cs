using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace MovieDuelsLauncher
{
    // A quick look at the installation, run after a new update was installed and from SETTINGS > Check installation.
    // It does not change anything:
    //
    //   - Jedi Academy: base\assets0.pk3 .. assets3.pk3 next to the MD folder
    //   - Movie Duels: every pk3 of the MD folder opens as a zip (its file list can be read), so a broken or
    //     half-copied download shows up
    //
    // A file list per update (name, size, checksum) would also find files that are out of date; the team can
    // publish one in the online folder later.
    public static class FilesCheck
    {
        private const string ModFolder = "MD";
        private static readonly string[] BaseFiles = { "assets0.pk3", "assets1.pk3", "assets2.pk3", "assets3.pk3" };

        // the optional add-ons SETTINGS lists under ADDONS: installed when all their files are there
        public sealed class Addon
        {
            public string Name;
            public string Page;    // where to get it
            public string[] Files; // relative to the game folder
        }

        public static readonly Addon[] Addons =
        {
            new Addon
            {
                Name = "Jedi Outcast",
                Page = "https://www.moddb.com/mods/movie-duels/downloads/movie-duels-jedi-outcast-addon",
                // the add-on, and the player's own Jedi Outcast files copied into base under new names
                Files = new[] { @"MD\r_outcastaddon.pk3", @"base\0-assets0.pk3", @"base\0-assets1.pk3", @"base\0-assets2.pk3", @"base\0-assets5.pk3" }
            },
            new Addon
            {
                Name = "TOTG S1: Dark Forces 2",
                Page = "https://www.moddb.com/mods/movie-duels/downloads/tales-of-the-galaxy-dark-forces-ii-duels",
                Files = new[] { @"MD\TOTG_DF2.pk3" }
            }
        };

        // the files of an add-on that are not there
        public static List<string> Missing(string folder, Addon addon)
        {
            var missing = new List<string>();
            foreach (string file in addon.Files)
            {
                try
                {
                    if (!File.Exists(Path.Combine(folder, file)))
                    {
                        missing.Add(file);
                    }
                }
                catch (Exception)
                {
                    missing.Add(file);
                }
            }
            return missing;
        }

        public sealed class Result
        {
            public bool JediAcademy;
            public int Pk3Count;
            public List<string> Broken = new List<string>();
        }

        public static bool HasJediAcademy(string folder)
        {
            try
            {
                return BaseFiles.All(f => File.Exists(Path.Combine(folder, "base", f)));
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static string[] Pk3Files(string folder)
        {
            try
            {
                string dir = Path.Combine(folder, ModFolder);
                return Directory.Exists(dir)
                    ? Directory.GetFiles(dir, "*.pk3").OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase).ToArray()
                    : new string[0];
            }
            catch (Exception)
            {
                return new string[0];
            }
        }

        // progress(done, total, file name); called off the UI thread
        public static Result Run(string folder, Action<int, int, string> progress)
        {
            var result = new Result { JediAcademy = HasJediAcademy(folder) };
            string[] files = Pk3Files(folder);
            result.Pk3Count = files.Length;
            for (int i = 0; i < files.Length; i++)
            {
                string name = Path.GetFileName(files[i]);
                progress?.Invoke(i, files.Length, name);
                try
                {
                    using (ZipArchive zip = ZipFile.OpenRead(files[i]))
                    {
                        if (zip.Entries.Count == 0)
                        {
                            result.Broken.Add(name);
                        }
                    }
                }
                catch (Exception)
                {
                    result.Broken.Add(name); // not a zip, cut short, locked ...
                }
            }
            progress?.Invoke(files.Length, files.Length, "");
            return result;
        }
    }
}
