using MediatR;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using HotelManagement.Application.Common.Interfaces;
using HotelManagement.Application.Common.Models;

namespace HotelManagement.Application.Features.Rooms.Queries;

public record GetRoomsQuery(string? Search, string? Status, string? RoomType, int PageNumber = 1, int PageSize = 12) : IRequest<PagedResult<RoomDto>>;
public record GetRoomByIdQuery(Guid Id) : IRequest<RoomDto?>;

public class RoomDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public string RoomType { get; set; } = string.Empty;
    public decimal PricePerNight { get; set; }
    public string? Description { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<string> Amenities { get; set; } = new();
    public List<RoomImageDto> Images { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class RoomImageDto
{
    public Guid Id { get; set; }
    public Guid RoomId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
}

public class GetRoomsQueryHandler : IRequestHandler<GetRoomsQuery, PagedResult<RoomDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ITenantService _tenantService;
    private readonly IMapper _mapper;

    public GetRoomsQueryHandler(IApplicationDbContext context, ITenantService tenantService, IMapper mapper)
    {
        _context = context; _tenantService = tenantService; _mapper = mapper;
    }

    public async Task<PagedResult<RoomDto>> Handle(GetRoomsQuery request, CancellationToken ct)
    {
        var query = _context.Rooms
            .Include(r => r.Images)
            .Include(r => r.RoomAmenities).ThenInclude(ra => ra.Amenity)
            .Where(r => r.TenantId == _tenantService.TenantId && !r.IsDeleted)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(r => r.RoomNumber.Contains(request.Search) || r.Description!.Contains(request.Search));
        if (!string.IsNullOrWhiteSpace(request.Status) && Enum.TryParse<HotelManagement.Domain.Enums.RoomStatus>(request.Status, out var status))
            query = query.Where(r => r.Status == status);
        if (!string.IsNullOrWhiteSpace(request.RoomType) && Enum.TryParse<HotelManagement.Domain.Enums.RoomType>(request.RoomType, out var type))
            query = query.Where(r => r.RoomType == type);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(r => r.RoomNumber)
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        return new PagedResult<RoomDto>
        {
            Items = _mapper.Map<List<RoomDto>>(items),
            TotalCount = total,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize
        };
    }

    public class GetRoomByIdQueryHandler : IRequestHandler<GetRoomByIdQuery, RoomDto?>
    {
        private readonly IApplicationDbContext _context;
        private readonly ITenantService _tenantService;
        private readonly IMapper _mapper;

        public GetRoomByIdQueryHandler(IApplicationDbContext context, ITenantService tenantService, IMapper mapper)
        {
            _context = context;
            _tenantService = tenantService;
            _mapper = mapper;
        }

        public async Task<RoomDto?> Handle(GetRoomByIdQuery request, CancellationToken ct)
        {
            var room = await _context.Rooms
                .Include(r => r.Images)
                .Include(r => r.RoomAmenities).ThenInclude(ra => ra.Amenity)
                .FirstOrDefaultAsync(r => r.Id == request.Id
                    && r.TenantId == _tenantService.TenantId
                    && !r.IsDeleted, ct);

            if (room == null) return null;
            return _mapper.Map<RoomDto>(room);
        }
    }
}
