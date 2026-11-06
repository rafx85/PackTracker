using PackTracker.Update;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using Xunit;

namespace PackTracker.Tests
{
    public class UpdaterTests
    {
        [Fact]
        public void ReleaseFeedUsesMaintainedFork()
        {
            Assert.Equal("https://api.github.com/repos/rafx85/PackTracker/releases", Updater.ReleasesApiUrl);
        }

        [Theory]
        [InlineData("v1.4.26", 1, 4, 26)]
        [InlineData("release-35.0.3", 35, 0, 3)]
        public void ParseVersionExtractsNumericVersion(string tag, int major, int minor, int build)
        {
            Assert.Equal(new Version(major, minor, build), Updater.ParseVersion(tag));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("release")]
        public void ParseVersionRejectsInvalidTags(string tag)
        {
            Assert.Throws<FormatException>(() => Updater.ParseVersion(tag));
        }

        [Theory]
        [InlineData("https://github.com/rafx85/PackTracker/releases/download/v2.0.0/PackTracker.zip", true)]
        [InlineData("https://github.com/rafx85/DustFinder/releases/download/v2.0.0/PackTracker.zip", false)]
        [InlineData("https://github.com/rafx85/PackTracker/releases/download/v1.4.28/PackTracker.zip", false)]
        [InlineData("https://example.com/PackTracker.zip", false)]
        public void UpdateAssetMustBelongToTheSelectedRelease(string url, bool trusted)
        {
            var asset = new Asset { name = UpdatePackage.AssetName, browser_download_url = url, digest = "sha256:" + new string('a', 64), size = 100 };
            var release = new Release { tag_name = "v2.0.0", assets = new List<Asset> { asset } };
            Assert.Equal(trusted ? asset : null, Updater.GetUpdateAsset(release));

            asset.digest = null;
            Assert.Null(Updater.GetUpdateAsset(release));
        }

        [Theory]
        [InlineData("v2.0.0-rc1", false, false, false)]
        [InlineData("v2.0.0", true, false, false)]
        [InlineData("v2.0.0", false, true, false)]
        [InlineData("v2.0.0", false, false, true)]
        public void OnlyStableReleaseVersionsCanBeInstalled(string tag, bool draft, bool prerelease, bool valid)
        {
            var release = new Release { tag_name = tag, draft = draft, prerelease = prerelease };
            Assert.Equal(valid, Updater.TryGetReleaseVersion(release, out var version));
            if (valid)
                Assert.Equal(new Version(2, 0, 0, 0), version);
        }

        [Fact]
        public void DigestVerificationRejectsTamperedPackage()
        {
            const string digest = "sha256:2cf24dba5fb0a30e26e83b2ac5b9e29e1b161e5c1fa7425e73043362938b9824";
            using var package = new MemoryStream(Encoding.UTF8.GetBytes("hello"));
            UpdatePackage.VerifyDigest(package, digest);
            package.Position = 0;
            package.WriteByte(0);
            package.Position = 0;
            Assert.Throws<InvalidDataException>(() => UpdatePackage.VerifyDigest(package, digest));
        }

        [Theory]
        [InlineData("../outside.txt")]
        [InlineData("unrelated.dll")]
        [InlineData("packtracker.DLL")]
        public void ExtractRejectsUnexpectedFilesBeforeWriting(string extraEntry)
        {
            using var directory = new TemporaryDirectory();
            using var stream = new MemoryStream();
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
            {
                foreach (var name in new[] { UpdatePackage.PluginFileName, UpdatePackage.HelperFileName, extraEntry })
                {
                    using var file = archive.CreateEntry(name).Open();
                    file.WriteByte(1);
                }
            }

            stream.Position = 0;
            using var update = new ZipArchive(stream, ZipArchiveMode.Read);
            Assert.Throws<InvalidDataException>(() => UpdatePackage.Extract(update, directory.Path));
            Assert.Empty(Directory.GetFiles(directory.Path));
            Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(directory.Path), "outside.txt")));
        }
    }
}
