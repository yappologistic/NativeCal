using System;
using System.IO;
using System.Threading.Tasks;
using NativeCal.Services;

namespace NativeCal.Tests;

/// <summary>
/// Base test class that provides a fresh, temp-file-based DatabaseService
/// for each test. Automatically cleans up the temp file on disposal.
/// </summary>
public abstract class TestBase : IAsyncLifetime
{
    protected DatabaseService Db { get; private set; } = null!;
    private string _dbPath = null!;

    public async Task InitializeAsync()
    {
        // Use a unique temp file for each test to ensure isolation
        _dbPath = Path.Combine(Path.GetTempPath(), $"nativecal_test_{Guid.NewGuid():N}.db");
        Db = new DatabaseService(_dbPath);
        await Db.InitializeAsync();
        App.Database = Db;
    }

    public Task DisposeAsync()
    {
        App.Database = null!;
        // Clean up the temp database file
        try
        {
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
        }
        catch
        {
            // Best effort cleanup — temp files are in system temp dir anyway
        }

        return Task.CompletedTask;
    }
}
