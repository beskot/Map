using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace AxamlMapControl.Source.Cache
{
    /// <summary>File-backed URI-keyed tile storage. Each operation owns its connection.</summary>
    public sealed class SqliteTileStore
    {
        private readonly string _connectionString;

        public SqliteTileStore(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || path == ":memory:")
                throw new ArgumentException("A database file path is required.", nameof(path));
            _connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = Path.GetFullPath(path),
                Mode = SqliteOpenMode.ReadWriteCreate,
                Pooling = false,
                DefaultTimeout = 15
            }.ToString();
        }

        private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var connection = new SqliteConnection(_connectionString);
            try
            {
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                using var command = connection.CreateCommand();
                command.CommandText = "CREATE TABLE IF NOT EXISTS TileImagesV2 (Uri TEXT NOT NULL PRIMARY KEY, ImageData BLOB NOT NULL);";
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                return connection;
            }
            catch
            {
                await connection.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }

        public async Task<Dictionary<string, byte[]>> ReadAsync(
            IEnumerable<string> uris, CancellationToken cancellationToken = default)
        {
            if (uris is null) throw new ArgumentNullException(nameof(uris));
            cancellationToken.ThrowIfCancellationRequested();
            var keys = uris.Distinct(StringComparer.Ordinal).ToArray();
            var result = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            if (keys.Length == 0) return result;
            await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT ImageData FROM TileImagesV2 WHERE Uri = $uri;";
            var parameter = command.Parameters.Add("$uri", SqliteType.Text);
            foreach (var key in keys)
            {
                cancellationToken.ThrowIfCancellationRequested();
                parameter.Value = key;
                var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                if (value is byte[] { Length: > 0 } data) result[key] = data;
            }
            return result;
        }

        public async Task WriteAsync(IReadOnlyDictionary<string, byte[]> images,
            CancellationToken cancellationToken = default)
        {
            if (images is null) throw new ArgumentNullException(nameof(images));
            cancellationToken.ThrowIfCancellationRequested();
            if (images.Count == 0) return;
            await using var connection = await OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
            using var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText = "INSERT INTO TileImagesV2 (Uri, ImageData) VALUES ($uri, $data) ON CONFLICT(Uri) DO UPDATE SET ImageData = excluded.ImageData;";
            var uriParameter = command.Parameters.Add("$uri", SqliteType.Text);
            var dataParameter = command.Parameters.Add("$data", SqliteType.Blob);
            foreach (var image in images)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (image.Value is not { Length: > 0 }) continue;
                uriParameter.Value = image.Key;
                dataParameter.Value = image.Value;
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            cancellationToken.ThrowIfCancellationRequested();
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
