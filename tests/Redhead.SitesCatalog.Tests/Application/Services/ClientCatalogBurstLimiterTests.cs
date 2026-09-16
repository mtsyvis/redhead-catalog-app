using Redhead.SitesCatalog.Application.Services.ClientCatalog;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Exceptions;

namespace Redhead.SitesCatalog.Tests;

public sealed class ClientCatalogBurstLimiterTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void Constructor_RejectsNonPositiveBudget(int limit)
    {
        // Arrange
        var clock = new MutableClock();

        // Act
        var exception = Record.Exception(() => new ClientCatalogBurstLimiter(clock, limit));

        // Assert
        Assert.Equal("uniqueSitesLimit", Assert.IsType<ArgumentOutOfRangeException>(exception).ParamName);
    }

    [Fact]
    public void Budget_IsReleasedAtExactlyFiveMinutes()
    {
        // Arrange
        var clock = new MutableClock();
        var sut = new ClientCatalogBurstLimiter(clock, 1000);
        sut.EnsureAllowed("client", Domains(1000), 100);

        // Act
        clock.Advance(TimeSpan.FromSeconds(299));
        var rejected = Record.Exception(() => sut.EnsureAllowed("client", Domains(1000, "new"), 100));
        clock.Advance(TimeSpan.FromSeconds(1));
        var recovered = Record.Exception(() => sut.EnsureAllowed("client", Domains(1000, "new"), 100));

        // Assert
        Assert.Equal(1, Assert.IsType<ClientCatalogBurstLimitExceededException>(rejected).RetryAfterSeconds);
        Assert.Null(recovered);
    }

    [Fact]
    public void DefaultLimit_Allows2000UniqueSites_AndRepeatedOrEmptySelections()
    {
        // Arrange
        var clock = new MutableClock();
        var sut = CreateLimiter(clock);
        var domains = Domains(2000);

        // Act
        sut.EnsureAllowed("client", domains, 100);
        var rejected = Assert.Throws<ClientCatalogBurstLimitExceededException>(() =>
            sut.EnsureAllowed("client", ["new.com"], 100));
        var repeat = Record.Exception(() => sut.EnsureAllowed("client", domains.Concat(domains).ToArray(), 100));
        var empty = Record.Exception(() => sut.EnsureAllowed("client", [], 100));
        var otherUser = Record.Exception(() => sut.EnsureAllowed("other", ["new.com"], 100));

        // Assert
        Assert.Equal(300, rejected.RetryAfterSeconds);
        Assert.Null(repeat);
        Assert.Null(empty);
        Assert.Null(otherUser);
    }

    [Fact]
    public void RejectedRequest_DoesNotRefreshOrReserveDomains_AndBudgetRecoversAtFiveMinutes()
    {
        // Arrange
        var clock = new MutableClock();
        var sut = CreateLimiter(clock);
        sut.EnsureAllowed("client", Domains(2000), 100);
        clock.Advance(TimeSpan.FromSeconds(299));

        // Act
        var rejected = Assert.Throws<ClientCatalogBurstLimitExceededException>(() =>
            sut.EnsureAllowed("client", ["new.com"], 100));
        clock.Advance(TimeSpan.FromSeconds(1));
        var resumed = Record.Exception(() => sut.EnsureAllowed("client", Domains(2000, "new"), 100));

        // Assert
        Assert.Equal(1, rejected.RetryAfterSeconds);
        Assert.Null(resumed);
    }

    [Fact]
    public void PartialOverlap_RejectsWholeSelection_ButAllowsSmallerSelection()
    {
        // Arrange
        var sut = CreateLimiter(new MutableClock(), limit: 3);
        sut.EnsureAllowed("client", ["a.com", "b.com"], 1);

        // Act
        var rejected = Record.Exception(() => sut.EnsureAllowed("client", ["b.com", "c.com", "d.com"], 1));
        var smallerSelection = Record.Exception(() => sut.EnsureAllowed("client", ["c.com"], 1));
        var next = Record.Exception(() => sut.EnsureAllowed("client", ["d.com"], 1));

        // Assert
        Assert.IsType<ClientCatalogBurstLimitExceededException>(rejected);
        Assert.Null(smallerSelection);
        Assert.IsType<ClientCatalogBurstLimitExceededException>(next);
    }

    [Fact]
    public void RetryAfter_ExcludesRequestedDomains_AndWaitsForEnoughSlots()
    {
        // Arrange
        var clock = new MutableClock();
        var sut = CreateLimiter(clock, limit: 3);
        sut.EnsureAllowed("client", ["a.com"], 1);
        clock.Advance(TimeSpan.FromMinutes(1));
        sut.EnsureAllowed("client", ["b.com", "c.com"], 1);

        // Act
        var rejected = Assert.Throws<ClientCatalogBurstLimitExceededException>(() =>
            sut.EnsureAllowed("client", ["a.com", "d.com", "e.com"], 1));
        clock.Advance(TimeSpan.FromSeconds(rejected.RetryAfterSeconds));
        var resumed = Record.Exception(() => sut.EnsureAllowed("client", ["a.com", "d.com", "e.com"], 1));

        // Assert
        Assert.Equal(300, rejected.RetryAfterSeconds);
        Assert.Null(resumed);
    }

    [Fact]
    public void RepeatDoesNotAddUniqueSites_ButKeepsIssuedSitesInRollingWindow()
    {
        // Arrange
        var clock = new MutableClock();
        var sut = CreateLimiter(clock, limit: 2);
        sut.EnsureAllowed("client", ["a.com"], 1);
        clock.Advance(TimeSpan.FromMinutes(4));
        sut.EnsureAllowed("client", ["a.com"], 1);
        clock.Advance(TimeSpan.FromMinutes(1));

        // Act
        sut.EnsureAllowed("client", ["b.com"], 1);
        var rejected = Record.Exception(() => sut.EnsureAllowed("client", ["c.com"], 1));

        // Assert
        Assert.Equal(240, Assert.IsType<ClientCatalogBurstLimitExceededException>(rejected).RetryAfterSeconds);
    }

    [Fact]
    public void Preview_DoesNotConsumeOrRefreshBudget_AndChecksCurrentAvailability()
    {
        // Arrange
        var clock = new MutableClock();
        var sut = CreateLimiter(clock, limit: 2);
        sut.EnsureAllowed("client", ["a.com"], 1, consume: false);
        sut.EnsureAllowed("client", ["b.com"], 1);
        clock.Advance(TimeSpan.FromMinutes(4));

        // Act
        var rejectedPreview = Record.Exception(() => sut.EnsureAllowed("client", ["a.com", "c.com"], 1, consume: false));
        sut.EnsureAllowed("client", ["b.com"], 1, consume: false);
        clock.Advance(TimeSpan.FromMinutes(1));
        var resumed = Record.Exception(() => sut.EnsureAllowed("client", ["a.com", "c.com"], 1));

        // Assert
        Assert.IsType<ClientCatalogBurstLimitExceededException>(rejectedPreview);
        Assert.Null(resumed);
    }

    [Theory]
    [InlineData(100, 2000)]
    [InlineData(1, 2000)]
    public void PersonalSelection_FitsButDoesNotAllowFurtherNewSitesBeyondBudget(int selectionLimit, int expectedBudget)
    {
        // Arrange
        var sut = CreateLimiter(new MutableClock());

        // Act
        var selection = Record.Exception(() => sut.EnsureAllowed("client", Domains(expectedBudget), selectionLimit));
        var excess = Record.Exception(() => sut.EnsureAllowed("client", ["new.com"], selectionLimit));

        // Assert
        Assert.Null(selection);
        Assert.IsType<ClientCatalogBurstLimitExceededException>(excess);
    }

    [Fact]
    public async Task ConcurrentRequests_CannotReserveMoreThanTheSharedBudget()
    {
        // Arrange
        var sut = CreateLimiter(new MutableClock());
        using var start = new ManualResetEventSlim();
        var tasks = Enumerable.Range(0, 40).Select(i => Task.Run(() =>
        {
            start.Wait();
            return Record.Exception(() => sut.EnsureAllowed("client", Domains(100, $"batch{i}-"), 100));
        })).ToArray();

        // Act
        start.Set();
        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(20, results.Count(result => result == null));
        Assert.All(results.Where(result => result != null), result => Assert.IsType<ClientCatalogBurstLimitExceededException>(result));
    }

    [Theory]
    [InlineData(101)]
    [InlineData(300)]
    [InlineData(1000)]
    [InlineData(5000)]
    public void TrustedSelection_BypassesBudget_AndDoesNotCarryUsageBackToProtectedSelection(int selectionLimit)
    {
        // Arrange
        var sut = CreateLimiter(new MutableClock());
        sut.EnsureAllowed("client", Domains(2000), 100);

        // Act
        var first = Record.Exception(() => sut.EnsureAllowed("client", Domains(5000, "trusted-first"), selectionLimit));
        var preview = Record.Exception(() => sut.EnsureAllowed("client", Domains(5000, "trusted-preview"), selectionLimit, consume: false));
        var second = Record.Exception(() => sut.EnsureAllowed("client", Domains(5000, "trusted-second"), selectionLimit));
        var protectedSelection = Record.Exception(() => sut.EnsureAllowed("client", Domains(100), 100));
        var protectedBudget = Record.Exception(() => sut.EnsureAllowed("client", Domains(1900, "new"), 100));
        var next = Record.Exception(() => sut.EnsureAllowed("client", ["another.com"], 100));

        // Assert
        Assert.Null(first);
        Assert.Null(preview);
        Assert.Null(second);
        Assert.Null(protectedSelection);
        Assert.Null(protectedBudget);
        Assert.IsType<ClientCatalogBurstLimitExceededException>(next);
    }

    [Fact]
    public void StaggeredRequests_ReleaseCapacityGradually()
    {
        // Arrange
        var clock = new MutableClock();
        var sut = CreateLimiter(clock, limit: 3);
        sut.EnsureAllowed("client", ["a.com"], 1);
        clock.Advance(TimeSpan.FromMinutes(1));
        sut.EnsureAllowed("client", ["b.com", "c.com"], 1);

        // Act
        clock.Advance(TimeSpan.FromMinutes(4));
        var smaller = Record.Exception(() => sut.EnsureAllowed("client", ["d.com"], 1));
        var full = Record.Exception(() => sut.EnsureAllowed("client", ["e.com", "f.com"], 1));
        clock.Advance(TimeSpan.FromMinutes(1));
        var recovered = Record.Exception(() => sut.EnsureAllowed("client", ["e.com", "f.com"], 1));

        // Assert
        Assert.Null(smaller);
        Assert.Equal(60, Assert.IsType<ClientCatalogBurstLimitExceededException>(full).RetryAfterSeconds);
        Assert.Null(recovered);
    }

    [Fact]
    public void OversizedSelection_RequiresNarrowingInsteadOfAnImpossibleRetry()
    {
        // Arrange
        var sut = CreateLimiter(new MutableClock(), limit: 2);

        // Act
        var exception = Record.Exception(() => sut.EnsureAllowed("client", ["a.com", "b.com", "c.com"], 1));
        var smaller = Record.Exception(() => sut.EnsureAllowed("client", ["a.com", "b.com"], 1));

        // Assert
        Assert.IsType<RequestValidationException>(exception);
        Assert.Null(smaller);
    }

    [Fact]
    public void RetryAfter_WaitsForAllRequiredSlots_NotJustTheFirstExpiry()
    {
        // Arrange
        var clock = new MutableClock();
        var sut = CreateLimiter(clock, limit: 3);
        sut.EnsureAllowed("client", ["a.com"], 1);
        clock.Advance(TimeSpan.FromMinutes(1));
        sut.EnsureAllowed("client", ["b.com"], 1);
        clock.Advance(TimeSpan.FromMinutes(1));
        sut.EnsureAllowed("client", ["c.com"], 1);

        // Act
        var rejected = Record.Exception(() => sut.EnsureAllowed("client", ["d.com", "e.com"], 1));
        clock.Advance(TimeSpan.FromMinutes(3));
        var stillTooLarge = Record.Exception(() => sut.EnsureAllowed("client", ["d.com", "e.com"], 1));
        clock.Advance(TimeSpan.FromMinutes(1));
        var recovered = Record.Exception(() => sut.EnsureAllowed("client", ["d.com", "e.com"], 1));

        // Assert
        Assert.Equal(240, Assert.IsType<ClientCatalogBurstLimitExceededException>(rejected).RetryAfterSeconds);
        Assert.Equal(60, Assert.IsType<ClientCatalogBurstLimitExceededException>(stillTooLarge).RetryAfterSeconds);
        Assert.Null(recovered);
    }

    [Fact]
    public void RejectedOverlappingRequest_DoesNotRefreshPreviouslyIssuedDomains()
    {
        // Arrange
        var clock = new MutableClock();
        var sut = CreateLimiter(clock, limit: 2);
        sut.EnsureAllowed("client", ["a.com", "b.com"], 1);
        clock.Advance(TimeSpan.FromMinutes(4));

        // Act
        var rejected = Record.Exception(() => sut.EnsureAllowed("client", ["a.com", "c.com"], 1));
        clock.Advance(TimeSpan.FromMinutes(1));
        var recovered = Record.Exception(() => sut.EnsureAllowed("client", ["c.com", "d.com"], 1));

        // Assert
        Assert.Equal(60, Assert.IsType<ClientCatalogBurstLimitExceededException>(rejected).RetryAfterSeconds);
        Assert.Null(recovered);
    }

    private static ClientCatalogBurstLimiter CreateLimiter(TimeProvider clock, int limit = ClientCatalogLimits.DefaultUniqueSitesPerFiveMinutes)
        => new(clock, limit);

    private static string[] Domains(int count, string prefix = "site")
        => Enumerable.Range(1, count).Select(i => $"{prefix}{i}.com").ToArray();

    private sealed class MutableClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 9, 14, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan duration) => _now += duration;
    }
}
