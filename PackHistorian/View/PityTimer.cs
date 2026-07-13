using HearthDb.Enums;
using PackTracker.Entity;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace PackTracker.View
{
    public class PityTimer : INotifyPropertyChanged, IDisposable
    {
        public int PackId { get; }
        public Rarity Rarity { get; }
        public bool Premium { get; }
        public bool SkipFirst { get; }
        public bool WaitForFirst { get; private set; }
        private Settings _settings;
        private readonly History _history;

        public int Current { get; private set; } = 0;
        public ObservableCollection<int> Prev { get; } = new ObservableCollection<int>();
        public int? Average => this.Prev.Count > 0 ? (int?)Math.Round(this.Prev.Average(), 0) : null;

        public PityTimer(History History, int packId, Rarity rarity, bool premium, bool skipFirst, Settings settings)
        {
            this.PackId = packId;
            this.Rarity = rarity;
            this.Premium = premium;
            this.SkipFirst = this.WaitForFirst = skipFirst;
            this._settings = settings;
            this._history = History;

            foreach (var Pack in History)
            {
                this.AddPack(Pack);
            }

            History.CollectionChanged += this.History_CollectionChanged;
        }

        private void History_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                foreach (Pack Pack in e.NewItems)
                {
                    this.AddPack(Pack);
                }
            }
        }

        private void AddPack(Pack Pack)
        {
            if (Pack.Id != this.PackId)
            {
                return;
            }

            if (this.Condition(Pack))
            {
                var newCurr = this.Current;
                this.Current = 0;

                if (this.WaitForFirst)
                {
                    this.WaitForFirst = false;
                }
                else
                {
                    this.Prev.Add(newCurr);
                    this.OnPropertyChanged("Average");
                }
            }
            else
            {
                this.Current++;
            }

            this.OnPropertyChanged("Current");
        }

        private bool Condition(Pack Pack)
        {
            // this.Premium is true for all usage right now
            // but let's keep the logic here so it works for the future where !this.Premium
            return Pack.Cards.Any(x => x.Rarity == this.Rarity && (this._settings.GoldenResetRegularPityTimer || this.Premium == x.Premium));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string prop)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }

        public void Dispose()
        {
            this._history.CollectionChanged -= this.History_CollectionChanged;
        }
    }
}
