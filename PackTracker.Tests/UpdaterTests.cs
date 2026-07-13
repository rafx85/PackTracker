using PackTracker.Update;
using System;
using System.IO;
using System.IO.Compression;
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

        [Fact]
        public void ExtractSafelyRejectsParentDirectoryTraversal()
        {
            using (var directory = new TemporaryDirectory())
            using (var stream = new MemoryStream())
            {
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
                {
                    var entry = archive.CreateEntry("../outside.txt");
                    using (var writer = new StreamWriter(entry.Open()))
                    {
                        writer.Write("unsafe");
                    }
                }

                stream.Position = 0;
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    Assert.Throws<InvalidDataException>(() => Updater.ExtractSafely(archive, directory.Path));
                }

                Assert.False(File.Exists(Path.Combine(Path.GetDirectoryName(directory.Path), "outside.txt")));
            }
        }
    }
}
