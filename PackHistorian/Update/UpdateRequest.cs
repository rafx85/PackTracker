using System;
using System.IO;
using System.Runtime.Serialization;

namespace PackTracker.Update
{
    [DataContract]
    internal sealed class UpdateRequest
    {
        [DataMember] public int ProcessId { get; set; }
        [DataMember] public long ProcessStartUtcTicks { get; set; }
        [DataMember] public string HdtExecutable { get; set; }
        [DataMember] public string InstalledPlugin { get; set; }
        [DataMember] public string ExpectedVersion { get; set; }

        internal static void ValidateHdtExecutable(string path)
        {
            var name = Path.GetFileName(path);
            if (!File.Exists(path)
                || (!string.Equals(name, "HearthstoneDeckTracker.exe", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(name, "Hearthstone Deck Tracker.exe", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("The Hearthstone Deck Tracker executable could not be located.");
        }
    }
}
