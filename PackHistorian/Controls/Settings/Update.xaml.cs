using MahApps.Metro.Controls;
using PackTracker.Update;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Timers;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace PackTracker.Controls.Settings
{
    /// <summary>
    /// Interaktionslogik für Update.xaml
    /// </summary>
    public partial class Update : MetroContentControl, ITitledElement
    {
        private Updater _updater;
        private Timer _timer;
        private Release _availableRelease;

        public string Title => "Update";

        public Update(PackTracker.Settings Settings, Updater Updater)
        {
            this.InitializeComponent();
            this.DataContext = Settings;

            this._updater = Updater;
            this._timer = new Timer(new TimeSpan(0, 0, 10).TotalMilliseconds) { AutoReset = false };
            this._timer.Elapsed += (sender, e) => this.Dispatcher.Invoke(() => this.btn_Refresh.IsEnabled = true);

            Loaded += this.Update_Loaded;
        }

        private void Update_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= this.Update_Loaded;
            this.Refresh();
        }

        private void Refresh()
        {
            this.btn_Refresh.IsEnabled = false;
            this.btn_Update.IsEnabled = false;
            this.pb_Bar.Visibility = Visibility.Visible;
            this._availableRelease = null;
            this.btn_Update.Content = "Update Now";
            this.txt_UpdateStatus.Text = "Checking GitHub for updates…";

            this.txt_ChangeLog.Inlines.Clear();
            var bw = new BackgroundWorker();

            bw.DoWork += (sender, e) => e.Result = (this._updater.GetLatestRelease(), this._updater.GetAllReleases());

            bw.RunWorkerCompleted += (sender, e) =>
            {
                if (e.Error == null && e.Result is ValueTuple<Release, IEnumerable<Release>> result)
                {
                    this.InsertInlines(result.Item2 ?? Array.Empty<Release>(), this.txt_ChangeLog.Inlines);
                    var latest = result.Item1;
                    if (!Updater.IsNewerRelease(latest))
                    {
                        this.txt_UpdateStatus.Text = "No newer stable PackTracker release is available.";
                    }
                    else if (Updater.GetUpdateAsset(latest) == null)
                    {
                        this.txt_UpdateStatus.Text = $"{latest.tag_name} is available on GitHub. Automatic installation is unavailable for this release.";
                    }
                    else
                    {
                        this._availableRelease = latest;
                        this.txt_UpdateStatus.Text = $"{latest.tag_name} is ready to install.";
                        this.btn_Update.Content = $"Install {latest.tag_name}";
                        this.btn_Update.IsEnabled = true;
                    }
                }
                else
                {
                    this.txt_UpdateStatus.Text = "Could not check for updates. Try Refresh again.";
                }

                this.pb_Bar.Visibility = Visibility.Hidden;
                this._timer.Start();
            };

            bw.RunWorkerAsync();
        }

        private void InsertInlines(IEnumerable<Release> Releases, InlineCollection Target)
        {
            foreach (var Release in Releases)
            {
                var Headline = new Run(Release.tag_name + (Release.name != Release.tag_name ? (" \"" + Release.name + "\"") : ""))
                {
                    FontStyle = FontStyles.Oblique,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.CornflowerBlue,
                    TextDecorations = TextDecorations.Underline,
                    FontSize = 18,
                };
                Target.Add(Headline);

                if (Updater.TryGetReleaseVersion(Release, out var version) && Plugin.CurrentVersion == version)
                {
                    var Installed = new Run(" (installed)\n")
                    {
                        FontStyle = FontStyles.Oblique,
                        Foreground = Brushes.White,
                        FontSize = 10,
                    };
                    Target.Add(Installed);
                }
                else
                {
                    Headline.Text += "\n";
                }

                var Body = new Run(Release.body + "\n\n")
                {
                    Foreground = Brushes.White,
                };
                Target.Add(Body);
            }
        }

        private void btn_Refresh_Click(object sender, RoutedEventArgs e)
        {
            this.Refresh();
        }

        private void btn_Update_Click(object sender, RoutedEventArgs e)
        {
            var release = this._availableRelease;
            if (release == null || MessageBox.Show(
                    $"Install PackTracker {release.tag_name}? Hearthstone Deck Tracker will close and restart to finish the update.",
                    "PackTracker update", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes)
                return;

            var bw = new BackgroundWorker();
            bw.DoWork += (bwsender, bwe) => this._updater.StartUpdate(release);
            bw.RunWorkerCompleted += (bwsender, bwe) =>
            {
                this.pb_Bar.Visibility = Visibility.Hidden;

                if (bwe.Error == null)
                {
                    this.txt_UpdateStatus.Text = "Restarting Hearthstone Deck Tracker…";
                    Application.Current.Shutdown();
                }
                else
                {
                    this.btn_Refresh.IsEnabled = true;
                    this.btn_Update.IsEnabled = true;
                    this.txt_UpdateStatus.Text = "Could not prepare the update.";
                    MessageBox.Show(bwe.Error.Message, "PackTracker update", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };

            this.btn_Refresh.IsEnabled = false;
            this.btn_Update.IsEnabled = false;
            this.pb_Bar.Visibility = Visibility.Visible;
            this.txt_UpdateStatus.Text = "Downloading and verifying the update…";
            this._timer.Stop();
            bw.RunWorkerAsync();
        }
    }
}
