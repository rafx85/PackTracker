using HearthDb.Enums;
using Hearthstone_Deck_Tracker;
using Hearthstone_Deck_Tracker.Plugins;
using Hearthstone_Deck_Tracker.Utility.Toasts;
using Hearthstone_Deck_Tracker.Enums.Hearthstone;
using PackTracker.Storage;
using PackTracker.Update;
using System;
using System.ComponentModel;
using System.Windows.Controls;

namespace PackTracker
{
    public class Plugin : IPlugin
    {
        private const string _name = "Pack Tracker";
        private PackWatcher _watcher;
        private Updater _updater;
        private History _history;
        private IHistoryStorage _historyStorage = new XmlHistory();
        private Settings _settings;
        private ISettingsStorage _settingsStorage = new SettingsStorage();
        private WindowManager _windows = new WindowManager(_name);
        private View.AverageCollection _averageCollection;
        private View.Cache.PityTimerRepository _pityTimers;
        private static readonly object ModeHandlerLock = new object();
        private static WeakReference<PackWatcher> _activeWatcher;
        private static bool _modeHandlerRegistered;

        public static Version CurrentVersion { get; } = new Version("1.4.28");

        public Plugin()
        {
            this._watcher = new PackWatcher();
            this._updater = new Updater();

            try
            {
                this._history = this._historyStorage.Fetch();
            }
            catch
            {
                this._history = new History();
            }
            this._averageCollection = new View.AverageCollection(this._history);

            try
            {
                this._settings = this._settingsStorage.Fetch() ?? new Settings();
            }
            catch
            {
                this._settings = new Settings();
            }

            this._pityTimers = new View.Cache.PityTimerRepository(this._history, this._settings);

            //watcher
            this._watcher.PackOpened += (sender, e) =>
            {
                this._history.Add(e.Pack);
                this._historyStorage.Store(this._history.Ascending);

                if (this._settings.Spoil)
                {
                    var Average = this._averageCollection.FindForPackId(e.Pack.Id);
                    ToastManager.ShowCustomToast(new Controls.Toast(e.Pack, Average));
                }
            };

            this._watcher.PackScreenEntered += (sender, e) =>
            {
                if (this._settings.PityOverlay)
                {
                    this._windows.ShowPityTimerOverlay(this._history, this._pityTimers, this._settings.RightmostPityTimerOverlay);
                }
            };
            this._watcher.PackScreenLeft += (sender, e) => this._windows.ClosePityTimerOverlay();
        }

        public string Author => "DBqFetti <dbqfetti@gmail.com>";

        public string ButtonText => "Settings";

        public string Description => this.Name + " is a plugin that allows you to keep an eye on every pack you open. This allows you to see how many cards of different rarities have dropped over time and also enables you to estimate when your next Epic or Legendary is coming!";

        public MenuItem MenuItem
        {
            get
            {
                var Menu = new Controls.Menu();
                Menu.mnu_History.Click += (sender, e) => this._windows.ShowHistoryWin(this._history);
                Menu.mnu_Statistic.Click += (sender, e) => this._windows.ShowStatisticWin(this._settings, this._history);
                Menu.mnu_Log.Click += (sender, e) => this._windows.ShowLogWin(this._history);
                Menu.mnu_Search.Click += (sender, e) => this._windows.ShowSearchWin(this._history);
                Menu.mnu_PityTimers.Click += (sender, e) => this._windows.ShowPityWin(this._settings, this._history, this._pityTimers);
                Menu.mnu_ManualInsert.Click += (sender, e) => this._windows.ShowManualInsertWin(this._history);
                return Menu;
            }
        }

        public string Name => _name;
        public static string NAME => _name;

        public Version Version => CurrentVersion;

        public void OnButtonPress()
        {
            this._windows.ShowSettingsWin(this._settings, this._settingsStorage);
        }

        public void OnLoad()
        {
            lock (ModeHandlerLock)
            {
                _activeWatcher = new WeakReference<PackWatcher>(this._watcher);
                if (!_modeHandlerRegistered)
                {
                    Hearthstone_Deck_Tracker.API.GameEvents.OnModeChanged.Add(HandleModeChanged);
                    _modeHandlerRegistered = true;
                }
            }
            this._watcher.Start();

            if (this._settings.Update)
            {
                var bwCheck = new BackgroundWorker();
                bwCheck.DoWork += (sender, e) => e.Result = this._updater.NewVersionAvailable();
                bwCheck.RunWorkerCompleted += (sender, e) =>
                {
                    if (e.Error == null && (bool?)e.Result == true)
                    {
                        this._windows.ShowSettingsWin(this._settings, this._settingsStorage, typeof(Controls.Settings.Update));
                    }
                };
                bwCheck.RunWorkerAsync();
            }
        }

        public void OnUnload()
        {
            lock (ModeHandlerLock)
            {
                if (_activeWatcher != null
                    && _activeWatcher.TryGetTarget(out var activeWatcher)
                    && ReferenceEquals(activeWatcher, this._watcher))
                {
                    _activeWatcher = null;
                }
            }
            this._watcher.Stop();
            this._windows.CloseAll();
            this._pityTimers.Dispose();
            this._averageCollection.Dispose();
        }

        public void OnUpdate()
        {
        }

        private static void HandleModeChanged(Mode mode)
        {
            WeakReference<PackWatcher> watcherReference;
            lock (ModeHandlerLock)
            {
                watcherReference = _activeWatcher;
            }

            if (watcherReference != null && watcherReference.TryGetTarget(out var watcher))
            {
                watcher.HandleMode(mode);
            }
        }

        public static HearthDb.Enums.Locale GetLocale()
        {
            dynamic config = Config.Instance;
            try
            {
                // SelectedLanguage will be removed
                if (Enum.TryParse(config.SelectedLanguage, out Locale cardLang))
                {
                    return cardLang;
                }
            }
            catch
            {
            }

            try
            {
                // LastSeenHearthstoneLang is the replacement
                // internally used by Helper.GetCardLanguage()
                if (Enum.TryParse(config.LastSeenHearthstoneLang, out Locale cardLang))
                {
                    return cardLang;
                }
            }
            catch
            {
            }

            try
            {
                // no card language found, fallback to HDT UI language
                if (Enum.TryParse(config.Localization?.ToString(), out Locale cardLang))
                {
                    return cardLang;
                }
            }
            catch
            {
            }

            return HearthDb.Enums.Locale.enUS;
        }
    }
}
