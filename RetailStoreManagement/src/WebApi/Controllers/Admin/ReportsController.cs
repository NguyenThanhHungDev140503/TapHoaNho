using Application.Common.Models;
using Application.Features.Reports.Dtos;
using Application.Features.Reports.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApi.Abstractions;

namespace WebApi.Controllers.Admin;

/// <summary>
/// Controller cho báo cáo
/// </summary>
[Route("api/admin/reports")]
[Authorize(Roles = "Admin")]
public class ReportsController(IMediator mediator) : BaseApiController(mediator)
{
    /// <summary>
    /// Báo cáo doanh thu
    /// </summary>
    [HttpGet("revenue")]
    [ProducesResponseType(typeof(ApiResponse<List<RevenueReportDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRevenueReport([FromQuery] GetRevenueReportQuery query)
    {
        return Ok(await Mediator.Send(query));
    }

    /// <summary>
    /// Báo cáo bán hàng
    /// </summary>
    [HttpGet("sales")]
    [ProducesResponseType(typeof(ApiResponse<List<SalesReportDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSalesReport([FromQuery] GetSalesReportQuery query)
    {
        return Ok(await Mediator.Send(query));
    }

    /// <summary>
    /// Top sản phẩm bán chạy
    /// </summary>
    [HttpGet("top-products")]
    [ProducesResponseType(typeof(ApiResponse<List<TopProductDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTopProducts(
        [FromQuery] DateTime startDate, 
        [FromQuery] DateTime endDate, 
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        return Ok(await Mediator.Send(new GetTopProductsQuery(startDate, endDate, page, pageSize)));
    }

    /// <summary>
    /// Top khách hàng
    /// </summary>
    [HttpGet("top-customers")]
    [ProducesResponseType(typeof(ApiResponse<List<TopCustomerDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTopCustomers(
        [FromQuery] DateTime startDate, 
        [FromQuery] DateTime endDate, 
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        return Ok(await Mediator.Send(new GetTopCustomersQuery(startDate, endDate, page, pageSize)));
    }

    /// <summary>
    /// Báo cáo khuyến mãi
    /// </summary>
    [HttpGet("promotion")]
    [ProducesResponseType(typeof(ApiResponse<List<PromotionReportDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPromotionReport(
        [FromQuery] DateTime startDate, 
        [FromQuery] DateTime endDate,
        [FromQuery] int? promoId = null,
        [FromQuery] bool includeOrderDetails = false)
    {
        return Ok(await Mediator.Send(new GetPromotionReportQuery(startDate, endDate, promoId, includeOrderDetails)));
    }

    /// <summary>
    /// Dự báo tồn kho
    /// </summary>
    [HttpGet("inventory-forecast")]
    [ProducesResponseType(typeof(ApiResponse<List<InventoryForecastDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInventoryForecast(
        [FromQuery] int? productId,
        [FromQuery] int? categoryId,
        [FromQuery] int lookbackMonths = 3,
        [FromQuery] int leadTimeDays = 7,
        [FromQuery] double safetyStockMultiplier = 1.5)
    {
        return Ok(await Mediator.Send(new GetInventoryForecastQuery(productId, categoryId, lookbackMonths, leadTimeDays, safetyStockMultiplier)));
    }
}
