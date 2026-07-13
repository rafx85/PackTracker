using HearthDb.Enums;
using System.Collections.Generic;
using System.Linq;
using System;

namespace PackTracker.View.Cache
{
    public class PityTimerRepository : IDisposable
    {
        private History _history;
        private Settings _settings;
        private List<PityTimer> _cache = new List<PityTimer>();

        public PityTimerRepository(History History, Settings settings)
        {
            this._history = History;
            this._settings = settings;
        }

        public PityTimer GetPityTimer(int packId, Rarity rarity, bool skipFirst)
        {
            var premium = ManualPackInsert.GoldenPacks.Contains(packId);
            var pt = this._cache.FirstOrDefault(x => x.PackId == packId && x.Rarity == rarity && x.Premium == premium && x.SkipFirst == skipFirst);
            if (!(pt is PityTimer))
            {
                pt = new PityTimer(this._history, packId, rarity, premium, skipFirst, this._settings);
                this._cache.Add(pt);
            }

            return pt;
        }

        public void Dispose()
        {
            foreach (var pityTimer in this._cache)
            {
                pityTimer.Dispose();
            }
            this._cache.Clear();
        }
    }
}
