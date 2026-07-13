using System;
using System.IO;

namespace PackTracker.Tests
{
    internal sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; }

        public TemporaryDirectory()
        {
            this.Path = System.IO.Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "test-data",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(this.Path);
        }

        public void Dispose()
        {
            if (Directory.Exists(this.Path))
            {
                Directory.Delete(this.Path, true);
            }
        }
    }
}
