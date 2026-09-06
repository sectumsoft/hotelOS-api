using HotelManagement.Application.Common.Interfaces;
using HotelManagement.Application.Common.Models;
using HotelManagement.Application.Features.Rooms.Commands;
using HotelManagement.Application.Features.Rooms.Queries;
using HotelManagement.API.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq;

namespace HotelManagement.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class RoomsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IImageService _imageService;

    public RoomsController(IMediator mediator, IImageService imageService)
    {
        _mediator = mediator;
        _imageService = imageService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<RoomDto>>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] string? roomType,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 12)
    {
        var result = await _mediator.Send(new GetRoomsQuery(search, status, roomType, pageNumber, pageSize));
        return Ok(ApiResponse<PagedResult<RoomDto>>.Ok(result));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<RoomDto>>> GetById(Guid id)
    {
        var room = await _mediator.Send(new GetRoomByIdQuery(id));
        if (room == null) return NotFound(ApiResponse<RoomDto>.Fail("Room not found"));
        return Ok(ApiResponse<RoomDto>.Ok(room));
    }

    [HttpPost]
    [Consumes("multipart/form-data")]  // <-- tells Swagger/clients this is a form upload
    public async Task<ActionResult<ApiResponse<Guid>>> Create([FromForm] CreateRoomRequest request)
    {
        // 1. validate file types upfront
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        foreach (var file in request.Images)
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext))
                return BadRequest(ApiResponse<Guid>.Fail($"File '{file.FileName}' is not a supported image type."));
        }

        // 2. save each image and collect URLs
        var imageUrls = new List<string>();
        foreach (var file in request.Images)
        {
            var url = await _imageService.SaveImageAsync(file.OpenReadStream(), file.FileName, "rooms");
            imageUrls.Add(url);
        }

        // 3. build command with image URLs and dispatch
        var command = new CreateRoomCommand(
            request.RoomNumber,
            request.RoomType,
            request.PricePerNight,
            request.Description,
            request.Status,
            request.Amenities,
            imageUrls);

        var id = await _mediator.Send(command);
        return Ok(ApiResponse<Guid>.Ok(id, "Room created successfully"));
    }

    // Bulk import from a spreadsheet — the client parses the file and posts rows as JSON.
    [HttpPost("bulk")]
    public async Task<ActionResult<ApiResponse<BulkCreateRoomsResult>>> BulkCreate([FromBody] BulkCreateRoomsRequest request)
    {
        if (request?.Rooms == null || request.Rooms.Count == 0)
            return BadRequest(ApiResponse<BulkCreateRoomsResult>.Fail("No rooms provided."));
        if (request.Rooms.Count > 500)
            return BadRequest(ApiResponse<BulkCreateRoomsResult>.Fail("Maximum 500 rooms per upload."));

        var rows = request.Rooms
            .Select(r => new BulkRoomRow(r.Row, r.RoomNumber, r.RoomType, r.PricePerNight, r.Description, r.Status, r.Amenities))
            .ToList();

        var result = await _mediator.Send(new BulkCreateRoomsCommand(rows));
        var message = $"{result.Added} room(s) added"
            + (result.Skipped.Count > 0 ? $", {result.Skipped.Count} skipped" : "");
        return Ok(ApiResponse<BulkCreateRoomsResult>.Ok(result, message));
    }

    [HttpPut("{id}")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ApiResponse<bool>>> Update(Guid id, [FromForm] UpdateRoomRequest request)
    {
        // 1. validate file types
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        foreach (var file in request.NewImages)
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext))
                return BadRequest(ApiResponse<bool>.Fail($"File '{file.FileName}' is not a supported image type."));
        }

        // 2. save new images
        var imageUrls = new List<string>();
        foreach (var file in request.NewImages)
        {
            var url = await _imageService.SaveImageAsync(file.OpenReadStream(), file.FileName, "rooms");
            imageUrls.Add(url);
        }

        // 3. build command and dispatch
        var command = new UpdateRoomCommand(
            id,
            request.RoomNumber,
            request.RoomType,
            request.PricePerNight,
            request.Description,
            request.Status,
            request.Amenities,
            imageUrls,
            request.DeleteImageUrls);

        var result = await _mediator.Send(command);
        if (!result) return NotFound(ApiResponse<bool>.Fail("Room not found"));
        return Ok(ApiResponse<bool>.Ok(true, "Room updated successfully"));
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(Guid id)
    {
        var result = await _mediator.Send(new DeleteRoomCommand(id));
        if (!result) return NotFound(ApiResponse<bool>.Fail("Room not found"));
        return Ok(ApiResponse<bool>.Ok(true, "Room deleted"));
    }
    [HttpGet("availability/date")]
    public async Task<ActionResult<ApiResponse<List<RoomAvailabilityDto>>>> GetAvailability([FromQuery] DateTime date)
    {
        var result = await _mediator.Send(new GetRoomAvailabilityQuery(date));
        return Ok(ApiResponse<List<RoomAvailabilityDto>>.Ok(result));
    }
}