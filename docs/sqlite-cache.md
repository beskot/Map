# SQLite tile cache

The cache now uses `TileImagesV2`, keyed by the full tile URI. The legacy `Tiles` table is not modified, imported, or deleted: old rows do not identify their provider reliably. Expect cache misses and downloads when switching to the new implementation. Back up valuable database files before upgrading.

`DataSource` accepts a database file path, including a file that does not exist yet. Its parent directory must already exist. Empty configuration uses `sqlite_cache.db` in the working directory. Each request snapshots the path before its first await; changing the property affects subsequent requests, not the destination of an existing request.

Each storage operation opens and closes its own connection. Successful non-empty responses are written as one transaction with URI-based upserts. Database errors are propagated to the caller. Cancellation is passed to the loader and storage operations, but interruption latency depends on SQLite and the provider; this is not a guarantee of immediate interruption of native work.

Disposing the cache rejects new requests. Already-started requests finish or cancel using their own token; no shared connection or context is retained. The injected loader remains owned by the caller.

Full URIs can contain API keys or other sensitive query parameters. Do not publish cache databases containing private URLs. Equal URIs are treated as equal resources; responses varying only by authentication headers require a separate database.

This implementation has no automatic expiry, disk-size limit, offline-area downloader, or compatibility with arbitrary third-party tile databases. New storage tests have been added, but their presence alone does not establish a successful CI run or UI validation. The older SQLite warnings in the root README should be reviewed as the remaining stabilization work is completed.
