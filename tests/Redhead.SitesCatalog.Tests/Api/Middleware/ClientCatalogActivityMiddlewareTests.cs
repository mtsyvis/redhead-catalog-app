using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Redhead.SitesCatalog.Api.Middleware;
using Redhead.SitesCatalog.Domain.Constants;
using Redhead.SitesCatalog.Domain.Exceptions;
using Redhead.SitesCatalog.Infrastructure.Data;

namespace Redhead.SitesCatalog.Tests.Api.Middleware;

public sealed class ClientCatalogActivityMiddlewareTests
{
    [Theory]
    [InlineData(200)]
    [InlineData(400)]
    [InlineData(403)]
    [InlineData(429)]
    [InlineData(500)]
    public async Task Completion_RecordsFinalStatusAfterOuterExceptionHandler(int status)
    {
        // Arrange
        var services = new ServiceCollection();
        var databaseName = Guid.NewGuid().ToString();
        services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(databaseName));
        await using var provider = services.BuildServiceProvider();
        var completion = new CompletionFeature();
        var context = CreateContext(completion);
        var sut = new ClientCatalogActivityMiddleware(http =>
        {
            http.Items["ClientCatalogDomains"] = new[] { "example.com", "example.com" };
            switch (status)
            {
                case 400: throw new RequestValidationException("Invalid filters");
                case 403: throw new ExportDisabledException(AppRoles.Client);
                case 500: throw new InvalidOperationException("Unexpected failure");
                default: http.Response.StatusCode = status; return Task.CompletedTask;
            }
        }, NullLogger<ClientCatalogActivityMiddleware>.Instance);
        var exceptionHandler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);

        // Act
        try
        {
            await sut.InvokeAsync(context, provider.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System);
        }
        catch (Exception error)
        {
            await exceptionHandler.TryHandleAsync(context, error, CancellationToken.None);
        }
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var rowsBeforeCompletion = await db.ClientCatalogRequests.CountAsync();
        await completion.CompleteAsync();

        // Assert
        Assert.Equal(0, rowsBeforeCompletion);
        var activity = await db.ClientCatalogRequests.SingleAsync();
        Assert.Equal(status, context.Response.StatusCode);
        Assert.Equal(status, activity.StatusCode);
        Assert.Equal("client", activity.UserId);
        Assert.Equal(status == 200 ? new[] { "example.com" } : [], activity.Domains);
    }

    [Fact]
    public async Task Completion_StorageFailure_DoesNotFailTheResponse()
    {
        // Arrange
        await using var provider = new ServiceCollection().BuildServiceProvider();
        var completion = new CompletionFeature();
        var context = CreateContext(completion);
        var sut = new ClientCatalogActivityMiddleware(_ => Task.CompletedTask,
            NullLogger<ClientCatalogActivityMiddleware>.Instance);

        // Act
        await sut.InvokeAsync(context, provider.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System);
        var exception = await Record.ExceptionAsync(completion.CompleteAsync);

        // Assert
        Assert.Null(exception);
        Assert.Equal(200, context.Response.StatusCode);
    }

    private static DefaultHttpContext CreateContext(CompletionFeature completion)
    {
        var context = new DefaultHttpContext();
        context.Features.Set<IHttpResponseFeature>(completion);
        context.Response.Body = new MemoryStream();
        context.Request.Method = "POST";
        context.Request.Path = "/api/export/preview";
        context.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "client"), new Claim(ClaimTypes.Role, AppRoles.Client)], "Test"));
        return context;
    }

    // Model the server completing a response after the entire middleware pipeline returns.
    private sealed class CompletionFeature : HttpResponseFeature
    {
        private readonly Stack<(Func<object, Task> Callback, object State)> _callbacks = new();
        public override void OnCompleted(Func<object, Task> callback, object state) => _callbacks.Push((callback, state));
        public async Task CompleteAsync()
        {
            while (_callbacks.TryPop(out var item))
            {
                await item.Callback(item.State);
            }
        }
    }
}
