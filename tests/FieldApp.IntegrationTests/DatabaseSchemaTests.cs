using FieldApp.Domain.Areas;
using FieldApp.Domain.FieldItems;
using FieldApp.Domain.Projects;
using FieldApp.Infrastructure.Persistence;
using FieldApp.Infrastructure.Seeding;
using FieldApp.IntegrationTests.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using static FieldApp.Infrastructure.Seeding.DemoData;

namespace FieldApp.IntegrationTests;

/// <summary>The migration against a clean SQL Server, database constraints and rowversion concurrency.</summary>
public sealed class DatabaseSchemaTests(SqlServerContainerFixture sqlServer)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Migrations_apply_to_a_clean_server_and_match_the_model()
    {
        sqlServer.SkipIfUnavailable();
        var connectionString = await sqlServer.CreateMigratedDatabaseAsync(
            SqlServerContainerFixture.UniqueDatabaseName("CleanMigrate"), seedDemoData: false, CancellationToken);

        await using var db = CreateContext(connectionString);

        Assert.Equal(db.Database.GetMigrations(), await db.Database.GetAppliedMigrationsAsync(CancellationToken));
        Assert.Empty(await db.Database.GetPendingMigrationsAsync(CancellationToken));
        Assert.False(db.Database.HasPendingModelChanges(), "The model has changes not captured in a migration.");
    }

    [Fact]
    public async Task Seeding_is_idempotent()
    {
        sqlServer.SkipIfUnavailable();
        var connectionString = await sqlServer.CreateMigratedDatabaseAsync(
            SqlServerContainerFixture.UniqueDatabaseName("SeedTwice"), seedDemoData: true, CancellationToken);

        var before = await CountRowsAsync(connectionString);
        await sqlServer.CreateMigratedDatabaseAsync(new SqlConnectionStringBuilder(connectionString).InitialCatalog, seedDemoData: true, CancellationToken);
        var after = await CountRowsAsync(connectionString);

        Assert.Equal(before, after);
        Assert.Equal(DemoData.Personas.Count, before.Users);
    }

    [Fact]
    public async Task Project_number_is_unique()
    {
        sqlServer.SkipIfUnavailable();
        await using var db = CreateContext(sqlServer.SeededConnectionString!);

        db.Projects.Add(Project.Create(Guid.NewGuid(), ProjectNumbers.MosEisley, "Duplicate Synthetic", "UTC", DateTimeOffset.UtcNow));

        await AssertSqlErrorAsync(db, 2601);
    }

    [Fact]
    public async Task Area_path_is_unique_within_a_project()
    {
        sqlServer.SkipIfUnavailable();
        await using var db = CreateContext(sqlServer.SeededConnectionString!);

        db.Areas.Add(Area.Create(Guid.NewGuid(), ProjectId(ProjectNumbers.MosEisley), null, "Building A", 999));

        await AssertSqlErrorAsync(db, 2601);
    }

    [Fact]
    public async Task Database_rejects_a_parent_area_from_another_project()
    {
        sqlServer.SkipIfUnavailable();

        // Bypasses the domain on purpose: the composite foreign key must hold on its own.
        var error = await ExecuteExpectingSqlErrorAsync(
            sqlServer.SeededConnectionString!,
            "INSERT INTO Areas (Id, ProjectId, ParentAreaId, Name, Path, SortOrder, IsActive) VALUES (@id, @project, @parent, N'X', N'X-cross', 1, 1)",
            ("@id", Guid.NewGuid()),
            ("@project", ProjectId(ProjectNumbers.MosEisley)),
            ("@parent", AreaId(ProjectNumbers.Anchorhead, "Headworks")));

        Assert.Equal(547, error.Number); // Foreign key violation.
    }

    [Fact]
    public async Task Database_rejects_an_area_that_is_its_own_parent()
    {
        sqlServer.SkipIfUnavailable();
        var id = Guid.NewGuid();

        var error = await ExecuteExpectingSqlErrorAsync(
            sqlServer.SeededConnectionString!,
            "INSERT INTO Areas (Id, ProjectId, ParentAreaId, Name, Path, SortOrder, IsActive) VALUES (@id, @project, @id, N'Self', N'Self', 1, 1)",
            ("@id", id),
            ("@project", ProjectId(ProjectNumbers.MosEisley)));

        Assert.Equal(547, error.Number); // Check (or FK) constraint violation.
    }

    [Fact]
    public async Task Database_rejects_trade_partner_membership_without_company()
    {
        sqlServer.SkipIfUnavailable();

        var error = await ExecuteExpectingSqlErrorAsync(
            sqlServer.SeededConnectionString!,
            "INSERT INTO ProjectMemberships (Id, ProjectId, UserId, Roles, CompanyId, IsActive) VALUES (@id, @project, @user, 8, NULL, 1)",
            ("@id", Guid.NewGuid()),
            ("@project", ProjectId(ProjectNumbers.ToscheStation)),
            ("@user", UserId(PersonaKeys.Unassigned)));

        Assert.Equal(547, error.Number);
        Assert.Contains("CK_ProjectMemberships_TradePartnerScope", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_trade_is_enabled_at_most_once_per_project()
    {
        sqlServer.SkipIfUnavailable();

        var error = await ExecuteExpectingSqlErrorAsync(
            sqlServer.SeededConnectionString!,
            "INSERT INTO ProjectTrades (Id, ProjectId, TradeId, ResponsibleCompanyId, IsActive) VALUES (@id, @project, @trade, NULL, 1)",
            ("@id", Guid.NewGuid()),
            ("@project", ProjectId(ProjectNumbers.MosEisley)),
            ("@trade", TradeId("DRY")));

        Assert.Equal(2601, error.Number);
    }

    [Fact]
    public async Task Rowversion_changes_on_update_and_stale_writes_are_rejected()
    {
        sqlServer.SkipIfUnavailable();
        var connectionString = await sqlServer.CreateMigratedDatabaseAsync(
            SqlServerContainerFixture.UniqueDatabaseName("Concurrency"), seedDemoData: true, CancellationToken);
        var areaId = AreaId(ProjectNumbers.MosEisley, "Building A / Level 1 / Lobby");

        await using var first = CreateContext(connectionString);
        await using var second = CreateContext(connectionString);
        var firstCopy = await first.Areas.SingleAsync(a => a.Id == areaId, CancellationToken);
        var secondCopy = await second.Areas.SingleAsync(a => a.Id == areaId, CancellationToken);
        var originalVersion = firstCopy.RowVersion.ToArray();

        firstCopy.Deactivate();
        await first.SaveChangesAsync(CancellationToken);

        Assert.NotEqual(originalVersion, firstCopy.RowVersion);

        secondCopy.Deactivate();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync(CancellationToken));
    }

    [Fact]
    public async Task Field_item_constraints_hold_at_the_database_level()
    {
        sqlServer.SkipIfUnavailable();
        var connectionString = await sqlServer.CreateMigratedDatabaseAsync(
            SqlServerContainerFixture.UniqueDatabaseName("ItemConstraints"), seedDemoData: true, CancellationToken);
        var draftId = Guid.NewGuid();

        await using (var db = CreateContext(connectionString))
        {
            db.FieldItems.Add(await NewDraftAsync(db, draftId));
            await db.SaveChangesAsync(CancellationToken);
        }

        // One item per creator + clientDraftId.
        await using (var db = CreateContext(connectionString))
        {
            db.FieldItems.Add(await NewDraftAsync(db, draftId));
            await AssertSqlErrorAsync(db, 2601);
        }

        // An item's Area must be in the item's project (composite foreign key).
        var error = await ExecuteExpectingSqlErrorAsync(
            connectionString,
            "UPDATE FieldItems SET AreaId = @area WHERE ClientDraftId = @draft",
            ("@area", AreaId(ProjectNumbers.Anchorhead, "Headworks")),
            ("@draft", draftId));
        Assert.Equal(547, error.Number);
    }

    [Fact]
    public async Task At_most_one_active_primary_photo_exists_per_item()
    {
        sqlServer.SkipIfUnavailable();
        var connectionString = await sqlServer.CreateMigratedDatabaseAsync(
            SqlServerContainerFixture.UniqueDatabaseName("OnePrimary"), seedDemoData: true, CancellationToken);
        Guid itemId;

        await using (var db = CreateContext(connectionString))
        {
            var item = await NewDraftAsync(db, Guid.NewGuid());
            item.ReservePrimaryPhoto(Guid.NewGuid(), "image/jpeg", 100, TestImages.Sha256([1]), null, UserId(PersonaKeys.Superintendent), DateTimeOffset.UtcNow, TimeSpan.FromMinutes(15));
            db.FieldItems.Add(item);
            await db.SaveChangesAsync(CancellationToken);
            itemId = item.Id;
        }

        var error = await ExecuteExpectingSqlErrorAsync(
            connectionString,
            """
            INSERT INTO FieldItemPhotos (Id, FieldItemId, Status, BlobKey, MediaType, ByteLength, Sha256, SortOrder, ReservedAt, ReservationExpiresAt, UploadedByUserId, IsPrimary)
            VALUES (NEWID(), @item, 'Reserved', CONVERT(nvarchar(36), NEWID()), 'image/jpeg', 100, REPLICATE('A', 43) + '=', 1, SYSUTCDATETIME(), SYSUTCDATETIME(), @user, 1)
            """,
            ("@item", itemId),
            ("@user", UserId(PersonaKeys.Superintendent)));

        Assert.Equal(2601, error.Number);
    }

    [Fact]
    public async Task Concurrent_changes_to_the_same_item_are_detected_by_rowversion()
    {
        sqlServer.SkipIfUnavailable();
        var connectionString = await sqlServer.CreateMigratedDatabaseAsync(
            SqlServerContainerFixture.UniqueDatabaseName("ItemConcurrency"), seedDemoData: true, CancellationToken);
        Guid itemId;
        await using (var db = CreateContext(connectionString))
        {
            var item = await NewDraftAsync(db, Guid.NewGuid());
            db.FieldItems.Add(item);
            await db.SaveChangesAsync(CancellationToken);
            itemId = item.Id;
        }

        await using var first = CreateContext(connectionString);
        await using var second = CreateContext(connectionString);
        var firstCopy = await first.FieldItems.Include(i => i.Photos).SingleAsync(i => i.Id == itemId, CancellationToken);
        var secondCopy = await second.FieldItems.Include(i => i.Photos).SingleAsync(i => i.Id == itemId, CancellationToken);
        var user = UserId(PersonaKeys.Superintendent);

        firstCopy.ReservePrimaryPhoto(Guid.NewGuid(), "image/jpeg", 100, TestImages.Sha256([1]), null, user, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(15));
        await first.SaveChangesAsync(CancellationToken);
        secondCopy.ReservePrimaryPhoto(Guid.NewGuid(), "image/jpeg", 200, TestImages.Sha256([2]), null, user, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(15));

        await Assert.ThrowsAnyAsync<DbUpdateException>(() => second.SaveChangesAsync(CancellationToken));
    }

    private static async Task<FieldItem> NewDraftAsync(FieldAppDbContext db, Guid clientDraftId)
    {
        var projectId = ProjectId(ProjectNumbers.MosEisley);
        var projectTrade = await db.ProjectTrades.AsNoTracking().SingleAsync(pt => pt.ProjectId == projectId && pt.TradeId == TradeId("DRY"), CancellationToken);
        var trade = await db.Trades.AsNoTracking().SingleAsync(t => t.Id == projectTrade.TradeId, CancellationToken);
        var company = await db.Companies.AsNoTracking().SingleAsync(c => c.Id == projectTrade.ResponsibleCompanyId, CancellationToken);
        return FieldItem.CreateDraft(
            Guid.NewGuid(), projectId, clientDraftId, FieldItemType.PunchList, null, "Unit 214", projectTrade, trade, company,
            ItemPriority.Normal, UserId(PersonaKeys.Superintendent), DateTimeOffset.UtcNow);
    }

    private static FieldAppDbContext CreateContext(string connectionString) =>
        new(new DbContextOptionsBuilder<FieldAppDbContext>().UseSqlServer(connectionString).Options);

    private static async Task AssertSqlErrorAsync(FieldAppDbContext db, int expectedNumber)
    {
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(CancellationToken));
        Assert.Equal(expectedNumber, Assert.IsType<SqlException>(ex.InnerException).Number);
    }

    private static async Task<SqlException> ExecuteExpectingSqlErrorAsync(string connectionString, string sql, params (string Name, object Value)[] parameters)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(CancellationToken);
        await using var command = connection.CreateCommand();
#pragma warning disable CA2100 // Test-only SQL with constant text and parameters.
        command.CommandText = sql;
#pragma warning restore CA2100
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return await Assert.ThrowsAsync<SqlException>(() => command.ExecuteNonQueryAsync(CancellationToken));
    }

    private static async Task<(int Users, int Projects, int Areas, int ProjectTrades, int Memberships)> CountRowsAsync(string connectionString)
    {
        await using var db = CreateContext(connectionString);
        return (
            await db.Users.CountAsync(CancellationToken),
            await db.Projects.CountAsync(CancellationToken),
            await db.Areas.CountAsync(CancellationToken),
            await db.ProjectTrades.CountAsync(CancellationToken),
            await db.ProjectMemberships.CountAsync(CancellationToken));
    }
}
