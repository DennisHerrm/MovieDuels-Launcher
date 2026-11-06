using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MovieDuelsLauncher
{
    // What the team publishes for the launcher, read from the online folder of the launcher's GitHub repo
    // (online/ in MovieDuels/MovieDuels-Launcher): editing a file there and pushing it is all it takes.
    //
    //   latest.json  the newest MovieDuels update and where to get it (the emblem glows red when it is newer than
    //                the installed one; the launcher downloads nothing, the buttons open ModDB / Nexus Mods)
    //   news.json    the articles of the NEWS page, newest first; each one is a small HTML file and a picture
    //
    // Everything that was read is kept in a cache folder (next to the launcher's ini), so the news still show
    // without a connection.
    public static class Online
    {
        public const string ContentUrl = "https://raw.githubusercontent.com/MovieDuels/MovieDuels-Launcher/main/online/";

        private const int TimeoutMs = 8000;

        public static string CacheFolder; // set by the window; null: no cache

        public sealed class Latest
        {
            public int Update;
            public string Title;
            public List<string> WhatsNew = new List<string>();
            public string ModDB = "https://www.moddb.com/mods/movie-duels/downloads";
            public string Nexus = "https://www.nexusmods.com/starwarsjediknightjediacademy/mods/45?tab=files";
            public string Article; // id of a news article about it, or null
        }

        public sealed class Article
        {
            public string Id;
            public string Tag;
            public string Date;
            public string Title;
            public string Summary;
            public string Image; // path in the online folder (or a full https address)
            public string File;  // the article's HTML
        }

        // a path of the online folder as a web address (a full https address stays as it is)
        public static string Resolve(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }
            if (path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }
            if (path.StartsWith("http:", StringComparison.OrdinalIgnoreCase) || path.Contains(".."))
            {
                return null; // only https, and nothing outside the online folder
            }
            return ContentUrl + path.TrimStart('/');
        }

        // ------------------------------------------------------------------------------------------
        // Reading, with the cache as fallback (called off the UI thread)
        // ------------------------------------------------------------------------------------------

        public static byte[] GetBytes(string path, bool preferCache = false)
        {
            string url = Resolve(path);
            if (url == null)
            {
                return null;
            }
            string cached = CachePath(url);
            if (preferCache && cached != null && File.Exists(cached))
            {
                try
                {
                    return File.ReadAllBytes(cached);
                }
                catch (Exception)
                {
                }
            }
            byte[] data = Download(url);
            if (data != null)
            {
                try
                {
                    if (cached != null)
                    {
                        Directory.CreateDirectory(CacheFolder);
                        File.WriteAllBytes(cached, data);
                    }
                }
                catch (Exception)
                {
                    // no cache this time
                }
                return data;
            }
            try
            {
                return cached != null && File.Exists(cached) ? File.ReadAllBytes(cached) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static string GetText(string path)
        {
            byte[] data = GetBytes(path);
            return data == null ? null : new UTF8Encoding(false).GetString(data).TrimStart('﻿');
        }

        // a picture, decoded and frozen so the window thread can use it; pictures don't change, so the cache comes first
        public static ImageSource GetImage(string path)
        {
            byte[] data = GetBytes(path, true);
            if (data == null)
            {
                return null;
            }
            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = new MemoryStream(data);
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch (Exception)
            {
                return null; // not a picture
            }
        }

        private static byte[] Download(string url)
        {
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Timeout = TimeoutMs;
                request.ReadWriteTimeout = TimeoutMs;
                request.UserAgent = "MovieDuels-Launcher";
                request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                using (var response = (HttpWebResponse)request.GetResponse())
                using (Stream stream = response.GetResponseStream())
                using (var memory = new MemoryStream())
                {
                    stream.CopyTo(memory);
                    return memory.ToArray();
                }
            }
            catch (Exception)
            {
                return null; // offline, not published yet (404), ...
            }
        }

        private static string CachePath(string url)
        {
            if (CacheFolder == null)
            {
                return null;
            }
            var name = new StringBuilder();
            foreach (char c in url.Substring(url.IndexOf("//", StringComparison.Ordinal) + 2))
            {
                name.Append(char.IsLetterOrDigit(c) || c == '.' || c == '-' ? c : '_');
            }
            return Path.Combine(CacheFolder, name.ToString());
        }

        // ------------------------------------------------------------------------------------------
        // The files
        // ------------------------------------------------------------------------------------------

        public static Latest ParseLatest(string json)
        {
            var o = ParseObject(json);
            if (o == null || !(o.TryGetValue("update", out object update)) || !int.TryParse(Convert.ToString(update), out int number))
            {
                return null;
            }
            var latest = new Latest { Update = number, Title = Str(o, "title") ?? "Update " + number, Article = Str(o, "article") };
            latest.ModDB = Str(o, "moddb") ?? latest.ModDB;
            latest.Nexus = Str(o, "nexus") ?? latest.Nexus;
            if (o.TryGetValue("whatsNew", out object lines) && lines is IEnumerable list && !(lines is string))
            {
                foreach (object line in list)
                {
                    string text = Convert.ToString(line);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        latest.WhatsNew.Add(text.Trim());
                    }
                }
            }
            return latest;
        }

        public static List<Article> ParseNews(string json)
        {
            var articles = new List<Article>();
            var o = ParseObject(json);
            if (o == null || !o.TryGetValue("articles", out object items) || !(items is IEnumerable list))
            {
                return articles;
            }
            foreach (object item in list)
            {
                if (item is Dictionary<string, object> a && Str(a, "id") != null && Str(a, "title") != null)
                {
                    articles.Add(new Article
                    {
                        Id = Str(a, "id"),
                        Tag = Str(a, "tag") ?? "News",
                        Date = Str(a, "date") ?? "",
                        Title = Str(a, "title"),
                        Summary = Str(a, "summary") ?? "",
                        Image = Str(a, "image"),
                        File = Str(a, "file")
                    });
                }
            }
            return articles;
        }

        private static Dictionary<string, object> ParseObject(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }
            try
            {
                return new JavaScriptSerializer().DeserializeObject(json) as Dictionary<string, object>;
            }
            catch (Exception)
            {
                return null; // a typo in the file: as if there was none
            }
        }

        private static string Str(Dictionary<string, object> o, string key)
        {
            if (o.TryGetValue(key, out object value) && value != null)
            {
                string s = Convert.ToString(value).Trim();
                return s.Length > 0 ? s : null;
            }
            return null;
        }
    }
}
