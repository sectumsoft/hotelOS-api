using HotelManagement.Application.Common.Models;
using HotelManagement.Application.Features.Bookings.Commands;
using HotelManagement.Application.Features.Bookings.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HotelManagement.API.Models;

namespace HotelManagement.API.Controllers;

public class CheckInFormRequest
{
    public Guid BookingId { get; set; }
    public decimal AmountReceived { get; set; }
    public List<CheckInGuestFormDto> Guests { get; set; } = new();
}

public class CheckInGuestFormDto
{
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? IdType { get; set; }
    public string? IdNumber { get; set; }
}

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class BookingsController : ControllerBase
{
    private readonly IMediator _mediator;
    public BookingsController(IMediator mediator) { _mediator = mediator; }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<BookingDto>>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] DateTime? checkInFrom,
        [FromQuery] DateTime? checkInTo,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 12)
    {
        var result = await _mediator.Send(new GetBookingsQuery(search, status, checkInFrom, checkInTo, pageNumber, pageSize));
        return Ok(ApiResponse<PagedResult<BookingDto>>.Ok(result));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiResponse<BookingDto>>> GetById(Guid id)
    {
        var result = await _mediator.Send(new GetBookingsQuery(null, null, null, null, 1, 1000));
        var booking = result.Items.FirstOrDefault(b => b.Id == id);
        if (booking == null) return NotFound(ApiResponse<BookingDto>.Fail("Booking not found"));
        return Ok(ApiResponse<BookingDto>.Ok(booking));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<Guid>>> Create([FromBody] CreateBookingCommand command)
    {
        var id = await _mediator.Send(command);
        return Ok(ApiResponse<Guid>.Ok(id, "Booking created successfully"));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<bool>>> Update(Guid id, [FromBody] CreateBookingCommand command)
    {
        return Ok(ApiResponse<bool>.Ok(true, "Booking updated"));
    }

    [HttpPost("checkin")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ApiResponse<bool>>> CheckIn([FromForm] CheckInFormRequest request)
    {
        var files = Request.Form.Files;
        var guestDtos = new List<CheckInGuestDto>();

        for (int i = 0; i < request.Guests.Count; i++)
        {
            var guest = request.Guests[i];
            string? idProofUrl = null;

            var file = files[$"idProof_{i}"];
            if (file != null)
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                Directory.CreateDirectory(uploadsFolder);
                var fileName = $"{Guid.NewGuid()}_{file.FileName}";
                var filePath = Path.Combine(uploadsFolder, fileName);
                using var stream = new FileStream(filePath, FileMode.Create);
                await file.CopyToAsync(stream);
                idProofUrl = $"/uploads/{fileName}";
            }

            guestDtos.Add(new CheckInGuestDto
            {
                Name = guest.FullName,
                Phone = guest.Phone ?? "",
                Address = guest.Address,
                IdProofType = guest.IdType,
                IdProofNumber = guest.IdNumber,
                IdProofUrl = idProofUrl
            });
        }

        var result = await _mediator.Send(new CheckInCommand(
            request.BookingId,
            request.AmountReceived,
            guestDtos
        ));

        if (!result) return BadRequest(ApiResponse<bool>.Fail("Check-in failed"));
        return Ok(ApiResponse<bool>.Ok(true, "Guest checked in successfully"));
    }

    [HttpPost("{id}/checkout")]
    public async Task<ActionResult<ApiResponse<bool>>> CheckOut(Guid id)
    {
        var result = await _mediator.Send(new CheckOutCommand(id));
        if (!result) return BadRequest(ApiResponse<bool>.Fail("Check-out failed"));
        return Ok(ApiResponse<bool>.Ok(true, "Guest checked out successfully"));
    }

    [HttpPost("{id}/cancel")]
    public async Task<ActionResult<ApiResponse<bool>>> Cancel(Guid id)
    {
        var result = await _mediator.Send(new CancelBookingCommand(id));
        if (!result) return BadRequest(ApiResponse<bool>.Fail("Cancellation failed"));
        return Ok(ApiResponse<bool>.Ok(true, "Booking cancelled"));
    }

    [HttpPost("{id}/generate-bill")]
    public async Task<ActionResult<ApiResponse<Guid>>> GenerateBill(Guid id, [FromBody] GenerateBillRequest request)
    {
        var command = new GenerateBillCommand(
            id,
            request.ExtraServices.Select(s => new BillServiceItem(s.Description, s.Amount, s.Quantity)).ToList(),
            request.DiscountAmount,
            request.TaxPercent,
            request.Notes);
        var billId = await _mediator.Send(command);
        return Ok(ApiResponse<Guid>.Ok(billId, "Bill generated successfully"));
    }

    [HttpGet("{id}/bill")]
    public async Task<ActionResult<ApiResponse<BillDto>>> GetBill(Guid id)
    {
        var bill = await _mediator.Send(new GetBillQuery(id));
        if (bill == null) return NotFound(ApiResponse<BillDto>.Fail("Bill not found"));
        return Ok(ApiResponse<BillDto>.Ok(bill));
    }
}