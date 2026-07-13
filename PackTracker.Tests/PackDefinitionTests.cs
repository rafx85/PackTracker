using HearthDb.Enums;
using PackTracker.View;
using System;
using System.Collections;
using System.Reflection;
using Xunit;

namespace PackTracker.Tests
{
    public class PackDefinitionTests
    {
        [Fact]
        public void PluginVersionMatchesReleaseVersion()
        {
            Assert.Equal(new Version(1, 4, 28), Plugin.CurrentVersion);
        }

        [Theory]
        [InlineData(1047, "Escape from Violet Hold (1047)")]
        [InlineData(1048, "Golden Escape from Violet Hold (1048)")]
        public void VioletHoldPackNamesAreRegistered(int packId, string expected)
        {
            Assert.Equal(expected, PackNameConverter.Convert(packId, Locale.enUS));
        }

        [Fact]
        public void GoldenVioletHoldPackIsRegistered()
        {
            Assert.Contains(1048, ManualPackInsert.GoldenPacks);
        }

        [Theory]
        [InlineData(1047)]
        [InlineData(1048)]
        public void VioletHoldManualInsertFiltersAreRegistered(int packId)
        {
            var field = typeof(ManualPackInsert).GetField("_filter", BindingFlags.NonPublic | BindingFlags.Static);
            var filters = Assert.IsAssignableFrom<IDictionary>(field.GetValue(null));

            Assert.True(filters.Contains(packId));
        }
    }
}
