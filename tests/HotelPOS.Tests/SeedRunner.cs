using System;
using System.IO;
using System.Threading.Tasks;
using HotelPOS.Data;
using HotelPOS.Logging;
using Xunit;

namespace HotelPOS.Tests;

public class SeedRunner
{
    [Fact]
    public async Task SeedProductionDatabase()
    {
        string dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PSoftRestRentManager",
            "restrent.db");

        string logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PSoftRestRentManager",
            "logs");

        var connectionFactory = new DbConnectionFactory(dbPath);
        var logger = new AppLogger(logPath);

        var runner = new MigrationRunner(connectionFactory, logger);
        runner.EnsureDatabaseIsReady();

        var seeder = new DatabaseSeeder(connectionFactory, logger);
        await seeder.ResetAndSeedDatabaseAsync("2026-07");
    }
}
