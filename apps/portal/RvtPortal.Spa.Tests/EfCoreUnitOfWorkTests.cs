// File summary: Verifies real EF Core transaction behavior across RVT domain, search, and Identity contexts.
// Major updates:
// - 2026-07-25 pending Added fault-path coverage that preserves commit failures when rollback also fails.
// - 2026-07-08 pending Added relational Unit of Work tests for multi-context saves and rollback of immediate writes.

using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using RVT.DataAccess.Context;
using RVT.DataAccess.EntityModels.Models;
using RVT.Entities;
using RvtPortal.Spa.Application.Common;
using RvtPortal.Spa.Data;

namespace RvtPortal.Spa.Tests;

public sealed class EfCoreUnitOfWorkTests
{
    [Fact]
    // Function summary: Verifies one Unit of Work save persists domain, search, and Identity changes together.
    public async Task SaveChangesAsync_PersistsDomainSearchAndIdentityContexts()
    {
        await using RelationalUnitOfWorkFixture fixture = await RelationalUnitOfWorkFixture.CreateAsync();
        Guid companyId = Guid.NewGuid();
        Guid ruleId = Guid.NewGuid();
        ApplicationUser user = CreateUser("persisted-user@example.test");

        fixture.DomainContext.Companies.Add(new Company { Id = companyId, CompanyName = "Persisted Company" });
        fixture.SearchContext.ReportRules.Add(new ReportRule
        {
            Id = ruleId,
            SiteId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Frequency = ReportFrequencyType.Weekly,
            ReportName = "Persisted report rule"
        });
        fixture.ApplicationContext.Users.Add(user);

        int changes = await fixture.UnitOfWork.SaveChangesAsync(CancellationToken.None);

        Assert.True(changes >= 3);
        await using RVTDbContext domainVerification = fixture.CreateDomainContext();
        await using RVTSearchContext searchVerification = fixture.CreateSearchContext();
        await using ApplicationDbContext applicationVerification = fixture.CreateApplicationContext();
        Assert.True(await domainVerification.Companies.AnyAsync(company => company.Id == companyId));
        Assert.True(await searchVerification.ReportRules.AnyAsync(rule => rule.Id == ruleId));
        Assert.True(await applicationVerification.Users.AnyAsync(item => item.Id == user.Id));
    }

    [Fact]
    // Function summary: Verifies Identity writes saved inside a handler roll back when later domain persistence fails.
    public async Task ExecuteInTransactionAsync_RollsBackImmediateIdentitySaveWhenDomainSaveFails()
    {
        await using RelationalUnitOfWorkFixture fixture = await RelationalUnitOfWorkFixture.CreateAsync();
        ApplicationUser user = CreateUser("rolled-back-identity@example.test");

        await Assert.ThrowsAsync<DbUpdateException>(() => fixture.UnitOfWork.ExecuteInTransactionAsync(
            async token =>
            {
                fixture.ApplicationContext.Users.Add(user);
                await fixture.ApplicationContext.SaveChangesAsync(token);

                fixture.DomainContext.Deployments.Add(new Deployment
                {
                    Id = Guid.NewGuid(),
                    StartDate = DateTime.UtcNow,
                    ContractId = Guid.NewGuid(),
                    MonitorId = Guid.NewGuid()
                });
                await fixture.UnitOfWork.SaveChangesAsync(token);
                return true;
            },
            CancellationToken.None));

        await using ApplicationDbContext applicationVerification = fixture.CreateApplicationContext();
        Assert.False(await applicationVerification.Users.AnyAsync(item => item.Id == user.Id));
    }

    [Fact]
    // Function summary: Verifies search-context writes saved inside a handler roll back with the domain transaction.
    public async Task ExecuteInTransactionAsync_RollsBackImmediateSearchSaveWhenDomainSaveFails()
    {
        await using RelationalUnitOfWorkFixture fixture = await RelationalUnitOfWorkFixture.CreateAsync();
        Guid reportRuleId = Guid.NewGuid();

        await Assert.ThrowsAsync<DbUpdateException>(() => fixture.UnitOfWork.ExecuteInTransactionAsync(
            async token =>
            {
                fixture.SearchContext.ReportRules.Add(new ReportRule
                {
                    Id = reportRuleId,
                    SiteId = Guid.NewGuid(),
                    UserId = Guid.NewGuid(),
                    Frequency = ReportFrequencyType.Monthly,
                    ReportName = "Rolled back report rule"
                });
                await fixture.SearchContext.SaveChangesAsync(token);

                fixture.DomainContext.Deployments.Add(new Deployment
                {
                    Id = Guid.NewGuid(),
                    StartDate = DateTime.UtcNow,
                    ContractId = Guid.NewGuid(),
                    MonitorId = Guid.NewGuid()
                });
                await fixture.UnitOfWork.SaveChangesAsync(token);
                return true;
            },
            CancellationToken.None));

        await using RVTSearchContext searchVerification = fixture.CreateSearchContext();
        Assert.False(await searchVerification.ReportRules.AnyAsync(rule => rule.Id == reportRuleId));
    }

    [Fact]
    // Function summary: Verifies staged writes roll back when the handler returns a result that should not commit.
    public async Task ExecuteInTransactionAsync_RollsBackStagedWritesWhenResultShouldNotCommit()
    {
        await using RelationalUnitOfWorkFixture fixture = await RelationalUnitOfWorkFixture.CreateAsync();
        Guid companyId = Guid.NewGuid();

        // The operation stages and saves a write, then returns a failure result - exactly what a handler does
        // when it validates part-way through, populates errors, and returns instead of throwing.
        TestOutcome result = await fixture.UnitOfWork.ExecuteInTransactionAsync(
            async token =>
            {
                fixture.DomainContext.Companies.Add(new Company { Id = companyId, CompanyName = "Should roll back" });
                await fixture.UnitOfWork.SaveChangesAsync(token);
                return new TestOutcome(ShouldCommit: false);
            },
            CancellationToken.None);

        Assert.False(result.ShouldCommit);
        await using RVTDbContext verification = fixture.CreateDomainContext();
        Assert.False(
            await verification.Companies.AnyAsync(company => company.Id == companyId),
            "A result with ShouldCommit=false must roll back the writes the handler staged before it failed.");
    }

    [Fact]
    // Function summary: Verifies staged writes commit when the handler returns a successful result.
    public async Task ExecuteInTransactionAsync_CommitsStagedWritesWhenResultShouldCommit()
    {
        await using RelationalUnitOfWorkFixture fixture = await RelationalUnitOfWorkFixture.CreateAsync();
        Guid companyId = Guid.NewGuid();

        await fixture.UnitOfWork.ExecuteInTransactionAsync(
            async token =>
            {
                fixture.DomainContext.Companies.Add(new Company { Id = companyId, CompanyName = "Should commit" });
                await fixture.UnitOfWork.SaveChangesAsync(token);
                return new TestOutcome(ShouldCommit: true);
            },
            CancellationToken.None);

        await using RVTDbContext verification = fixture.CreateDomainContext();
        Assert.True(await verification.Companies.AnyAsync(company => company.Id == companyId));
    }

    [Fact]
    // Function summary: Verifies a caller-owned transaction still covers search and Identity writes made through the Unit of Work.
    public async Task ExecuteInTransactionAsync_EnlistsRemainingContextsInCallerOwnedTransaction()
    {
        await using RelationalUnitOfWorkFixture fixture = await RelationalUnitOfWorkFixture.CreateAsync();
        Guid reportRuleId = Guid.NewGuid();

        // The caller opens a transaction on the domain context only. The Unit of Work must widen it to the
        // other contexts rather than saving them outside any transaction boundary.
        await using IDbContextTransaction callerTransaction = await fixture.DomainContext.Database.BeginTransactionAsync();
        DbTransaction callerDbTransaction = callerTransaction.GetDbTransaction();

        await fixture.UnitOfWork.ExecuteInTransactionAsync(
            async token =>
            {
                // The contract under test: every context is enlisted in the caller's transaction, so EF itself
                // knows the writes are transactional rather than relying on shared-connection side effects.
                Assert.NotNull(fixture.SearchContext.Database.CurrentTransaction);
                Assert.NotNull(fixture.ApplicationContext.Database.CurrentTransaction);
                Assert.Same(callerDbTransaction, fixture.SearchContext.Database.CurrentTransaction.GetDbTransaction());
                Assert.Same(callerDbTransaction, fixture.ApplicationContext.Database.CurrentTransaction.GetDbTransaction());

                fixture.SearchContext.ReportRules.Add(new ReportRule
                {
                    Id = reportRuleId,
                    SiteId = Guid.NewGuid(),
                    UserId = Guid.NewGuid(),
                    Frequency = ReportFrequencyType.Weekly,
                    ReportName = "Ambient transaction report rule"
                });
                await fixture.UnitOfWork.SaveChangesAsync(token);
                return true;
            },
            CancellationToken.None);

        // Commit/rollback stays with the caller; rolling back must discard the search write too.
        await callerTransaction.RollbackAsync();

        await using RVTSearchContext searchVerification = fixture.CreateSearchContext();
        Assert.False(await searchVerification.ReportRules.AnyAsync(rule => rule.Id == reportRuleId));
    }

    [Fact]
    // Function summary: Verifies the Unit of Work rejects contexts that do not share one connection instead of failing obscurely.
    public async Task ExecuteInTransactionAsync_ThrowsWhenContextsDoNotShareOneConnection()
    {
        await using SqliteConnection domainConnection = new("Data Source=:memory:");
        await domainConnection.OpenAsync();
        await using SqliteConnection otherConnection = new("Data Source=:memory:");
        await otherConnection.OpenAsync();

        await using RVTDbContext domainContext = new(
            new DbContextOptionsBuilder<RVTDbContext>().UseSqlite(domainConnection).Options);
        await using RVTSearchContext searchContext = new(
            new DbContextOptionsBuilder<RVTSearchContext>().UseSqlite(otherConnection).Options);
        await using ApplicationDbContext applicationContext = new(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(otherConnection).Options);
        EfCoreUnitOfWork unitOfWork = new(domainContext, searchContext, applicationContext);

        InvalidOperationException error = await Assert.ThrowsAsync<InvalidOperationException>(() => unitOfWork.ExecuteInTransactionAsync(
            _ => Task.FromResult(true),
            CancellationToken.None));

        Assert.Contains("share one scoped DbConnection", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    // Function summary: Verifies a rollback fault cannot replace the commit fault that caused cleanup to begin.
    public async Task ExecuteInTransactionAsync_CommitFailureRemainsPrimaryWhenRollbackAlsoFails()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        CommitFailureException commitFailure = new();
        RollbackFailureException rollbackFailure = new();
        using CancellationTokenSource requestCancellation = new();
        FailingCommitAndRollbackInterceptor interceptor = new(
            commitFailure,
            rollbackFailure,
            requestCancellation);
        DbContextOptions<RVTDbContext> domainOptions = new DbContextOptionsBuilder<RVTDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(interceptor)
            .Options;
        DbContextOptions<RVTSearchContext> searchOptions = new DbContextOptionsBuilder<RVTSearchContext>()
            .UseSqlite(connection)
            .Options;
        DbContextOptions<ApplicationDbContext> applicationOptions =
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;

        await using RVTDbContext domainContext = new(domainOptions);
        await using RVTSearchContext searchContext = new(searchOptions);
        await using ApplicationDbContext applicationContext =
            new(applicationOptions);
        EfCoreUnitOfWork unitOfWork = new(
            domainContext,
            searchContext,
            applicationContext);

        Exception escaped = await Record.ExceptionAsync(
            () => unitOfWork.ExecuteInTransactionAsync(
                _ => Task.FromResult(new TestOutcome(ShouldCommit: true)),
                requestCancellation.Token));

        Assert.Multiple(
            () => Assert.Same(commitFailure, escaped),
            () => Assert.Equal(1, interceptor.CommitAttempts),
            () => Assert.Equal(1, interceptor.RollbackAttempts),
            () => Assert.True(requestCancellation.IsCancellationRequested),
            () => Assert.False(interceptor.RollbackToken.CanBeCanceled),
            () => Assert.Contains(
                nameof(FailingCommitAndRollbackInterceptor
                    .TransactionCommittingAsync),
                commitFailure.StackTrace,
                StringComparison.Ordinal));
        AggregateException diagnostics = Assert.IsType<AggregateException>(
            commitFailure.Data[
                EfCoreUnitOfWork.SecondaryTransactionFailuresDataKey]);
        Assert.Contains(rollbackFailure, diagnostics.InnerExceptions);
    }

    [Fact]
    // Function summary: Verifies best-effort secondary diagnostics cannot mask a primary exception with unusable Data.
    public async Task ExecuteInTransactionAsync_CommitFailureRemainsPrimaryWhenDiagnosticsCannotBeAttached()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();
        CommitFailureWithThrowingDataException commitFailure =
            new();
        RollbackFailureException rollbackFailure = new();
        FailingCommitAndRollbackInterceptor interceptor = new(
            commitFailure,
            rollbackFailure);
        DbContextOptions<RVTDbContext> domainOptions = new DbContextOptionsBuilder<RVTDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(interceptor)
            .Options;
        DbContextOptions<RVTSearchContext> searchOptions = new DbContextOptionsBuilder<RVTSearchContext>()
            .UseSqlite(connection)
            .Options;
        DbContextOptions<ApplicationDbContext> applicationOptions =
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;

        await using RVTDbContext domainContext = new(domainOptions);
        await using RVTSearchContext searchContext = new(searchOptions);
        await using ApplicationDbContext applicationContext =
            new(applicationOptions);
        EfCoreUnitOfWork unitOfWork = new(
            domainContext,
            searchContext,
            applicationContext);

        Exception escaped = await Record.ExceptionAsync(
            () => unitOfWork.ExecuteInTransactionAsync(
                _ => Task.FromResult(new TestOutcome(ShouldCommit: true)),
                CancellationToken.None));

        Assert.Multiple(
            () => Assert.Same(commitFailure, escaped),
            () => Assert.Equal(1, interceptor.CommitAttempts),
            () => Assert.Equal(1, interceptor.RollbackAttempts));
    }

    [Fact]
    // Function summary: Verifies a retried operation does not re-stage the previous attempt's writes as duplicates.
    public async Task ExecuteInTransactionAsync_ClearsChangeTrackerBetweenRetries()
    {
        await using SqliteConnection connection = new("Data Source=:memory:");
        await connection.OpenAsync();

        // A retrying execution strategy plus an interceptor that fails the first company INSERT: the strategy
        // retries the whole begin/save/commit block, which re-runs the operation below.
        FailFirstCompanyInsertInterceptor interceptor = new();
        DbContextOptions<RVTDbContext> domainOptions = new DbContextOptionsBuilder<RVTDbContext>()
            .UseSqlite(connection, sqlite => sqlite.ExecutionStrategy(dependencies => new RetryOnMarkerStrategy(dependencies)))
            .AddInterceptors(interceptor)
            .Options;
        DbContextOptions<RVTSearchContext> searchOptions = new DbContextOptionsBuilder<RVTSearchContext>().UseSqlite(connection).Options;
        DbContextOptions<ApplicationDbContext> applicationOptions = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;

        await using RVTDbContext domainContext = new(domainOptions);
        await using RVTSearchContext searchContext = new(searchOptions);
        await using ApplicationDbContext applicationContext = new(applicationOptions);
        await domainContext.Database.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();

        EfCoreUnitOfWork unitOfWork = new(domainContext, searchContext, applicationContext);

        // The operation adds one company per invocation, mirroring a create handler. If the retry re-runs it
        // without clearing the tracker, the first attempt's still-tracked company is inserted alongside the
        // second and the table ends up with two rows.
        await unitOfWork.ExecuteInTransactionAsync(
            async token =>
            {
                domainContext.Companies.Add(new Company { Id = Guid.NewGuid(), CompanyName = "Retried company" });
                await unitOfWork.SaveChangesAsync(token);
                return new TestOutcome(ShouldCommit: true);
            },
            CancellationToken.None);

        Assert.True(interceptor.Failed, "The interceptor must have forced a retry for this test to prove anything.");
        Assert.Equal(1, await domainContext.Companies.CountAsync());
    }

    // A minimal ITransactionOutcome so the gate can be exercised without depending on a specific command result.
    private sealed record TestOutcome(bool ShouldCommit) : ITransactionOutcome;

    private sealed class CommitFailureException : Exception;

    private sealed class CommitFailureWithThrowingDataException : Exception
    {
        public override System.Collections.IDictionary Data =>
            throw new InvalidOperationException(
                "Diagnostics cannot be attached to this exception.");
    }

    private sealed class RollbackFailureException : Exception;

    // Throws distinct commit and rollback instances so the escaped exception identity and cleanup diagnostics are observable.
    private sealed class FailingCommitAndRollbackInterceptor(
        Exception commitFailure,
        Exception rollbackFailure,
        CancellationTokenSource? requestCancellation = null)
        : Microsoft.EntityFrameworkCore.Diagnostics.DbTransactionInterceptor
    {
        public int CommitAttempts { get; private set; }

        public int RollbackAttempts { get; private set; }

        public CancellationToken RollbackToken { get; private set; }

        public override ValueTask<
            Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult>
            TransactionCommittingAsync(
                DbTransaction transaction,
                Microsoft.EntityFrameworkCore.Diagnostics.TransactionEventData eventData,
                Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult result,
                CancellationToken cancellationToken = default)
        {
            CommitAttempts++;
            requestCancellation?.Cancel();
            throw commitFailure;
        }

        public override ValueTask<
            Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult>
            TransactionRollingBackAsync(
                DbTransaction transaction,
                Microsoft.EntityFrameworkCore.Diagnostics.TransactionEventData eventData,
                Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult result,
                CancellationToken cancellationToken = default)
        {
            RollbackAttempts++;
            RollbackToken = cancellationToken;
            throw rollbackFailure;
        }
    }

    // Marks the one designated "transient" failure the retry strategy below is willing to retry.
    private sealed class TransientMarkerException : Exception;

    // Retries only the marker exception (unwrapping the DbUpdateException EF wraps it in), with no delay.
    private sealed class RetryOnMarkerStrategy : ExecutionStrategy
    {
        public RetryOnMarkerStrategy(ExecutionStrategyDependencies dependencies)
            : base(dependencies, maxRetryCount: 3, maxRetryDelay: TimeSpan.Zero)
        {
        }

        protected override bool ShouldRetryOn(Exception exception)
        {
            for (Exception? current = exception; current is not null; current = current.InnerException)
            {
                if (current is TransientMarkerException)
                {
                    return true;
                }
            }

            return false;
        }
    }

    // Throws the marker exception on the first company INSERT only; later inserts (including the retry) pass.
    // EF may issue the insert as a non-query or as a reader (INSERT ... RETURNING), so both hooks are covered.
    private sealed class FailFirstCompanyInsertInterceptor : Microsoft.EntityFrameworkCore.Diagnostics.DbCommandInterceptor
    {
        public bool Failed { get; private set; }

        private bool ShouldFail(DbCommand command)
        {
            if (Failed)
            {
                return false;
            }

            string text = command.CommandText;
            if (text.Contains("INSERT", StringComparison.OrdinalIgnoreCase) &&
                text.Contains("company", StringComparison.OrdinalIgnoreCase))
            {
                Failed = true;
                return true;
            }

            return false;
        }

        public override ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            Microsoft.EntityFrameworkCore.Diagnostics.CommandEventData eventData,
            Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (ShouldFail(command))
            {
                throw new TransientMarkerException();
            }

            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            Microsoft.EntityFrameworkCore.Diagnostics.CommandEventData eventData,
            Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (ShouldFail(command))
            {
                throw new TransientMarkerException();
            }

            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    // Function summary: Creates an Identity user suitable for direct relational persistence tests.
    private static ApplicationUser CreateUser(string email)
    {
        return new ApplicationUser
        {
            Id = Guid.NewGuid().ToString("N"),
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            EmailConfirmed = true,
            Name = email.Split('@')[0]
        };
    }

    private sealed class RelationalUnitOfWorkFixture : IAsyncDisposable
    {
        private readonly DbContextOptions<RVTDbContext> domainOptions;
        private readonly DbContextOptions<RVTSearchContext> searchOptions;
        private readonly DbContextOptions<ApplicationDbContext> applicationOptions;
        private readonly SqliteConnection connection;

        private RelationalUnitOfWorkFixture(
            SqliteConnection connection,
            DbContextOptions<RVTDbContext> domainOptions,
            DbContextOptions<RVTSearchContext> searchOptions,
            DbContextOptions<ApplicationDbContext> applicationOptions)
        {
            this.connection = connection;
            this.domainOptions = domainOptions;
            this.searchOptions = searchOptions;
            this.applicationOptions = applicationOptions;
            DomainContext = new RVTDbContext(domainOptions);
            SearchContext = new RVTSearchContext(searchOptions);
            ApplicationContext = new ApplicationDbContext(applicationOptions);
            UnitOfWork = new EfCoreUnitOfWork(DomainContext, SearchContext, ApplicationContext);
        }

        public RVTDbContext DomainContext { get; }
        public RVTSearchContext SearchContext { get; }
        public ApplicationDbContext ApplicationContext { get; }
        public EfCoreUnitOfWork UnitOfWork { get; }

        // Function summary: Creates one in-memory relational database shared by all EF contexts.
        public static async Task<RelationalUnitOfWorkFixture> CreateAsync()
        {
            SqliteConnection connection = new("Data Source=:memory:;Foreign Keys=True");
            await connection.OpenAsync();
            await EnableForeignKeysAsync(connection);

            DbContextOptions<RVTDbContext> domainOptions = new DbContextOptionsBuilder<RVTDbContext>().UseSqlite(connection).Options;
            DbContextOptions<RVTSearchContext> searchOptions = new DbContextOptionsBuilder<RVTSearchContext>().UseSqlite(connection).Options;
            DbContextOptions<ApplicationDbContext> applicationOptions = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
            await CreateTablesAsync(new RVTDbContext(domainOptions));
            await CreateTablesAsync(new RVTSearchContext(searchOptions));
            await CreateTablesAsync(new ApplicationDbContext(applicationOptions));

            return new RelationalUnitOfWorkFixture(connection, domainOptions, searchOptions, applicationOptions);
        }

        // Function summary: Creates an isolated domain context over the shared test database.
        public RVTDbContext CreateDomainContext()
        {
            return new RVTDbContext(domainOptions);
        }

        // Function summary: Creates an isolated search context over the shared test database.
        public RVTSearchContext CreateSearchContext()
        {
            return new RVTSearchContext(searchOptions);
        }

        // Function summary: Creates an isolated Identity context over the shared test database.
        public ApplicationDbContext CreateApplicationContext()
        {
            return new ApplicationDbContext(applicationOptions);
        }

        // Function summary: Releases EF contexts and the shared SQLite connection.
        public async ValueTask DisposeAsync()
        {
            await DomainContext.DisposeAsync();
            await SearchContext.DisposeAsync();
            await ApplicationContext.DisposeAsync();
            await connection.DisposeAsync();
        }

        // Function summary: Creates tables for one EF model in the shared test database.
        private static async Task CreateTablesAsync(DbContext context)
        {
            await using (context)
            {
                await context.Database.GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
            }
        }

        // Function summary: Ensures SQLite enforces relational constraints during rollback tests.
        private static async Task EnableForeignKeysAsync(SqliteConnection connection)
        {
            await using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "PRAGMA foreign_keys = ON";
            await command.ExecuteNonQueryAsync();
        }
    }
}
