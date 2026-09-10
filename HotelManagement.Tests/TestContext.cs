using HotelManagement.Application.Common.Interfaces;
using HotelManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HotelManagement.Tests;

/// <summary>
/// Spins up the real <see cref="ApplicationDbContext"/> on the EF Core in-memory
/// provider plus lightweight fakes for the ambient services, so MediatR handlers
/// can be exercised without a Postgres instance.
/// </summary>
internal static class TestContext
{
    public static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static ApplicationDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"tests-{Guid.NewGuid()}")
            .EnableSensitiveDataLogging()
            .Options;
        return new ApplicationDbContext(options);
    }

    public sealed class FakeTenantService : ITenantService
    {
        public Guid TenantId => TestContext.TenantId;
        public string TenantName => "Test Hotel";
    }

    public sealed class NullNotificationRecorder : INotificationRecorder
    {
        public Task RecordAsync(string type, string title, string message,
            string? link = null, string? entityId = null, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
