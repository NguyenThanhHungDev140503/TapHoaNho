using Domain.Entities;
using Domain.Enums;
using Domain.Repositories;
using Infrastructure.Database;
using Infrastructure.SeedWork;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories;

/// <summary>
/// Product Repository - triển khai các query đặc thù cho sản phẩm
/// </summary>
public class ProductRepository(ApplicationDbContext context) 
    : GenericRepository<ProductEntity>(context), IProductRepository
{
    public async Task<IEnumerable<ProductEntity>> GetByCategoryAsync(int categoryId)
    {
        return await _dbContext.Products
            .Where(x => x.CategoryId == categoryId && !x.DeletedAt.HasValue)
            .Include(x => x.Category)
            .Include(x => x.Supplier)
            .Include(x => x.Inventory)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<IEnumerable<ProductEntity>> GetBySupplierAsync(int supplierId)
    {
        return await _dbContext.Products
            .Where(x => x.SupplierId == supplierId && !x.DeletedAt.HasValue)
            .Include(x => x.Category)
            .Include(x => x.Supplier)
            .Include(x => x.Inventory)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<IEnumerable<ProductEntity>> GetLowStockProductsAsync(int threshold = 10)
    {
        return await _dbContext.Products
            .Where(x => !x.DeletedAt.HasValue)
            .Include(x => x.Inventory)
            .Where(x => x.Inventory != null && x.Inventory.Quantity <= threshold)
            .Include(x => x.Category)
            .Include(x => x.Supplier)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<IEnumerable<ProductEntity>> SearchAsync(string keyword)
    {
        var lowerKeyword = keyword.ToLower();
        return await _dbContext.Products
            .Where(x => !x.DeletedAt.HasValue && 
                (x.ProductName.ToLower().Contains(lowerKeyword) || 
                 x.Barcode.ToLower().Contains(lowerKeyword)))
            .Include(x => x.Category)
            .Include(x => x.Supplier)
            .Include(x => x.Inventory)
            .AsNoTracking()
            .ToListAsync();
    }
}

/// <summary>
/// Order Repository - triển khai các query đặc thù cho đơn hàng
/// </summary>
public class OrderRepository(ApplicationDbContext context) 
    : GenericRepository<OrderEntity>(context), IOrderRepository
{
    public async Task<OrderEntity?> GetOrderWithDetailsAsync(int orderId)
    {
        return await _dbContext.Orders
            .Include(x => x.Customer)
            .Include(x => x.User)
            .Include(x => x.Promotion)
            .Include(x => x.OrderItems)
                .ThenInclude(i => i.Product)
            .Include(x => x.Payments)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == orderId && !x.DeletedAt.HasValue);
    }

    public async Task<IEnumerable<OrderEntity>> GetByCustomerAsync(int customerId)
    {
        return await _dbContext.Orders
            .Where(x => x.CustomerId == customerId && !x.DeletedAt.HasValue)
            .Include(x => x.OrderItems)
            .Include(x => x.Payments)
            .OrderByDescending(x => x.OrderDate)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<IEnumerable<OrderEntity>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        return await _dbContext.Orders
            .Where(x => x.OrderDate >= startDate && x.OrderDate <= endDate && !x.DeletedAt.HasValue)
            .Include(x => x.Customer)
            .Include(x => x.OrderItems)
            .OrderByDescending(x => x.OrderDate)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<decimal> GetTotalRevenueAsync(DateTime? startDate = null, DateTime? endDate = null)
    {
        var query = _dbContext.Orders
            .Where(x => x.Status == OrderStatus.Paid && !x.DeletedAt.HasValue);

        if (startDate.HasValue)
            query = query.Where(x => x.OrderDate >= startDate.Value);
        
        if (endDate.HasValue)
            query = query.Where(x => x.OrderDate <= endDate.Value);

        return await query.SumAsync(x => x.TotalAmount - x.DiscountAmount);
    }
}

/// <summary>
/// Promotion Repository - triển khai các query đặc thù cho khuyến mãi
/// </summary>
public class PromotionRepository(ApplicationDbContext context) 
    : GenericRepository<PromotionEntity>(context), IPromotionRepository
{
    public async Task<IEnumerable<PromotionEntity>> GetActivePromotionsAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return await _dbContext.Promotions
            .Where(x => x.Status == PromotionStatus.Active 
                && x.StartDate <= today 
                && x.EndDate >= today
                && (x.UsageLimit == 0 || x.UsedCount < x.UsageLimit)
                && !x.DeletedAt.HasValue)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<PromotionEntity?> GetByCodeAsync(string promoCode)
    {
        return await _dbContext.Promotions
            .FirstOrDefaultAsync(x => x.PromoCode == promoCode && !x.DeletedAt.HasValue);
    }

    public async Task<bool> IsValidAsync(string promoCode, decimal orderAmount)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return await _dbContext.Promotions
            .AnyAsync(x => x.PromoCode == promoCode 
                && x.Status == PromotionStatus.Active
                && x.StartDate <= today 
                && x.EndDate >= today
                && x.MinOrderAmount <= orderAmount
                && (x.UsageLimit == 0 || x.UsedCount < x.UsageLimit)
                && !x.DeletedAt.HasValue);
    }
}

/// <summary>
/// Inventory Repository - triển khai các query đặc thù cho tồn kho
/// </summary>
public class InventoryRepository(ApplicationDbContext context) 
    : GenericRepository<InventoryEntity>(context), IInventoryRepository
{
    public async Task<InventoryEntity?> GetByProductIdAsync(int productId)
    {
        return await _dbContext.Inventory
            .Include(x => x.Product)
            .FirstOrDefaultAsync(x => x.ProductId == productId && !x.DeletedAt.HasValue);
    }

    public async Task<IEnumerable<InventoryEntity>> GetLowStockAsync(int threshold = 10)
    {
        return await _dbContext.Inventory
            .Where(x => x.Quantity <= threshold && !x.DeletedAt.HasValue)
            .Include(x => x.Product)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task UpdateQuantityAsync(int productId, int quantityChange, string reason, int userId)
    {
        var inventory = await _dbContext.Inventory
            .FirstOrDefaultAsync(x => x.ProductId == productId);

        if (inventory == null)
            throw new InvalidOperationException($"Không tìm thấy tồn kho cho sản phẩm {productId}");

        inventory.Quantity += quantityChange;
        inventory.UpdatedAt = DateTime.UtcNow;

        // Ghi lịch sử
        var history = new InventoryHistoryEntity
        {
            ProductId = productId,
            UserId = userId,
            QuantityChange = quantityChange,
            QuantityAfter = inventory.Quantity,
            Reason = reason
        };

        await _dbContext.InventoryHistories.AddAsync(history);
    }
}

/// <summary>
/// User Repository - triển khai các query đặc thù cho người dùng
/// </summary>
public class UserRepository(ApplicationDbContext context) 
    : GenericRepository<UserEntity>(context), IUserRepository
{
    public async Task<UserEntity?> GetByUsernameAsync(string username)
    {
        return await _dbContext.Users
            .FirstOrDefaultAsync(x => x.Username == username && !x.DeletedAt.HasValue);
    }

    public async Task<bool> ExistsUsernameAsync(string username)
    {
        return await _dbContext.Users
            .AnyAsync(x => x.Username == username && !x.DeletedAt.HasValue);
    }
}
