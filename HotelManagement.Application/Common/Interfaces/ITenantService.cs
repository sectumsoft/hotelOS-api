namespace HotelManagement.Application.Common.Interfaces;

public interface ITenantService
{
    Guid TenantId { get; }
    string TenantName { get; }
}
