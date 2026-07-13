using HearthDb.Enums;
using PackTracker.Controls;
using Xunit;

namespace PackTracker.Tests
{
    public class PackDropDownTests
    {
        [Fact]
        public void SortPackIdsByName_UsesLocalizedPackNameInsteadOfNumericId()
        {
            var result = PackDropDown.SortPackIdsByName(new[] { 1048, 1047, 1030 }, Locale.enUS);

            Assert.Equal(new[] { 1030, 1047, 1048 }, result);
        }

        [Theory]
        [InlineData(1047, "violet")]
        [InlineData(1048, "golden")]
        [InlineData(1048, "1048")]
        public void MatchesSearch_FindsPackByNameOrId(int packId, string searchText)
        {
            Assert.True(PackDropDown.MatchesSearch(packId, searchText, Locale.enUS));
        }

        [Fact]
        public void MatchesSearch_RejectsUnrelatedName()
        {
            Assert.False(PackDropDown.MatchesSearch(1047, "cataclysm", Locale.enUS));
        }
    }
}
