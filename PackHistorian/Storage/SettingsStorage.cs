using Hearthstone_Deck_Tracker;
using Newtonsoft.Json;
using System.IO;
using System.Xml;
using System;

namespace PackTracker.Storage
{
    internal class SettingsStorage : ISettingsStorage
    {
        private readonly string _appDataPath;

        public SettingsStorage() : this(Config.AppDataPath)
        {
        }

        internal SettingsStorage(string appDataPath)
        {
            this._appDataPath = appDataPath ?? throw new ArgumentNullException(nameof(appDataPath));
        }

        public Settings Fetch()
        {
            var Settings = new Settings();

            var path = Path.Combine(this._appDataPath, "PackTracker", "Settings.json");
            try
            {
                return JsonConvert.DeserializeObject<Settings>(File.ReadAllText(path)) ?? Settings;
            }
            catch
            {
                // Supress
            }

            path = Path.Combine(this._appDataPath, "PackTracker", "Settings.xml");
            if (File.Exists(path))
            {
                var Xml = new XmlDocument();
                Xml.Load(path);
                var Root = Xml.SelectSingleNode("settings");

                if (Root != null)
                {
                    if (bool.TryParse(Root.SelectSingleNode("spoil")?.InnerText, out var spoil))
                    {
                        Settings.Spoil = spoil;
                    }

                    if (bool.TryParse(Root.SelectSingleNode("pityoverlay")?.InnerText, out var pityoverlay))
                    {
                        Settings.PityOverlay = pityoverlay;
                    }

                    if (bool.TryParse(Root.SelectSingleNode("update")?.InnerText, out var update))
                    {
                        Settings.Update = update;
                    }
                }
            }

            return Settings;
        }

        public void Store(Settings Settings)
        {
            if (Settings == null)
            {
                throw new ArgumentNullException(nameof(Settings));
            }

            var path = Path.Combine(this._appDataPath, "PackTracker", "Settings.json");
            AtomicFile.WriteAllText(path, JsonConvert.SerializeObject(Settings, Newtonsoft.Json.Formatting.Indented));
        }
    }
}
