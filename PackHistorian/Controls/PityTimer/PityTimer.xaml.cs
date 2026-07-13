using HearthDb.Enums;
using MahApps.Metro.Controls;
using PackTracker.View.Cache;

namespace PackTracker.Controls.PityTimer
{
    /// <summary>
    /// Interaktionslogik für PityTimer.xaml
    /// </summary>
    public partial class PityTimer : MetroWindow
    {
        private PityTimerRepository _pityTimers;

        public PityTimer(PackTracker.History History, PityTimerRepository PityTimers, PackTracker.Settings settings)
        {
            this.InitializeComponent();

            this._pityTimers = PityTimers;

            this.dd_Packs.SelectionChanged += (sender, e) =>
            {
                if (e.AddedItems.Count == 1)
                {
                    var packId = (int) e.AddedItems[0];
                    this.Ep_Prev.DataContext = this.Ep_Label.DataContext = this._pityTimers.GetPityTimer(packId, Rarity.EPIC, true);
                    this.Leg_Prev.DataContext = this.Leg_Label.DataContext = this._pityTimers.GetPityTimer(packId, Rarity.LEGENDARY, true);
                }
            };

            Loaded += (sender, e) => this.dd_Packs.DataContext = History;
            Closed += (sender, e) => this.dd_Packs.DataContext = null;
        }
    }
}
