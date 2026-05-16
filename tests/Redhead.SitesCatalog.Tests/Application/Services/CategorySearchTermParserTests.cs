using Microsoft.EntityFrameworkCore;
using Redhead.SitesCatalog.Application.Models;
using Redhead.SitesCatalog.Application.Services;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Tests;

public class CategorySearchTermParserTests
{
    [Fact]
    public void EscapeLikeTerm_EscapesPostgresLikeWildcardsAndEscapeCharacter()
    {
        var escaped = CategorySearchTermParser.EscapeLikeTerm(@"100% sports_betting \ casino");

        Assert.Equal(@"100\% sports\_betting \\ casino", escaped);
    }

    [Fact]
    public void SitesQueryBuilder_CategorySearch_UsesPostgresILikeWithEscapeCharacter()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=redhead_test;Username=test;Password=test")
            .Options;
        using var context = new ApplicationDbContext(options);
        var queryBuilder = new SitesQueryBuilder(context);

        var query = queryBuilder.BuildQuery(context.Sites, new SitesQuery
        {
            CategorySearchTerms = [@"100% sports_betting \ casino"]
        });

        var sql = query.ToQueryString();

        Assert.Contains("ILIKE", sql);
        Assert.Contains("ESCAPE", sql);
        Assert.Contains(@"100\% sports\_betting \\ casino", sql);
    }
}
