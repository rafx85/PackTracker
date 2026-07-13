using Hearthstone_Deck_Tracker;
using Hearthstone_Deck_Tracker.Hearthstone;
using PackTracker.Entity;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using HDTCard = Hearthstone_Deck_Tracker.Hearthstone.Card;

namespace PackTracker.Storage
{
    internal class XmlHistory : IHistoryStorage
    {
        private readonly string _appDataPath;
        private readonly Func<string, HDTCard> _cardResolver;
        private bool _historyRequiresRecovery;
        private bool _recoveryBackupCreated;

        public XmlHistory() : this(Config.AppDataPath, Database.GetCardFromId)
        {
        }

        internal XmlHistory(string appDataPath, Func<string, HDTCard> cardResolver)
        {
            this._appDataPath = appDataPath ?? throw new ArgumentNullException(nameof(appDataPath));
            this._cardResolver = cardResolver ?? throw new ArgumentNullException(nameof(cardResolver));
        }

        public History Fetch()
        {
            var History = new History();

            var path = this.GetHistoryPath();
            if (!File.Exists(path))
            {
                return History;
            }

            this._historyRequiresRecovery = true;
            this._recoveryBackupCreated = false;
            var content = File.ReadAllText(path);
            try
            {
                var Xml = new XmlDocument();
                Xml.LoadXml(content);
                var Root = Xml.SelectSingleNode("history");

                if (Root == null)
                {
                    throw new InvalidDataException("History.xml does not contain a history root element.");
                }

                var Packs = Root.SelectNodes("pack");
                foreach (XmlNode Pack in Packs)
                {
                    if (!int.TryParse(Pack.Attributes?["id"]?.Value, out var packId)
                        || !long.TryParse(Pack.Attributes?["time"]?.Value, out var ticks))
                    {
                        throw new InvalidDataException("A pack contains an invalid id or timestamp.");
                    }

                    var Cards = Pack.SelectNodes("card");
                    if (Cards.Count == 0)
                    {
                        throw new InvalidDataException("A pack does not contain any cards.");
                    }

                    var HistoryCards = new List<Entity.Card>();
                    foreach (XmlNode Card in Cards)
                    {
                        var cardId = Card.Attributes?["id"]?.Value;
                        if (string.IsNullOrEmpty(cardId))
                        {
                            throw new InvalidDataException("A card does not contain an id.");
                        }

                        var hdtCard = this._cardResolver(cardId);
                        if (hdtCard == null)
                        {
                            throw new InvalidDataException($"Card '{cardId}' could not be resolved.");
                        }

                        var premium = Card.Attributes?["premium"]?.Value;
                        HistoryCards.Add(new Entity.Card(hdtCard, premium == "premium"));
                    }

                    History.Add(new Pack(packId, new DateTime(ticks), HistoryCards));
                }

                this._historyRequiresRecovery = false;
            }
            catch (Exception exception) when (exception is XmlException || exception is InvalidDataException || exception is ArgumentOutOfRangeException)
            {
                this._historyRequiresRecovery = true;
                this.CreateRecoveryBackup(path);
                return new History();
            }

            return History;
        }

        public void Store(History History)
        {
            var Xml = new XmlDocument();
            Xml.AppendChild(Xml.CreateXmlDeclaration("1.0", "UTF-8", null));

            XmlNode Root = Xml.CreateElement("history");
            Xml.AppendChild(Root);

            foreach (var Pack in History)
            {
                XmlNode PackNode = Xml.CreateElement("pack");
                Root.AppendChild(PackNode);

                var Time = Xml.CreateAttribute("time");
                Time.Value = Pack.Time.Ticks.ToString();
                PackNode.Attributes.Append(Time);

                var PackId = Xml.CreateAttribute("id");
                PackId.Value = Pack.Id.ToString();
                PackNode.Attributes.Append(PackId);

                foreach (var Card in Pack.Cards)
                {
                    XmlNode CardNode = Xml.CreateElement("card");
                    PackNode.AppendChild(CardNode);

                    var CardId = Xml.CreateAttribute("id");
                    CardId.Value = Card.HDTCard.Id;
                    CardNode.Attributes.Append(CardId);

                    if (Card.Premium)
                    {
                        var Premium = Xml.CreateAttribute("premium");
                        Premium.Value = "premium";
                        CardNode.Attributes.Append(Premium);
                    }
                }
            }

            var path = this.GetHistoryPath();
            if (this._historyRequiresRecovery && !this._recoveryBackupCreated)
            {
                this.CreateRecoveryBackup(path);
            }

            AtomicFile.WriteAllText(path, Xml.OuterXml);
        }

        private string GetHistoryPath()
        {
            return Path.Combine(this._appDataPath, "PackTracker", "History.xml");
        }

        private void CreateRecoveryBackup(string historyPath)
        {
            var directory = Path.GetDirectoryName(historyPath);
            Directory.CreateDirectory(directory);

            var backupPath = Path.Combine(
                directory,
                $"History_backup_{DateTime.Now:yyyy-MM-dd_HH-mm-ss-fff}_{Guid.NewGuid():N}.xml");

            File.Copy(historyPath, backupPath, false);
            this._recoveryBackupCreated = true;
        }
    }
}
