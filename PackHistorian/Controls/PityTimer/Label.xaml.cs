using MahApps.Metro.Controls;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace PackTracker.Controls.PityTimer
{
    /// <summary>
    /// Interaktionslogik für Label.xaml
    /// </summary>
    public partial class Label : UserControl, INotifyPropertyChanged
    {
        private int _curr = 0;
        private int? _average;
        private bool _stillSyncing;
        private int? _packId = null;

        public int Current
        {
            get => this._curr;
            set
            {
                if (this._curr == value)
                {
                    return;
                }

                this.pt_Curr.Transition = value > this._curr ? TransitionType.Down : TransitionType.Up;
                this._curr = value;
                this.OnPropertyChanged("Current");
            }
        }

        public bool Popup { get; set; } = false;
        public string PopupText => this.GeneratePopupText();
        public int Limit { get; set; }
        public string RarityPlaceholder { get; set; } = null;

        public int? Average
        {
            get => this._average;
            private set
            {
                if (this._average == value)
                {
                    return;
                }

                this._average = value;
                this.OnPropertyChanged("Average");
            }
        }

        public Label()
        {
            this.InitializeComponent();
            this.Counters.DataContext = this;
        }

        private void This_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is View.PityTimer)
            {
                var pt = (View.PityTimer)e.OldValue;

                pt.PropertyChanged -= this.Pt_PropertyChanged;
            }

            if (e.NewValue is View.PityTimer)
            {
                var pt = (View.PityTimer)e.NewValue;

                this.Current = pt.Current;
                this.Average = pt.Average;
                pt.PropertyChanged += this.Pt_PropertyChanged;

                this._stillSyncing = pt.SkipFirst && pt.WaitForFirst;
                this._packId = pt.PackId;
            }
            else
            {
                this.Current = 0;
                this.Average = null;
                this._packId = null;
            }
        }

        private void Pt_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (!(sender is View.PityTimer))
            {
                return;
            }
            var pt = (View.PityTimer)sender;

            switch (e.PropertyName)
            {
                case "Current":
                    this.Current = pt.Current;
                    this._stillSyncing = pt.SkipFirst && pt.WaitForFirst;
                    break;
                case "Average":
                    this.Average = pt.Average;
                    break;
            }
        }

        private void btn_Popup_Click(object sender, RoutedEventArgs e)
        {
            this.Popup = true;
            this.OnPropertyChanged("PopupText");
            this.OnPropertyChanged("Popup");
        }

        private string GeneratePopupText()
        {
            var rarity = this.RarityPlaceholder ?? "a card of the respective rarity";
            var packName = this._packId != null ? View.PackNameConverter.Convert((int)this._packId) : "the selected set";
            var packWord = this._curr == 1 ? "pack" : "packs";
            var averageText = this.Average.HasValue ? this.Average.Value.ToString() : "–";

            var sb = new StringBuilder();
            sb.Append("Pity timer for ").AppendLine(rarity)
              .AppendLine();

            if (this._stillSyncing)
            {
                sb.AppendLine("Tracking is not synced yet.")
                  .Append(Plugin.NAME).Append(" has not seen ").Append(rarity).Append(" in ").Append(packName).AppendLine(" yet.")
                  .AppendLine("Keep opening packs normally. When one appears, the counter will reset to 0 and become accurate.")
                  .AppendLine();
            }
            else
            {
                sb.AppendLine("Tracking is synced.")
                  .Append(Plugin.NAME).Append(" has already seen ").Append(rarity).Append(" in ").Append(packName).AppendLine(".")
                  .AppendLine();
            }

            sb.Append("BIG NUMBER: ").Append(this._curr).AppendLine()
              .Append("You opened ").Append(this._curr).Append(' ').Append(packWord).Append(" without finding ").Append(rarity).AppendLine(".")
              .AppendLine()
              .Append("MIDDLE NUMBER: ").Append(averageText).AppendLine();

            if (this.Average.HasValue)
            {
                sb.Append("Your completed streaks average ").Append(this.Average.Value).Append(" packs without finding ").Append(rarity).AppendLine(".")
                  .AppendLine("This is the same value as the blue dashed line.");
            }
            else
            {
                sb.AppendLine("There are not enough completed streaks to calculate your personal average yet.");
            }

            sb.AppendLine()
              .Append("SMALL NUMBER: ").Append(this.Limit).AppendLine()
              .Append("This is the most packs you can miss in a row. If the big number reaches ").Append(this.Limit)
              .Append(", the next pack should contain ").Append(rarity).AppendLine(".")
              .AppendLine()
              .AppendLine("CHART:")
              .AppendLine("• Each bar is one streak without the card.")
              .AppendLine("• The faded right bar is your current streak.")
              .AppendLine("• Yellow line: the usual average, not a guarantee.")
              .AppendLine("• Red line: the maximum.")
              .Append("• Blue dashed line: your personal average after enough tracked streaks.");

            return sb.ToString();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string prop)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }
    }
}
