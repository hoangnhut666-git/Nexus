using Nexus.Data.Entities;

namespace Nexus.Services.Orders.Models;

public sealed class AdminOrderQuery
{
    public OrderStatus? Status { get; set; }

    public string? Search { get; set; }

    public DateTime? FromDate { get; set; }

    public DateTime? ToDate { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 10;
}
