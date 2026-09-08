using AutoMapper;
using HotelManagement.Application.Features.Rooms.Queries;
using HotelManagement.Application.Features.Bookings.Queries;
using HotelManagement.Domain.Entities;

namespace HotelManagement.Application.Common.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<Room, RoomDto>()
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()))
            .ForMember(d => d.Amenities, o => o.MapFrom(s => s.RoomAmenities.Select(ra => ra.Amenity.Name).ToList()))
            .ForMember(d => d.Images, o => o.MapFrom(s => s.Images));

        CreateMap<RoomImage, RoomImageDto>();

        CreateMap<Booking, BookingDto>()
            .ForMember(d => d.RoomNumber, o => o.MapFrom(s => s.Room.RoomNumber))
            .ForMember(d => d.RoomType, o => o.MapFrom(s => s.Room.RoomType))
            .ForMember(d => d.Status, o => o.MapFrom(s => s.Status.ToString()));
    }
}
