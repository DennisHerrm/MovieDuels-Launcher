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
