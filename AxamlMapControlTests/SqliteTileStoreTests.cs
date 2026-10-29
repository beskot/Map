using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AxamlMapControl.Source.Cache;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AxamlMapControlTests
{
    public class SqliteTileStoreTests
    {
        [Fact]
        public async Task PersistsAndSeparatesProvidersAfterReopening()
        {
            var path = NewPath();
            try
            {
                var first = "https://first.example/3/2/1.png";
                var second = "https://second.example/3/2/1.png";
                await new SqliteTileStore(path).WriteAsync(new Dictionary<string, byte[]>
                {
                    [first] = new byte[] { 1 },
                    [second] = new byte[] { 2 }
                });
                var data = await new SqliteTileStore(path).ReadAsync(new[] { first, second });
                Assert.Equal(new byte[] { 1 }, data[first]);
                Assert.Equal(new byte[] { 2 }, data[second]);
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public async Task PreservesLegacyTableAndDoesNotTreatItAsProviderData()
        {
            var path = NewPath();
            try
            {
                await using (var connection = Connection(path))
                {
                    await connection.OpenAsync();
                    using var command = connection.CreateCommand();
                    command.CommandText = "CREATE TABLE Tiles (Zoom INTEGER, X INTEGER, Y INTEGER, ImageData BLOB); INSERT INTO Tiles VALUES (3, 2, 1, X'09');";
                    await command.ExecuteNonQueryAsync();
                }
                var key = "https://first.example/3/2/1.png";
                var store = new SqliteTileStore(path);
                Assert.Empty(await store.ReadAsync(new[] { key }));
                await store.WriteAsync(new Dictionary<string, byte[]> { [key] = new byte[] { 1 } });
                await using var check = Connection(path);
                await check.OpenAsync();
                using var count = check.CreateCommand();
                count.CommandText = "SELECT COUNT(*) FROM Tiles WHERE ImageData = X'09';";
                Assert.Equal(1L, Convert.ToInt64(await count.ExecuteScalarAsync()));
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public async Task PreCancelledWriteDoesNotCreateDatabase()
        {
            var path = NewPath();
            try
            {
                using var cancellation = new CancellationTokenSource();
                cancellation.Cancel();
                var store = new SqliteTileStore(path);
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.WriteAsync(
                    new Dictionary<string, byte[]>
                    {
                        ["https://first.example/0/0/0.png"] = new byte[] { 1 }
                    }, cancellation.Token));
                Assert.False(File.Exists(path));
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public async Task UpdatesExistingUriAndSkipsEmptyData()
        {
            var path = NewPath();
            try
            {
                var store = new SqliteTileStore(path);
                var key = "https://first.example/0/0/0.png";
                var empty = "https://first.example/0/0/1.png";
                await store.WriteAsync(new Dictionary<string, byte[]> { [key] = new byte[] { 1 } });
                await store.WriteAsync(new Dictionary<string, byte[]>
                {
                    [key] = new byte[] { 2 }, [empty] = Array.Empty<byte>()
                });
                var data = await store.ReadAsync(new[] { key, empty });
                Assert.Single(data);
                Assert.Equal(new byte[] { 2 }, data[key]);
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public async Task ConcurrentConnectionsPreserveBothWrites()
        {
            var path = NewPath();
            try
            {
                var first = "https://first.example/0/0/0.png";
                var second = "https://second.example/0/0/0.png";
                var store = new SqliteTileStore(path);
                await store.ReadAsync(new[] { first });
                await Task.WhenAll(
                    Task.Run(() => new SqliteTileStore(path).WriteAsync(
                        new Dictionary<string, byte[]> { [first] = new byte[] { 1 } })),
                    Task.Run(() => new SqliteTileStore(path).WriteAsync(
                        new Dictionary<string, byte[]> { [second] = new byte[] { 2 } })));
                var data = await store.ReadAsync(new[] { first, second });
                Assert.Equal(new byte[] { 1 }, data[first]);
                Assert.Equal(new byte[] { 2 }, data[second]);
            }
            finally { File.Delete(path); }
        }

        private static string NewPath() => Path.Combine(
            Path.GetTempPath(), "axamlmap-" + Guid.NewGuid().ToString("N") + ".db");

        private static SqliteConnection Connection(string path) => new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
    }
}
