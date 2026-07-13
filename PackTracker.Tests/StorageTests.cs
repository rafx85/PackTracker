using PackTracker.Storage;
using System.IO;
using Xunit;

namespace PackTracker.Tests
{
    public class StorageTests
    {
        [Fact]
        public void AtomicFileReplacesExistingContent()
        {
            using (var directory = new TemporaryDirectory())
            {
                var path = Path.Combine(directory.Path, "data.txt");
                AtomicFile.WriteAllText(path, "first");
                AtomicFile.WriteAllText(path, "second");

                Assert.Equal("second", File.ReadAllText(path));
                Assert.Empty(Directory.GetFiles(directory.Path, "*.tmp"));
            }
        }

        [Fact]
        public void NullJsonSettingsFallBackToDefaults()
        {
            using (var directory = new TemporaryDirectory())
            {
                var settingsDirectory = Path.Combine(directory.Path, "PackTracker");
                Directory.CreateDirectory(settingsDirectory);
                File.WriteAllText(Path.Combine(settingsDirectory, "Settings.json"), "null");

                var settings = new SettingsStorage(directory.Path).Fetch();

                Assert.NotNull(settings);
                Assert.True(settings.PityOverlay);
                Assert.True(settings.Update);
            }
        }

        [Fact]
        public void SettingsRoundTripThroughAtomicStorage()
        {
            using (var directory = new TemporaryDirectory())
            {
                var storage = new SettingsStorage(directory.Path);
                storage.Store(new Settings { Spoil = true, Update = false });

                var settings = storage.Fetch();

                Assert.True(settings.Spoil);
                Assert.False(settings.Update);
            }
        }

        [Fact]
        public void MalformedHistoryIsBackedUpBeforeReturningEmptyHistory()
        {
            using (var directory = new TemporaryDirectory())
            {
                var historyDirectory = Path.Combine(directory.Path, "PackTracker");
                Directory.CreateDirectory(historyDirectory);
                var historyPath = Path.Combine(historyDirectory, "History.xml");
                const string malformed = "<history><pack";
                File.WriteAllText(historyPath, malformed);

                var storage = new XmlHistory(directory.Path, id => null);
                var history = storage.Fetch();

                Assert.Equal(0, history.Count);
                var backup = Assert.Single(Directory.GetFiles(historyDirectory, "History_backup_*.xml"));
                Assert.Equal(malformed, File.ReadAllText(backup));
                Assert.Equal(malformed, File.ReadAllText(historyPath));

                storage.Store(new History());

                Assert.Contains("<history", File.ReadAllText(historyPath));
                Assert.Equal(malformed, File.ReadAllText(backup));
            }
        }
    }
}
