using HearthDb.Enums;
using Hearthstone_Deck_Tracker;
using PackTracker.Entity;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;

namespace PackTracker
{
    internal class IndexRepository : IDisposable
    {
        private readonly StringBuilder _sb = new StringBuilder();
        private Dictionary<string, string> _index = new Dictionary<string, string>();                       //<searchstring, id>
        private Dictionary<string, List<Index>> _indexObjects = new Dictionary<string, List<Index>>();      //<id, index-objects>
        private readonly History _history;

        public IndexRepository(History History)
        {
            this._history = History;
            foreach (var Pack in History)
            {
                foreach (var Card in Pack.Cards)
                {
                    this.Add(new Index(Card, Pack.Time));
                }
            }

            History.CollectionChanged += this.History_CollectionChanged;
        }

        private void History_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                foreach (Pack Pack in e.NewItems)
                {
                    foreach (var Card in Pack.Cards)
                    {
                        this.Add(new Index(Card, Pack.Time));
                    }
                }
            }
        }

        private void Add(Index Index)
        {
            if (!this._index.ContainsValue(Index.Card.HDTCard.Id))
            {
                var DbCard = HearthDb.Cards.GetFromDbfId(Index.Card.HDTCard.DbfId);
                if (DbCard == null)
                {
                    return;
                }

                var name = DbCard.Name?.ToLower();
                var text = DbCard.Text?.ToLower();

                var lang = Plugin.GetLocale();
                var locName = DbCard.GetLocName(lang)?.ToLower();
                var locText = DbCard.GetLocText(lang)?.ToLower();

                this._sb.Append(locName).Append(name).Append(locText).Append(text);
                this._index.Add(this._sb.ToString(), Index.Card.HDTCard.Id);
                this._sb.Clear();

                this._indexObjects.Add(Index.Card.HDTCard.Id, new List<Index>());
            }

            this._indexObjects[Index.Card.HDTCard.Id].Add(Index);
        }

        public IEnumerable<Index> Find(string searchString)
        {
            var elems = searchString.Split(' ');
            var Filtered = this._index.Where(x => x.Key.Contains(elems[0]));

            foreach (var elem in elems.Skip(1))
            {
                Filtered = Filtered.Where(x => x.Key.Contains(elem));
            }

            var Result = new List<Index>();
            foreach (var Index in this._indexObjects.Where(x => Filtered.Select(y => y.Value).Contains(x.Key)).Select(x => x.Value))
            {
                Result.AddRange(Index);
            }

            return Result.Distinct();
        }

        public void Dispose()
        {
            this._history.CollectionChanged -= this.History_CollectionChanged;
        }
    }
}
