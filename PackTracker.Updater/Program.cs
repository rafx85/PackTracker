using PackTracker.Update;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization.Json;
using System.Threading;
using System.Windows.Forms;

namespace PackTracker.Updater
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            var quiet = args.Length == 2 && args[1] == "--quiet";
            var payload = Path.GetDirectoryName(typeof(Program).Assembly.Location);
            var stage = Path.GetDirectoryName(payload);
            var logPath = Path.Combine(stage, "update.log");
            UpdateRequest request = null;
            var hdtExited = false;
            var updated = false;

            try
            {
                if ((!quiet && args.Length != 1)
                    || !string.Equals(Path.GetFullPath(args[0]), Path.Combine(stage, "update-request.json"), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("The updater needs the request from its staging directory.");

                using (var stream = File.OpenRead(args[0]))
                    request = (UpdateRequest)new DataContractJsonSerializer(typeof(UpdateRequest)).ReadObject(stream);
                if (request == null || request.ProcessId <= 0 || request.ProcessStartUtcTicks <= 0)
                    throw new InvalidDataException("The update request has no valid HDT process.");
                UpdateRequest.ValidateHdtExecutable(request.HdtExecutable);

                WaitForExit(request);
                hdtExited = true;

                var version = Version.Parse(request.ExpectedVersion);
                UpdatePackage.ValidatePayload(payload, version);
                Install(payload, Path.GetFullPath(request.InstalledPlugin), Path.Combine(stage, "backup"), version);
                updated = true;
                Log(logPath, $"PackTracker {version} is installed or already current.");
                RestartHdt(request.HdtExecutable);
                return 0;
            }
            catch (Exception exception)
            {
                Log(logPath, exception.ToString());
                if (hdtExited && !updated)
                {
                    try { RestartHdt(request.HdtExecutable); }
                    catch (Exception restartError) { Log(logPath, restartError.ToString()); }
                }

                if (!quiet)
                {
                    var summary = updated
                        ? "PackTracker was updated. Please start Hearthstone Deck Tracker manually."
                        : "PackTracker could not install the update.";
                    MessageBox.Show($"{summary}\n\n{exception.Message}\n\nLog: {logPath}",
                        "PackTracker update", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                return 1;
            }
        }

        private static void WaitForExit(UpdateRequest request)
        {
            Process process;
            try { process = Process.GetProcessById(request.ProcessId); }
            catch (ArgumentException) { return; }
            using (process)
            {
                if (process.HasExited)
                    return;

                try
                {
                    if (process.StartTime.ToUniversalTime().Ticks != request.ProcessStartUtcTicks)
                        return;
                    if (!process.WaitForExit((int)TimeSpan.FromMinutes(2).TotalMilliseconds))
                        throw new TimeoutException("Hearthstone Deck Tracker did not exit within two minutes.");
                }
                catch (InvalidOperationException) when (process.HasExited) { }
            }
        }

        private static void Install(string payload, string pluginPath, string backupDirectory, Version expectedVersion)
        {
            var targetDirectory = Path.GetDirectoryName(pluginPath);
            var lockPath = Path.Combine(targetDirectory, ".packtracker-update.lock");
            using var updateLock = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
            var installed = AssemblyName.GetAssemblyName(pluginPath);
            if (installed.Name != "PackTracker")
                throw new InvalidDataException("The installed PackTracker assembly could not be identified.");
            if (installed.Version >= expectedVersion)
                return;

            Directory.CreateDirectory(backupDirectory);
            var prepared = new List<(string Target, string Temporary, string Backup)>();
            var replaced = new Stack<(string Target, string Temporary, string Backup)>();
            try
            {
                foreach (var name in UpdatePackage.FileNames)
                {
                    var target = name == UpdatePackage.PluginFileName ? pluginPath : Path.Combine(targetDirectory, name);
                    var temporary = Path.Combine(targetDirectory, $".{name}.{Guid.NewGuid():N}.update");
                    var backup = File.Exists(target) ? Path.Combine(backupDirectory, name) : null;
                    prepared.Add((target, temporary, backup));
                    if (backup != null)
                        File.Copy(target, backup);
                    File.Copy(Path.Combine(payload, name), temporary);
                }

                foreach (var file in prepared)
                {
                    if (file.Backup != null)
                        File.Replace(file.Temporary, file.Target, null, true);
                    else
                        File.Move(file.Temporary, file.Target);
                    replaced.Push(file);
                }
            }
            catch (Exception installError)
            {
                var errors = new List<Exception> { installError };
                foreach (var file in replaced)
                {
                    try
                    {
                        if (file.Backup == null)
                            File.Delete(file.Target);
                        else
                        {
                            File.Copy(file.Backup, file.Temporary);
                            File.Replace(file.Temporary, file.Target, null, true);
                        }
                    }
                    catch (Exception rollbackError)
                    {
                        errors.Add(rollbackError);
                    }
                }

                if (errors.Count > 1)
                    throw new AggregateException($"Rollback encountered an error. The backups are in {backupDirectory}.", errors);
                throw;
            }
            finally
            {
                foreach (var file in prepared)
                {
                    try { File.Delete(file.Temporary); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
        }

        private static void RestartHdt(string path)
        {
            using var process = Process.Start(new ProcessStartInfo(path)
            {
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(path),
            });
            if (process == null)
                throw new InvalidOperationException("Hearthstone Deck Tracker could not be restarted.");
        }

        private static void Log(string path, string message)
        {
            try { File.AppendAllText(path, $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}"); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
