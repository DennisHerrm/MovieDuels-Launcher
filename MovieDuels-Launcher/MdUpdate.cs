using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;

namespace MovieDuelsLauncher
{
	// Which MovieDuels update the installed assets are, found the way the game finds it: the singleplayer
	// main menu (ui/main.menu) says it with "uiScript mdUpdate 9", which sets the game's read-only md_update
	// cvar. A main menu without that line is Update 8 (the game's default).
	//
	// The menu the game loads is the one in the last pk3 (in name order) of the MD folder that has it.
	public static class MdUpdate
	{
		public const int Default = 8;

		const string GameFolder = "MD";
		const string MainMenu = "ui/main.menu";

		static readonly Regex MdUpdateLine = new Regex(@"uiScript\s+""?mdUpdate""?\s+""?(\d+)", RegexOptions.IgnoreCase);

		// launcherFolder: where the launcher (and the game exes) are
		public static int Detect(string launcherFolder)
		{
			try
			{
				string dir = Path.Combine(launcherFolder, GameFolder);
				if (!Directory.Exists(dir))
				{
					return Default;
				}

				// the last pk3 wins, so look at them from the last one back
				foreach (string pk3 in Directory.GetFiles(dir, "*.pk3").OrderByDescending(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase))
				{
					string menu = ReadMainMenu(pk3);
					if (menu != null)
					{
						return ParseUpdate(menu);
					}
				}
			}
			catch (Exception)
			{
				// can't read the game folder: assume the oldest update this launcher knows
			}
			return Default;
		}

		// The main menu text of a pk3, or null if it has none (or can't be read).
		static string ReadMainMenu(string pk3)
		{
			try
			{
				using (ZipArchive zip = ZipFile.OpenRead(pk3))
				{
					ZipArchiveEntry entry = zip.Entries.FirstOrDefault(e => string.Equals(e.FullName.Replace('\\', '/'), MainMenu, StringComparison.OrdinalIgnoreCase));
					if (entry == null)
					{
						return null;
					}
					using (var reader = new StreamReader(entry.Open()))
					{
						return reader.ReadToEnd();
					}
				}
			}
			catch (Exception)
			{
				return null; // not a zip, locked, ...: as if it had no menu
			}
		}

		static int ParseUpdate(string menu)
		{
			foreach (string line in menu.Split('\n'))
			{
				string code = line;
				int comment = code.IndexOf("//", StringComparison.Ordinal);
				if (comment >= 0)
				{
					code = code.Substring(0, comment); // a commented-out line does not count
				}
				Match m = MdUpdateLine.Match(code);
				if (m.Success && int.TryParse(m.Groups[1].Value, out int update) && update >= Default)
				{
					return update;
				}
			}
			return Default;
		}
	}
}
