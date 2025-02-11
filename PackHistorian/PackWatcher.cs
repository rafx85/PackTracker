using HearthMirror.Objects;
using Hearthstone_Deck_Tracker.Enums.Hearthstone;
using Hearthstone_Deck_Tracker.Hearthstone;
using HearthWatcher.EventArgs;
using PackTracker.Entity;
using PackTracker.Event;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;

namespace PackTracker
{
    internal delegate void PackOpenedEventHandler(object sender, PackOpenedEventArgs e);

    internal class PackWatcher
    {
        private List<Process> _hearthstones = new List<Process>();

        public bool Running { get; private set; } = false;

        public event PackOpenedEventHandler PackOpened;
        public event EventHandler PackScreenEntered;
        public event EventHandler PackScreenLeft;

        private void NewPack(object sender, PackEventArgs e)
        {
            var Time = DateTime.Now;
            var Cards = new List<Entity.Card>();

            foreach (var Card in e.Cards)
            {
                var cardFromId = Database.GetCardFromId(Card.Id);
                bool isPremium;
                try
                {
                    isPremium = ((dynamic)Card).Premium;
                }
                catch
                {
                    isPremium = ((dynamic)Card).PremiumType != 0;
                }
                Cards.Add(new Entity.Card(cardFromId, isPremium));
            }

            this.OnPackOpened(new Pack(e.PackId, Time, Cards));
        }

        private void OnPackOpened(Pack Pack)
        {
            PackOpened?.Invoke(this, new PackOpenedEventArgs(Pack));
        }

        internal void HandleMode(Mode Mode)
        {
            if (!this.Running)
            {
                return;
            }

            // fixed: LETTUCE_PACK_OPENING (28) is not defined by HDT
            if (Mode == Mode.PACKOPENING || Mode == Mode.LETTUCE_PACK_OPENING)
            {
                foreach (var hs in Process.GetProcessesByName("Hearthstone"))
                {
                    if (hs is Process && !this._hearthstones.Contains(hs))
                    {
                        hs.EnableRaisingEvents = true;
                        hs.Exited += this.Hs_Exited;
                        this._hearthstones.Add(hs);
                    }
                }

                PackScreenEntered.Invoke(this, new EventArgs());
            }
            else
            {
                if (this._hearthstones.Count > 0)
                {
                    this._hearthstones.ForEach(x => { x.Exited -= this.Hs_Exited; x.EnableRaisingEvents = false; });
                    this._hearthstones.Clear();

                    PackScreenLeft?.Invoke(this, new EventArgs());
                }
            }
        }

        private void Hs_Exited(object sender, EventArgs e)
        {
            if (sender is Process hs)
            {
                if (this._hearthstones.Contains(hs))
                {
                    hs.Exited -= this.Hs_Exited;
                    hs.EnableRaisingEvents = false;
                    this._hearthstones.Remove(hs);
                }
            }

            if (this._hearthstones.Count == 0)
            {
                PackScreenLeft?.Invoke(this, new EventArgs());
            }
        }

        public void Start()
        {
            if (!this.Running)
            {
                Watchers.PackWatcher.NewPackEventHandler += this.NewPack;
                this.Running = true;
            }
        }

        public void Stop()
        {
            if (this.Running)
            {
                Watchers.PackWatcher.NewPackEventHandler -= this.NewPack;
                this._hearthstones.ForEach(x => { x.Exited -= this.Hs_Exited; x.EnableRaisingEvents = false; });
                this._hearthstones.Clear();
                this.Running = false;
            }
        }

        public static object GetNetCacheService(string serviceName)
        {
            var monoObject = typeof(HearthMirror.Reflection).Invoke("GetService", "NetCache")
                .HearthMirrorGet("m_netCache")?.HearthMirrorGet("valueSlots");

            if (monoObject is not object[] ie)
            {
                return null;
            }

            foreach (var item in ie)
            {
                if (item?.ValueOf("Class")?.ValueOf("Name")?.ToString() == serviceName)
                {
                    return item;
                }
            }
            return null;
        }

        public static void UpdateGranted()
        {
            try
            {
                var boosterServices = GetNetCacheService("NetCacheBoosters");
                var stacks = boosterServices?.HearthMirrorGet("BoosterStacks");
                var items = stacks?.HearthMirrorGet("_items");

                if (items is not object[] array)
                {
                    return;
                }

                var newdict = new Dictionary<int, int>();
                try
                {
                    foreach (var item in array)
                    {
                        try
                        {
                            if (item != null)
                            {
                                var id = (int) item.HearthMirrorGet("Id");
                                var obtained = (int) item.HearthMirrorGet("EverGrantedCount");
                                newdict.Add(id, obtained);
                            }
                        }
                        catch
                        {
                        }
                    }
                }
                catch
                {
                }

                foreach (var kvp in newdict)
                {
                    if (kvp.Value > 0)
                    {
                        Controls.Statistic.obtained[kvp.Key] = kvp.Value;
                    }
                    else if (Controls.Statistic.obtained.ContainsKey(kvp.Key))
                    {
                        Controls.Statistic.obtained.Remove(kvp.Key);
                    }
                }
            }
            catch
            {
                // Game not launched, failed, etc.
                return;
            }
        }
    }
}
