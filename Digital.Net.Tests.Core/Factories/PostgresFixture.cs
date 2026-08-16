using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Digital.Net.Cms.Context;
using Digital.Net.Core.Entities.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Testcontainers.PostgreSql;
using TUnit.Core.Interfaces;
using Assembly = System.Reflection.Assembly;
using Digital.Net.Lib.Entities.Context;

namespace Digital.Net.Tests.Core.Factories;

public class PostgresFixture : IAsyncInitializer, IAsyncDisposable
{
    // Per-assembly container name so multiple test projects (Core.Test, Cms.Test, ect) can run in parallel.
    private static readonly string ContainerName = BuildContainerName();

    private static string BuildContainerName()
    {
        var entry = Assembly.GetEntryAssembly()?.GetName().Name
                    ?? Assembly.GetExecutingAssembly().GetName().Name
                    ?? "test";

        var slug = entry.ToLowerInvariant().Replace('.', '-');
        return $"digitalnet-test-pg-{slug}";
    }

    private static readonly string[] ManagedSchemas =
        ["digital_net", "digital_net_cms", "crud_test", "templating_test", "public"];

    private readonly PostgreSqlContainer _container;
    private readonly ConcurrentDictionary<Type, Lazy<Task>> _ensuredContexts = new();

    public PostgresFixture()
    {
        var builder = new PostgreSqlBuilder()
            .WithImage("postgres:18-alpine")
            .WithName(ContainerName)
            .WithDatabase("digitalnet_test")
            .WithCleanUp(true);

        var endpoint = PostgresDockerResolver.Resolve();
        if (!string.IsNullOrWhiteSpace(endpoint))
            builder = builder.WithDockerEndpoint(endpoint);

        _container = builder.Build();
    }

    public string ConnectionString => _container.GetConnectionString() + ";Maximum Pool Size=80;Minimum Pool Size=5";

    public async Task InitializeAsync()
    {
        await RemoveLeftoverContainerAsync();
        await _container.StartAsync();

        await using (var digital = CreateContext<DigitalContext>())
        {
            await digital.Database.MigrateAsync();
        }

        await using (var cms = CreateContext<CmsContext>())
        {
            await cms.Database.MigrateAsync();
        }

        await TruncateAllAsync();
    }

    public Task EnsureCreatedAsync<T>() where T : DbContext
        => _ensuredContexts
            .GetOrAdd(
                typeof(T),
                _ => new Lazy<Task>(CreateSchemaAsync<T>, LazyThreadSafetyMode.ExecutionAndPublication)
            )
            .Value;

    private async Task CreateSchemaAsync<T>() where T : DbContext
    {
        await using var ctx = CreateContext<T>();
        await ctx.Database.EnsureCreatedAsync();
        try
        {
            ctx.Database.GetService<IRelationalDatabaseCreator>().CreateTables();
        }
        catch
        {
            /* tables already exist */
        }
    }

    private async Task TruncateAllAsync()
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();

        var schemaList = string.Join(", ", Array.ConvertAll(ManagedSchemas, s => $"'{s}'"));
        var listTablesSql = $"""
                             SELECT format('%I.%I', table_schema, table_name) AS qualified
                             FROM information_schema.tables
                             WHERE table_type = 'BASE TABLE'
                               AND table_schema IN ({schemaList})
                               AND table_name NOT LIKE '\_\_%' ESCAPE '\'
                             """;

        var tables = new List<string>();
        await using (var cmd = new NpgsqlCommand(listTablesSql, connection))
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
                tables.Add(reader.GetString(0));
        }

        if (tables.Count == 0) return;

        var truncateSql = $"TRUNCATE TABLE {string.Join(", ", tables)} RESTART IDENTITY CASCADE";
        await using var truncate = new NpgsqlCommand(truncateSql, connection);
        await truncate.ExecuteNonQueryAsync();
    }

    private T CreateContext<T>() where T : DbContext
    {
        var options = new DbContextOptionsBuilder<T>()
            .UseDigitalNpgsql<T>(ConnectionString)
            .Options;
        return (T)Activator.CreateInstance(typeof(T), options)!
               ?? throw new InvalidOperationException($"Could not instantiate {typeof(T).Name}.");
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _container.StopAsync();
        }
        catch
        {
            /* container may already be stopped */
        }

        try
        {
            await _container.DisposeAsync();
        }
        catch
        {
            /* best-effort: ensure no leftover */
        }

        // Belt-and-suspenders: in environments where Testcontainers cannot remove the
        // container itself (e.g. Ryuk disabled on Podman rootless), force a CLI-level rm.
        await RemoveLeftoverContainerAsync();

        GC.SuppressFinalize(this);
    }

    private static async Task RemoveLeftoverContainerAsync()
    {
        foreach (var cli in new[] { "podman", "docker" })
            if (await TryRemoveAsync(cli))
                return;
    }

    private static async Task<bool> TryRemoveAsync(string cli)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = cli,
                Arguments = $"rm -f {ContainerName}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (process is null) return false;
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}