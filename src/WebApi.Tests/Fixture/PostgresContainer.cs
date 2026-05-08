using Testcontainers.PostgreSql;
using TUnit.Core.Interfaces;

namespace WebApi.Tests.Fixture;

public sealed class PostgresContainer : IAsyncInitializer, IAsyncDisposable
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("golden-path")
        .WithUsername("golden-user")
        .WithPassword("golden-pass")
        .Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    public string GetConnectionString()
    {
        return _container.GetConnectionString();
    }
}