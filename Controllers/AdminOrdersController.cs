using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupermarketBot.Services;

namespace SupermarketBot.Controllers;

[ApiController]
[Authorize]
[Route("admin/orders")]
public class AdminOrdersController : ControllerBase
{
    private readonly AdminOrderService _adminOrders;

    public AdminOrdersController(AdminOrderService adminOrders)
    {
        _adminOrders = adminOrders;
    }

    [HttpGet]
    public async Task<IActionResult> GetOrders([FromQuery] string? status, [FromQuery] int? branchId)
    {
        var orders = await _adminOrders.GetOrdersAsync(status, branchId);
        return Ok(orders);
    }

    [HttpGet("{orderNumber}")]
    public async Task<IActionResult> GetOrderDetail(string orderNumber)
    {
        var detail = await _adminOrders.GetOrderDetailAsync(orderNumber);
        if (detail is null) return NotFound(new { message = $"Order {orderNumber} not found." });
        return Ok(detail);
    }

    [HttpPost("{orderNumber}/confirm")]
    public async Task<IActionResult> Confirm(string orderNumber)
    {
        var ok = await _adminOrders.ConfirmOrderAsync(orderNumber);
        if (!ok) return NotFound(new { message = $"Order {orderNumber} not found." });
        return Ok(new { message = "Order confirmed." });
    }

    [HttpPost("{orderNumber}/pack")]
    public async Task<IActionResult> Pack(string orderNumber)
    {
        var ok = await _adminOrders.MarkPackedAsync(orderNumber);
        if (!ok) return NotFound(new { message = $"Order {orderNumber} not found." });
        return Ok(new { message = "Order marked as packed." });
    }

    public record DispatchRequest(string? DriverName, string? DriverPhone);

    [HttpPost("{orderNumber}/dispatch")]
    public async Task<IActionResult> Dispatch(string orderNumber, [FromBody] DispatchRequest? request)
    {
        var ok = await _adminOrders.DispatchAsync(orderNumber, request?.DriverName, request?.DriverPhone);
        if (!ok) return NotFound(new { message = $"Order {orderNumber} not found." });
        return Ok(new { message = "Order marked as out for delivery." });
    }

    [HttpPost("{orderNumber}/delivered")]
    public async Task<IActionResult> Delivered(string orderNumber)
    {
        var ok = await _adminOrders.MarkDeliveredAsync(orderNumber);
        if (!ok) return NotFound(new { message = $"Order {orderNumber} not found." });
        return Ok(new { message = "Marked as delivered. Customer has been asked to confirm receipt." });
    }
}