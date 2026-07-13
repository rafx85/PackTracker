using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System;

namespace PackTracker.View
{
    public class AverageCollection : INotifyCollectionChanged, IEnumerable<Average>, IDisposable
    {
        private ObservableCollection<Average> _statistics = new ObservableCollection<Average>();
        private readonly History _history;
        public event NotifyCollectionChangedEventHandler CollectionChanged;

        public AverageCollection(History History)
        {
            this._history = History;
            IEnumerable<int> PackIds = History.Select(x => x.Id).Distinct().OrderBy(x => x);
            foreach (var Id in PackIds)
            {
                this._statistics.Add(new Average(Id, History));
            }

            History.CollectionChanged += this.History_CollectionChanged;

            this._statistics.CollectionChanged += (sender, e) => CollectionChanged?.Invoke(this, e);
        }

        private void History_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (sender is History)
            {
                if (e.Action == NotifyCollectionChangedAction.Add)
                {
                    foreach (Entity.Pack Pack in e.NewItems)
                    {
                        if (!this._statistics.Any(x => x.Id == Pack.Id))
                        {
                            this.Add(new Average(Pack.Id, (History)sender));
                        }
                    }
                }
            }
        }

        private void Add(Average Statistic)
        {
            foreach (var Stat in this._statistics)
            {
                if (Statistic.Id < Stat.Id)
                {
                    this._statistics.Insert(this._statistics.IndexOf(Stat), Statistic);
                    return;
                }
            }

            this._statistics.Add(Statistic);
        }

        public IEnumerator<Average> GetEnumerator()
        {
            return this._statistics.GetEnumerator();

        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this._statistics.GetEnumerator();
        }

        public Average FindForPackId(int id)
        {
            return this._statistics.SingleOrDefault(x => x.Id == id);
        }

        public void Dispose()
        {
            this._history.CollectionChanged -= this.History_CollectionChanged;
            foreach (var statistic in this._statistics)
            {
                statistic.Dispose();
            }
        }
    }
}
