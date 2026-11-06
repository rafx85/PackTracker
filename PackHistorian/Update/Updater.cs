using Hearthstone_Deck_Tracker;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text.RegularExpressions;
using System.Threading;

namespace PackTracker.Update
{
    public class Updater
    {
        internal const string ReleasesPageUrl = "https://github.com/rafx85/PackTracker/releases";
        internal const string ReleasesApiUrl = "https://api.github.com/repos/rafx85/PackTracker/releases";
        private const int RequestTimeoutMilliseconds = 15000;
        private static int _updateStarted;

        public bool? NewVersionAvailable()
        {
            try
            {
                return IsNewerRelease(this.GetLatestRelease());
            }
            catch (Exception exception) when (
                exception is WebException || exception is SerializationException || exception is InvalidOperationException)
            {
                return null;
            }
        }

        public static Version ParseVersion(string version)
        {
            var match = Regex.Match(version ?? "", @"\d+(\.\d+)*");
            if (!match.Success || !Version.TryParse(match.Value, out var parsed))
                throw new FormatException($"'{version}' is not a valid release version.");

            return parsed;
        }

        internal static bool TryGetReleaseVersion(Release release, out Version version)
        {
            version = null;
            if (release == null || release.draft || release.prerelease || string.IsNullOrEmpty(release.tag_name))
                return false;

            var tag = release.tag_name;
            if (tag.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                tag = tag.Substring(1);
            if (!Version.TryParse(tag, out var parsed))
                return false;

            version = UpdatePackage.NormalizeVersion(parsed);
            return true;
        }

        internal static bool IsNewerRelease(Release release) =>
            TryGetReleaseVersion(release, out var version) && version > Plugin.CurrentVersion;

        internal static Asset GetUpdateAsset(Release release)
        {
            if (!TryGetReleaseVersion(release, out _))
                return null;

            var matches = release.assets?.Where(asset => asset?.name == UpdatePackage.AssetName).ToArray();
            if (matches?.Length != 1)
                return null;

            var asset = matches[0];
            var expectedUrl = $"{ReleasesPageUrl}/download/{Uri.EscapeDataString(release.tag_name)}/{UpdatePackage.AssetName}";
            return asset.browser_download_url == expectedUrl
                && UpdatePackage.IsSha256Digest(asset.digest)
                && asset.size > 0 && asset.size <= UpdatePackage.MaximumBytes
                ? asset
                : null;
        }

        internal void StartUpdate(Release release)
        {
            if (!TryGetReleaseVersion(release, out var expectedVersion) || expectedVersion <= Plugin.CurrentVersion)
                throw new InvalidOperationException("Select a newer stable PackTracker release.");
            var asset = GetUpdateAsset(release)
                ?? throw new InvalidOperationException("This release has no verifiable PackTracker update package.");
            if (Interlocked.CompareExchange(ref _updateStarted, 1, 0) != 0)
                throw new InvalidOperationException("A PackTracker update is already in progress.");

            string stage = null;
            try
            {
                stage = Path.GetFullPath(Path.Combine(Config.AppDataPath, "PackTracker", "updates", Guid.NewGuid().ToString("N")));
                var payload = Path.Combine(stage, "payload");
                Directory.CreateDirectory(payload);
                var zipPath = Path.Combine(stage, UpdatePackage.AssetName);
                Download(asset, zipPath);

                using (var zip = new ZipArchive(File.OpenRead(zipPath), ZipArchiveMode.Read))
                    UpdatePackage.Extract(zip, payload);
                UpdatePackage.ValidatePayload(payload, expectedVersion);
                File.Delete(zipPath);

                var requestPath = Path.Combine(stage, "update-request.json");
                using (var hdt = Process.GetCurrentProcess())
                using (var stream = File.Create(requestPath))
                {
                    var hdtExecutable = hdt.MainModule?.FileName;
                    UpdateRequest.ValidateHdtExecutable(hdtExecutable);
                    new DataContractJsonSerializer(typeof(UpdateRequest)).WriteObject(stream, new UpdateRequest
                    {
                        ProcessId = hdt.Id,
                        ProcessStartUtcTicks = hdt.StartTime.ToUniversalTime().Ticks,
                        HdtExecutable = hdtExecutable,
                        InstalledPlugin = typeof(Plugin).Assembly.Location,
                        ExpectedVersion = expectedVersion.ToString(4),
                    });
                }

                using var helper = Process.Start(new ProcessStartInfo(Path.Combine(payload, UpdatePackage.HelperFileName), $"\"{requestPath}\"")
                {
                    UseShellExecute = false,
                    WorkingDirectory = payload,
                });
                if (helper == null)
                    throw new InvalidOperationException("The staged PackTracker updater could not start.");
            }
            catch
            {
                Interlocked.Exchange(ref _updateStarted, 0);
                if (stage != null)
                {
                    try { Directory.Delete(stage, true); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
                throw;
            }
        }

        public Release GetLatestRelease()
        {
            try
            {
                return ReadReleaseData<Release>(ReleasesApiUrl + "/latest");
            }
            catch (WebException exception) when (
                exception.Response is HttpWebResponse response && response.StatusCode == HttpStatusCode.NotFound)
            {
                exception.Response.Dispose();
                return null;
            }
        }

        public IEnumerable<Release> GetAllReleases() => ReadReleaseData<List<Release>>(ReleasesApiUrl);

        private static T ReadReleaseData<T>(string uri)
        {
            var request = CreateRequest(uri);
            request.Accept = "application/vnd.github+json";
            request.Headers["X-GitHub-Api-Version"] = "2022-11-28";
            using var response = request.GetResponse();
            using var stream = response.GetResponseStream();
            return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);
        }

        private static void Download(Asset asset, string zipPath)
        {
            var request = CreateRequest(asset.browser_download_url);
            using (var response = request.GetResponse())
            {
                if (response.ResponseUri.Scheme != Uri.UriSchemeHttps
                    || (response.ContentLength >= 0 && response.ContentLength != asset.size))
                    throw new InvalidDataException("The update download did not match the published asset.");

                using var input = response.GetResponseStream();
                using var output = new FileStream(zipPath, FileMode.CreateNew);
                UpdatePackage.CopyBounded(input, output, asset.size);
            }

            using var package = File.OpenRead(zipPath);
            UpdatePackage.VerifyDigest(package, asset.digest);
        }

        private static HttpWebRequest CreateRequest(string uri)
        {
            var request = WebRequest.CreateHttp(uri);
            request.UserAgent = "PackTracker";
            request.Timeout = RequestTimeoutMilliseconds;
            request.ReadWriteTimeout = RequestTimeoutMilliseconds;
            return request;
        }
    }
}
