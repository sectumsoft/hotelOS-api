using HotelManagement.Application.Common.Interfaces;
using HotelManagement.Infrastructure.Data;
using HotelManagement.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HotelManagement.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<ApplicationDbContext>(opts =>
            opts.UseNpgsql(config.GetConnectionString("DefaultConnection"),
                sql => sql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<ITenantService, TenantService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<INotificationRecorder, NotificationRecorder>();
        services.AddHttpContextAccessor();

        services.Configure<ObjectStorageOptions>(config.GetSection("ObjectStorage"));
        if (string.Equals(config["ObjectStorage:Provider"], "R2", StringComparison.OrdinalIgnoreCase))
            services.AddScoped<IImageService, R2ImageService>();
        else
            services.AddScoped<IImageService, LocalImageService>();

        return services;
    }
}
