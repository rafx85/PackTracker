using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;

namespace PackTracker.Update
{
    internal static class UpdatePackage
    {
        internal const string AssetName = "PackTracker.zip";
        internal const string PluginFileName = "PackTracker.dll";
        internal const string HelperFileName = "PackTracker.Updater.exe";
        internal const long MaximumBytes = 25 * 1024 * 1024;

        // Replace the plugin last so an interruption before that point leaves the old version loadable.
        internal static readonly IReadOnlyList<string> FileNames = Array.AsReadOnly(new[] { HelperFileName, PluginFileName });

        internal static Version NormalizeVersion(Version version) =>
            new Version(version.Major, version.Minor, Math.Max(version.Build, 0), Math.Max(version.Revision, 0));

        internal static bool IsSha256Digest(string digest) =>
            digest != null && digest.Length == 71
            && digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)
            && digest.Skip(7).All(Uri.IsHexDigit);

        internal static void VerifyDigest(Stream package, string expectedDigest)
        {
            using var sha256 = SHA256.Create();
            var actualDigest = "sha256:" + BitConverter.ToString(sha256.ComputeHash(package)).Replace("-", "");
            if (!string.Equals(actualDigest, expectedDigest, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The download did not match the SHA-256 digest provided by GitHub.");
        }

        internal static void Extract(ZipArchive archive, string destination)
        {
            var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
            long totalSize = 0;
            foreach (var entry in archive.Entries)
            {
                if (!FileNames.Contains(entry.FullName, StringComparer.OrdinalIgnoreCase) || entries.ContainsKey(entry.FullName))
                    throw new InvalidDataException($"Unexpected or duplicate update file: {entry.FullName}");
                if (entry.Length <= 0 || entry.Length > MaximumBytes - totalSize)
                    throw new InvalidDataException("The expanded update package exceeds its size limit or contains an empty file.");
                totalSize += entry.Length;
                entries.Add(entry.FullName, entry);
            }

            if (entries.Count != FileNames.Count)
                throw new InvalidDataException("The package must contain both PackTracker and its update helper.");

            foreach (var name in FileNames)
            {
                var entry = entries[name];
                using var input = entry.Open();
                using var output = new FileStream(Path.Combine(destination, name), FileMode.CreateNew);
                CopyBounded(input, output, entry.Length);
            }
        }

        internal static void CopyBounded(Stream input, Stream output, long expectedSize)
        {
            var buffer = new byte[81920];
            long totalSize = 0;
            int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                totalSize += read;
                if (totalSize > expectedSize)
                    throw new InvalidDataException("An update file exceeded its expected size.");
                output.Write(buffer, 0, read);
            }

            if (totalSize != expectedSize)
                throw new InvalidDataException("An update file is incomplete.");
        }

        internal static void ValidatePayload(string payloadDirectory, Version expectedVersion)
        {
            foreach (var name in FileNames)
            {
                var path = Path.Combine(payloadDirectory, name);
                var assembly = AssemblyName.GetAssemblyName(path);
                if (assembly.Name != Path.GetFileNameWithoutExtension(name) || assembly.Version != expectedVersion)
                    throw new InvalidDataException($"{name} does not match release {expectedVersion}.");
            }
        }
    }
}
