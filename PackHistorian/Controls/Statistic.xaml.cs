using System.Collections.Generic;
using System.Linq;

namespace PackTracker.Controls
{
    /// <summary>
    /// Interaktionslogik für Statistic.xaml
    /// </summary>
    public partial class Statistic
    {
        public static Dictionary<int, int> obtained = new Dictionary<int, int>();
        private readonly Dictionary<int, View.Statistic> _statistics = new Dictionary<int, View.Statistic>();

        public Statistic(PackTracker.History History, PackTracker.Settings settings)
        {
            this.InitializeComponent();

            this.dd_Packs.SelectionChanged += (sender, e) =>
            {
                if (e.AddedItems.Count == 1)
                {
                    var selection = (int)e.AddedItems[0];
                    if (!this._statistics.ContainsKey(selection))
                    {
                        this._statistics.Add(selection, new View.Statistic(selection, History));
                    }

                    this.dp_Statistic.DataContext = this._statistics[selection];
                }
                else
                {
                    this.dp_Statistic.DataContext = null;
                }
            };

            Loaded += (sender, e) => this.dd_Packs.DataContext = History;
            Closed += (sender, e) =>
            {
                this.dd_Packs.DataContext = null;
                foreach (var statistic in this._statistics.Values)
                {
                    statistic.Dispose();
                }
                this._statistics.Clear();
            };
            this.dd_Packs.Focus();
        }
    }
}
