using Hearthstone_Deck_Tracker;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Runtime.Serialization.Json;
using System.Text.RegularExpressions;
using System.Runtime.Serialization;

namespace PackTracker.Update
{
    public class Updater
    {
        private const string UserAgent = "PackTracker";
        private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

        public bool? NewVersionAvailable()
        {
            try
            {
                var LatestRelease = this.GetLatestRelease();
                var LatestVersion = ParseVersion(LatestRelease.tag_name);

                return Plugin.CurrentVersion.CompareTo(LatestVersion) < 0;
            }
            catch (Exception exception) when (
                exception is WebException
                || exception is SerializationException
                || exception is InvalidOperationException
                || exception is ArgumentException)
            {
                return null;
            }

        }

        public static Version ParseVersion(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                throw new FormatException("The release does not contain a version tag.");
            }

            var match = Regex.Match(version, @"\d+(\.\d+)*");
            if (!match.Success)
            {
                throw new FormatException($"'{version}' is not a valid release version.");
            }

            return new Version(match.Value);
        }

        public bool Update()
        {
            string tempPath = null;

            try
            {
                var LatestRelease = this.GetLatestRelease();
                var Asset = LatestRelease?.assets?.SingleOrDefault(x => x.name == "PackTracker.zip");
                if (Asset == null || !Uri.TryCreate(Asset.browser_download_url, UriKind.Absolute, out var assetUri) || assetUri.Scheme != Uri.UriSchemeHttps)
                {
                    return false;
                }

                using (var client = new WebClient())
                {
                    client.Headers[HttpRequestHeader.UserAgent] = UserAgent;
                    using (var download = client.OpenRead(assetUri))
                    {
                        var path = Path.Combine(Config.AppDataPath, "Plugins");
                        Directory.CreateDirectory(path);
                        tempPath = Path.Combine(Path.GetTempPath(), $"PackTracker-{Guid.NewGuid():N}");
                        Directory.CreateDirectory(tempPath);

                        using (var Zipper = new ZipArchive(download, ZipArchiveMode.Read))
                        {
                            ExtractSafely(Zipper, tempPath);
                        }

                        foreach (var file in Directory.GetFiles(tempPath))
                        {
                            var target = Path.Combine(path, Path.GetFileName(file));
                            File.Copy(file, target, true);
                            File.SetLastWriteTime(target, DateTime.Now);
                        }
                        return true;
                    }
                }
            }
            catch (Exception exception) when (
                exception is WebException
                || exception is IOException
                || exception is InvalidDataException
                || exception is InvalidOperationException
                || exception is UnauthorizedAccessException
                || exception is SerializationException
                || exception is ArgumentException)
            {
                return false;
            }
            finally
            {
                if (tempPath != null && Directory.Exists(tempPath))
                {
                    try
                    {
                        Directory.Delete(tempPath, true);
                    }
                    catch (IOException)
                    {
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                }
            }
        }

        public Release GetLatestRelease()
        {
            var request = CreateRequest(@"https://api.github.com/repos/sgkoishi/PackTracker/releases/latest");

            var Release = new Release();
            using (var response = request.GetResponse().GetResponseStream())
            {
                var ser = new DataContractJsonSerializer(Release.GetType());
                Release = (Release)ser.ReadObject(response);
            }

            return Release;
        }

        public IEnumerable<Release> GetAllReleases()
        {
            var Releases = new List<Release>();

            var request = CreateRequest(@"https://api.github.com/repos/sgkoishi/PackTracker/releases");

            try
            {
                using (var response = request.GetResponse().GetResponseStream())
                {
                    var ser = new DataContractJsonSerializer(Releases.GetType());
                    Releases = (List<Release>)ser.ReadObject(response);
                }
            }
            catch (WebException)
            {
                return null;
            }

            return Releases.AsEnumerable();
        }

        private static HttpWebRequest CreateRequest(string uri)
        {
            var request = WebRequest.CreateHttp(uri);
            request.UserAgent = UserAgent;
            request.Timeout = (int)RequestTimeout.TotalMilliseconds;
            request.ReadWriteTimeout = (int)RequestTimeout.TotalMilliseconds;
            return request;
        }

        internal static void ExtractSafely(ZipArchive archive, string destinationDirectory)
        {
            var destinationRoot = Path.GetFullPath(destinationDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;

            foreach (var entry in archive.Entries)
            {
                var destinationPath = Path.GetFullPath(Path.Combine(destinationRoot, entry.FullName));
                if (!destinationPath.StartsWith(destinationRoot, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("The update archive contains an unsafe path.");
                }

                if (string.IsNullOrEmpty(entry.Name))
                {
                    Directory.CreateDirectory(destinationPath);
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                entry.ExtractToFile(destinationPath, true);
            }
        }
    }
}
