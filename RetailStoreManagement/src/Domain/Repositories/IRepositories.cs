using Domain.Entities;
using Domain.SeedWork;

namespace Domain.Repositories;

/// <summary>
/// Interface cho Product Repository - các query đặc thù
/// </summary>
public interface IProductRepository : IGenericRepository<ProductEntity>
{
    /// <summary>
    /// Lấy sản phẩm theo category
    /// </summary>
    Task<IEnumerable<ProductEntity>> GetByCategoryAsync(int categoryId);
    
    /// <summary>
    /// Lấy sản phẩm theo supplier
    /// </summary>
    Task<IEnumerable<ProductEntity>> GetBySupplierAsync(int supplierId);
    
    /// <summary>
    /// Lấy sản phẩm có tồn kho thấp
    /// </summary>
    Task<IEnumerable<ProductEntity>> GetLowStockProductsAsync(int threshold = 10);
    
    /// <summary>
    /// Tìm kiếm sản phẩm theo tên hoặc barcode
    /// </summary>
    Task<IEnumerable<ProductEntity>> SearchAsync(string keyword);
}

/// <summary>
/// Interface cho Order Repository - các query đặc thù
/// </summary>
public interface IOrderRepository : IGenericRepository<OrderEntity>
{
    /// <summary>
    /// Lấy đơn hàng với đầy đủ thông tin (include items, customer, payments)
    /// </summary>
    Task<OrderEntity?> GetOrderWithDetailsAsync(int orderId);
    
    /// <summary>
    /// Lấy đơn hàng theo khách hàng
    /// </summary>
    Task<IEnumerable<OrderEntity>> GetByCustomerAsync(int customerId);
    
    /// <summary>
    /// Lấy đơn hàng theo ngày
    /// </summary>
    Task<IEnumerable<OrderEntity>> GetByDateRangeAsync(DateTime startDate, DateTime endDate);
    
    /// <summary>
    /// Tính tổng doanh thu
    /// </summary>
    Task<decimal> GetTotalRevenueAsync(DateTime? startDate = null, DateTime? endDate = null);
}

/// <summary>
/// Interface cho Promotion Repository - các query đặc thù
/// </summary>
public interface IPromotionRepository : IGenericRepository<PromotionEntity>
{
    /// <summary>
    /// Lấy khuyến mãi đang hoạt động
    /// </summary>
    Task<IEnumerable<PromotionEntity>> GetActivePromotionsAsync();
    
    /// <summary>
    /// Tìm khuyến mãi theo mã
    /// </summary>
    Task<PromotionEntity?> GetByCodeAsync(string promoCode);
    
    /// <summary>
    /// Kiểm tra mã khuyến mãi có hợp lệ không
    /// </summary>
    Task<bool> IsValidAsync(string promoCode, decimal orderAmount);
}

/// <summary>
/// Interface cho Inventory Repository - các query đặc thù
/// </summary>
public interface IInventoryRepository : IGenericRepository<InventoryEntity>
{
    /// <summary>
    /// Lấy tồn kho theo product
    /// </summary>
    Task<InventoryEntity?> GetByProductIdAsync(int productId);
    
    /// <summary>
    /// Lấy danh sách sản phẩm tồn kho thấp
    /// </summary>
    Task<IEnumerable<InventoryEntity>> GetLowStockAsync(int threshold = 10);
    
    /// <summary>
    /// Cập nhật số lượng tồn kho
    /// </summary>
    Task UpdateQuantityAsync(int productId, int quantityChange, string reason, int userId);
}

/// <summary>
/// Interface cho User Repository - các query đặc thù
/// </summary>
public interface IUserRepository : IGenericRepository<UserEntity>
{
    /// <summary>
    /// Tìm user theo username
    /// </summary>
    Task<UserEntity?> GetByUsernameAsync(string username);
    
    /// <summary>
    /// Kiểm tra username đã tồn tại chưa
    /// </summary>
    Task<bool> ExistsUsernameAsync(string username);
}
