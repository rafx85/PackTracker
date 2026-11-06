using HearthDb.Enums;
using PackTracker.View;
using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using Xunit;

namespace PackTracker.Tests
{
    public class PackDefinitionTests
    {
        [Fact]
        public void PluginVersionMatchesFileVersion()
        {
            var fileVersion = FileVersionInfo.GetVersionInfo(typeof(Plugin).Assembly.Location).FileVersion;
            Assert.Equal(Version.Parse(fileVersion), Plugin.CurrentVersion);
        }

        [Theory]
        [InlineData(1047, "Escape from Violet Hold (1047)")]
        [InlineData(1048, "Golden Escape from Violet Hold (1048)")]
        [InlineData(1057, "Reign of the Black Empire (1057)")]
        [InlineData(1058, "Golden Reign of the Black Empire (1058)")]
        [InlineData(1075, "Reign of the Black Empire Catch-up (1075)")]
        public void ExpansionPackNamesAreRegistered(int packId, string expected)
        {
            Assert.Equal(expected, PackNameConverter.Convert(packId, Locale.enUS));
        }

        [Theory]
        [InlineData(1048)]
        [InlineData(1058)]
        public void GoldenExpansionPackIsRegistered(int packId)
        {
            Assert.Contains(packId, ManualPackInsert.GoldenPacks);
        }

        [Theory]
        [InlineData(1047)]
        [InlineData(1048)]
        [InlineData(1057)]
        [InlineData(1058)]
        [InlineData(1075)]
        public void ExpansionManualInsertFiltersAreRegistered(int packId)
        {
            var field = typeof(ManualPackInsert).GetField("_filter", BindingFlags.NonPublic | BindingFlags.Static);
            var filters = Assert.IsAssignableFrom<IDictionary>(field.GetValue(null));

            Assert.True(filters.Contains(packId));
        }
    }
}
