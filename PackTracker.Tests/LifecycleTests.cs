using PackTracker.Entity;
using PackTracker.View;
using System;
using System.Linq;
using Xunit;

namespace PackTracker.Tests
{
    public class LifecycleTests
    {
        [Fact]
        public void DisposedAverageStopsObservingHistory()
        {
            var history = new History();
            var average = new Average(1, history);
            history.Add(new Pack(1, DateTime.UtcNow, Enumerable.Empty<Card>()));
            Assert.Equal(1, average.CurrentEpic);

            average.Dispose();
            history.Add(new Pack(1, DateTime.UtcNow.AddMinutes(1), Enumerable.Empty<Card>()));

            Assert.Equal(1, average.CurrentEpic);
        }

        [Fact]
        public void DisposedAverageCollectionStopsCreatingEntries()
        {
            var history = new History();
            var averages = new AverageCollection(history);
            averages.Dispose();

            history.Add(new Pack(42, DateTime.UtcNow, Enumerable.Empty<Card>()));

            Assert.Empty(averages);
        }
    }
}
