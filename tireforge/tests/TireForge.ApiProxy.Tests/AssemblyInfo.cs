// The DB tests share one Testcontainers SQL Server container; TestDb's sync ctor
// blocks once on container startup. Serial execution avoids thread-pool
// starvation while that first block resolves (parallel + sync-over-async deadlocked in CI).
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
