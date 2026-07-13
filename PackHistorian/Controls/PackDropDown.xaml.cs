using HearthDb.Enums;
using PackTracker.Entity;
using PackTracker.View;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace PackTracker.Controls
{
    public partial class PackDropDown : ComboBox
    {
        private readonly ObservableCollection<int> _dropDown;
        private readonly List<int> _allPackTypes;
        private readonly Locale _locale;
        private ICollectionView _dropDownView;
        private TextBox _searchBox;
        private string _searchText = string.Empty;
        private bool _settingDisplayText;
        private bool _openingFromSearch;
        private int? _lastSelectedId;

        public PackDropDown()
        {
            this.InitializeComponent();

            this._locale = Plugin.GetLocale();
            this._allPackTypes = new List<int>(PackNameConverter.PackNames.Keys);
            this._dropDown = new ObservableCollection<int>();
            this._dropDownView = CollectionViewSource.GetDefaultView(this._dropDown);
            this._dropDownView.Filter = this.FilterPack;
            this.ItemsSource = this._dropDownView;
        }

        public override void OnApplyTemplate()
        {
            if (this._searchBox != null)
            {
                this._searchBox.TextChanged -= this.SearchBox_TextChanged;
            }

            base.OnApplyTemplate();

            this._searchBox = this.GetTemplateChild("PART_EditableTextBox") as TextBox;
            if (this._searchBox != null)
            {
                this._searchBox.TextChanged += this.SearchBox_TextChanged;
                this.SetSelectedPackText();
            }
        }

        private void dd_Packs_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is PackTracker.History history)
            {
                history.CollectionChanged -= this.DropDown_NewEntry;
            }

            if (e.NewValue is PackTracker.History newhist)
            {
                this.SetAvailablePacks(this._allPackTypes.Intersect(newhist.Select(p => p.Id).Concat(Statistic.obtained.Keys)));
                newhist.CollectionChanged += this.DropDown_NewEntry;
            }
            else
            {
                this.SetAvailablePacks(Enumerable.Empty<int>());
            }
        }

        private void DropDown_NewEntry(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action != NotifyCollectionChangedAction.Add || e.NewItems == null)
            {
                return;
            }

            var newPackIds = e.NewItems.Cast<Pack>()
                .Select(pack => pack.Id)
                .Where(this._allPackTypes.Contains)
                .ToList();

            this.SetAvailablePacks(this._dropDown.Concat(newPackIds));
            if (newPackIds.Count > 0)
            {
                this.ClearSearch();
                this.SelectedItem = newPackIds[newPackIds.Count - 1];
            }
        }

        private void SetAvailablePacks(IEnumerable<int> packIds)
        {
            int? selectedId = this.SelectedItem is int id ? id : (int?)null;
            var sortedIds = SortPackIdsByName(packIds.Distinct(), this._locale);

            this._dropDown.Clear();
            foreach (var packId in sortedIds)
            {
                this._dropDown.Add(packId);
            }

            this._dropDownView.Refresh();
            if (selectedId.HasValue && this._dropDown.Contains(selectedId.Value))
            {
                this.SelectedItem = selectedId.Value;
            }
        }

        private bool FilterPack(object item)
        {
            return item is int id && MatchesSearch(id, this._searchText, this._locale);
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (this._settingDisplayText)
            {
                return;
            }

            this._searchText = this._searchBox.Text;
            this._dropDownView.Refresh();

            if (!this.IsDropDownOpen)
            {
                this._openingFromSearch = true;
                this.IsDropDownOpen = true;
                this._openingFromSearch = false;
            }
        }

        private void dd_Packs_DropDownOpened(object sender, EventArgs e)
        {
            if (this._openingFromSearch)
            {
                return;
            }

            this.ClearSearch();
            this.Dispatcher.BeginInvoke(new Action(() => this._searchBox?.SelectAll()), DispatcherPriority.Input);
        }

        private void dd_Packs_DropDownClosed(object sender, EventArgs e)
        {
            this.ClearSearch();
            if (!(this.SelectedItem is int) && this._lastSelectedId.HasValue && this._dropDown.Contains(this._lastSelectedId.Value))
            {
                this.SelectedItem = this._lastSelectedId.Value;
            }

            this.SetSelectedPackText();
        }

        protected override void OnSelectionChanged(SelectionChangedEventArgs e)
        {
            base.OnSelectionChanged(e);
            if (this.SelectedItem is int id)
            {
                this._lastSelectedId = id;
                this.ClearSearch();
                this.SetSelectedPackText();
            }
        }

        private void ClearSearch()
        {
            this._searchText = string.Empty;
            this._dropDownView?.Refresh();
        }

        private void SetSelectedPackText()
        {
            if (this._searchBox == null || !(this.SelectedItem is int id))
            {
                return;
            }

            this._settingDisplayText = true;
            this._searchBox.Text = GetPackDisplayName(id, this._locale);
            this._searchBox.CaretIndex = this._searchBox.Text.Length;
            this._settingDisplayText = false;
        }

        private void dd_Packs_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (this.IsDropDownOpen)
            {
                return;
            }

            if (e.Delta > 0)
            {
                if (this.SelectedIndex > 0)
                {
                    this.SelectedIndex--;
                }
            }
            else if (this.SelectedIndex < this.Items.Count - 1)
            {
                this.SelectedIndex++;
            }
        }

        internal static IReadOnlyList<int> SortPackIdsByName(IEnumerable<int> packIds, Locale locale)
        {
            return packIds
                .OrderBy(id => GetPackDisplayName(id, locale), StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(id => id)
                .ToList();
        }

        internal static bool MatchesSearch(int packId, string searchText, Locale locale)
        {
            return string.IsNullOrWhiteSpace(searchText)
                || GetPackDisplayName(packId, locale).IndexOf(searchText.Trim(), StringComparison.CurrentCultureIgnoreCase) >= 0;
        }

        internal static string GetPackDisplayName(int packId, Locale locale)
        {
            return PackNameConverter.Convert(packId, locale) ?? packId.ToString(CultureInfo.InvariantCulture);
        }
    }
}
