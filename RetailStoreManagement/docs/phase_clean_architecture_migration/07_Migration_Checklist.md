# 07. Migration Checklist - Danh Sách Công Việc

## Tổng Quan

Checklist chi tiết để thực hiện migration từ kiến trúc hiện tại sang Clean Architecture + CQRS Pattern.

---

## Phase 1: Planning & Documentation ✓

- [x] Phân tích codebase RetailStoreManagement hiện tại
- [x] Đọc và hiểu kiến trúc dự án mẫu CORE-MOBILE-APP
- [x] Đọc tài liệu CQRS_Pattern_Analysis.md
- [x] Đọc tài liệu Clean_Architecture_Analysis.md
- [x] Tạo implementation_plan.md
- [x] Tạo thư mục docs phase với tài liệu chi tiết
- [ ] **User review và phê duyệt kế hoạch**

---

## Phase 2: Domain Layer

### 2.1 Setup Project
- [ ] Tạo folder `src/` trong solution
- [ ] Tạo project `Domain.csproj`
- [ ] Cấu hình project file

### 2.2 SeedWork
- [ ] Tạo `SeedWork/IGenericRepository.cs`
- [ ] Tạo `SeedWork/IUnitOfWork.cs`

### 2.3 Entities (Migrate từ folder hiện tại)
- [ ] Migrate `BaseEntity.cs`
- [ ] Migrate `UserEntity.cs`
- [ ] Migrate `ProductEntity.cs`
- [ ] Migrate `CategoryEntity.cs`
- [ ] Migrate `CustomerEntity.cs`
- [ ] Migrate `SupplierEntity.cs`
- [ ] Migrate `OrderEntity.cs`
- [ ] Migrate `OrderItemEntity.cs`
- [ ] Migrate `PaymentEntity.cs`
- [ ] Migrate `InventoryEntity.cs`
- [ ] Migrate `InventoryHistoryEntity.cs`
- [ ] Migrate `PromotionEntity.cs`
- [ ] Migrate `UserRefreshToken.cs`

### 2.4 Enums (Migrate từ folder hiện tại)
- [ ] Migrate `OrderStatus.cs`
- [ ] Migrate `PaymentStatus.cs`
- [ ] Migrate `PaymentMethod.cs`
- [ ] Migrate `InventoryTransactionType.cs`
- [ ] Migrate `UserRole.cs`

### 2.5 Common
- [ ] Tạo `Common/AppSettings.cs`

### 2.6 Verify
- [ ] Build project Domain thành công
- [ ] Không có circular dependencies

---

## Phase 3: Application Layer

### 3.1 Setup Project
- [ ] Tạo project `Application.csproj`
- [ ] Add reference to Domain project
- [ ] Add NuGet packages (MediatR, FluentValidation, AutoMapper)

### 3.2 Abstractions
- [ ] Tạo `Abstractions/Messaging/ICommand.cs`
- [ ] Tạo `Abstractions/Messaging/ICommandHandler.cs`
- [ ] Tạo `Abstractions/Messaging/IQuery.cs`
- [ ] Tạo `Abstractions/Messaging/IQueryHandler.cs`

### 3.3 Common
- [ ] Tạo `Common/Models/ApiResponse.cs`
- [ ] Tạo `Common/Models/PaginatedResponse.cs`
- [ ] Tạo `Common/Models/PaginationRequest.cs`
- [ ] Tạo `Common/Behaviours/ValidationBehaviour.cs`
- [ ] Tạo `Common/Exceptions/ValidationException.cs`
- [ ] Tạo `Common/Exceptions/NotFoundException.cs`
- [ ] Tạo `Common/Exceptions/BadRequestException.cs`

### 3.4 Features - Auth Module
- [ ] Tạo `Features/Auth/Commands/LoginCommand.cs`
- [ ] Tạo `Features/Auth/Commands/LogoutCommand.cs`
- [ ] Tạo `Features/Auth/Commands/RefreshTokenCommand.cs`
- [ ] Tạo `Features/Auth/Handlers/LoginCommandHandler.cs`
- [ ] Tạo `Features/Auth/Handlers/LogoutCommandHandler.cs`
- [ ] Tạo `Features/Auth/Handlers/RefreshTokenCommandHandler.cs`
- [ ] Tạo `Features/Auth/Validators/LoginCommandValidator.cs`
- [ ] Tạo `Features/Auth/Dtos/LoginRequest.cs`
- [ ] Tạo `Features/Auth/Dtos/LoginResponse.cs`
- [ ] Tạo `Features/Auth/Services/IAuthService.cs`

### 3.5 Features - Products Module
- [ ] Tạo `Features/Products/Commands/CreateProductCommand.cs`
- [ ] Tạo `Features/Products/Commands/UpdateProductCommand.cs`
- [ ] Tạo `Features/Products/Commands/DeleteProductCommand.cs`
- [ ] Tạo `Features/Products/Queries/GetProductsQuery.cs`
- [ ] Tạo `Features/Products/Queries/GetProductByIdQuery.cs`
- [ ] Tạo `Features/Products/Queries/GetProductsByIdsQuery.cs`
- [ ] Tạo các Handlers tương ứng
- [ ] Tạo `Features/Products/Validators/*.cs`
- [ ] Tạo `Features/Products/Dtos/ProductDto.cs`

### 3.6 Features - Categories Module
- [ ] Tạo Commands (Create, Update, Delete)
- [ ] Tạo Queries (GetAll, GetById)
- [ ] Tạo Handlers
- [ ] Tạo Validators
- [ ] Tạo DTOs

### 3.7 Features - Orders Module
- [ ] Tạo `CreateOrderCommand.cs`
- [ ] Tạo `UpdateOrderStatusCommand.cs`
- [ ] Tạo `AddOrderItemCommand.cs`
- [ ] Tạo `UpdateOrderItemCommand.cs`
- [ ] Tạo `DeleteOrderItemCommand.cs`
- [ ] Tạo `DeleteOrderCommand.cs`
- [ ] Tạo `GenerateInvoicePdfCommand.cs`
- [ ] Tạo `GetOrdersQuery.cs`
- [ ] Tạo `GetOrderByIdQuery.cs`
- [ ] Tạo `GetTotalRevenueQuery.cs`
- [ ] Tạo tất cả Handlers
- [ ] Tạo Validators
- [ ] Tạo DTOs

### 3.8 Features - Customers Module
- [ ] Tạo Commands (Create, Update, Delete)
- [ ] Tạo Queries (GetAll, GetById)
- [ ] Tạo Handlers, Validators, DTOs

### 3.9 Features - Suppliers Module
- [ ] Tạo Commands (Create, Update, Delete)
- [ ] Tạo Queries (GetAll, GetById)
- [ ] Tạo Handlers, Validators, DTOs

### 3.10 Features - Inventory Module
- [ ] Tạo Commands (AdjustStock, StockIn, StockOut)
- [ ] Tạo Queries (GetStock, GetHistory)
- [ ] Tạo Handlers, Validators, DTOs

### 3.11 Features - Promotions Module
- [ ] Tạo Commands (Create, Update, Delete, Activate)
- [ ] Tạo Queries (GetAll, GetById)
- [ ] Tạo Handlers, Validators, DTOs

### 3.12 Features - Users Module
- [ ] Tạo Commands (Create, Update, Delete)
- [ ] Tạo Queries (GetAll, GetById)
- [ ] Tạo Handlers, Validators, DTOs

### 3.13 Features - Reports Module
- [ ] Tạo `GetDashboardQuery.cs`
- [ ] Tạo `GetSalesReportQuery.cs`
- [ ] Tạo `GetInventoryReportQuery.cs`
- [ ] Tạo `GetTopProductsQuery.cs`
- [ ] Tạo `GetTopCustomersQuery.cs`
- [ ] Tạo Handlers, DTOs

### 3.14 Profiles & DI
- [ ] Tạo `Profiles/MappingProfile.cs`
- [ ] Tạo `DependencyInjection.cs`

### 3.15 Verify
- [ ] Build project Application thành công
- [ ] Tất cả handlers được register

---

## Phase 4: Infrastructure Layer

### 4.1 Setup Project
- [ ] Tạo project `Infrastructure.csproj`
- [ ] Add references to Domain và Application
- [ ] Add NuGet packages (EF Core, Npgsql, BCrypt)

### 4.2 SeedWork
- [ ] Tạo `SeedWork/GenericRepository.cs`
- [ ] Tạo `SeedWork/UnitOfWork.cs`

### 4.3 Database
- [ ] Migrate `Database/ApplicationDbContext.cs`
- [ ] Tạo Entity Configurations:
  - [ ] `Configurations/UserConfiguration.cs`
  - [ ] `Configurations/ProductConfiguration.cs`
  - [ ] `Configurations/CategoryConfiguration.cs`
  - [ ] `Configurations/CustomerConfiguration.cs`
  - [ ] `Configurations/SupplierConfiguration.cs`
  - [ ] `Configurations/OrderConfiguration.cs`
  - [ ] `Configurations/OrderItemConfiguration.cs`
  - [ ] `Configurations/PaymentConfiguration.cs`
  - [ ] `Configurations/InventoryConfiguration.cs`
  - [ ] `Configurations/PromotionConfiguration.cs`

### 4.4 Services
- [ ] Migrate `Services/AuthService.cs`
- [ ] Tạo `Services/TokenService.cs` (nếu cần tách)

### 4.5 DI
- [ ] Tạo `DependencyInjection.cs`

### 4.6 Verify
- [ ] Build project Infrastructure thành công
- [ ] Database connection hoạt động

---

## Phase 5: WebApi Layer

### 5.1 Setup
- [ ] Refactor `WebApi.csproj` (hoặc tạo mới)
- [ ] Add references to Application và Infrastructure

### 5.2 Abstractions
- [ ] Tạo `Abstractions/BaseApiController.cs`

### 5.3 Infrastructure
- [ ] Tạo `Infrastructure/GlobalExceptionHandler.cs`

### 5.4 Refactor Program.cs
- [ ] Update DI configuration
- [ ] Add Application layer DI
- [ ] Add Infrastructure layer DI
- [ ] Add Exception handler
- [ ] Cleanup old code

### 5.5 Refactor Controllers
- [ ] Refactor `AuthController.cs`
- [ ] Refactor `ProductsController.cs`
- [ ] Refactor `CategoriesController.cs`
- [ ] Refactor `OrdersController.cs`
- [ ] Refactor `CustomersController.cs`
- [ ] Refactor `SuppliersController.cs`
- [ ] Refactor `InventoryController.cs`
- [ ] Refactor `PromotionsController.cs`
- [ ] Refactor `UsersController.cs`
- [ ] Refactor `ReportsController.cs`

### 5.6 Cleanup
- [ ] Xóa Services folder cũ
- [ ] Xóa Interfaces folder cũ (sau khi migrate)
- [ ] Xóa Models folder cũ (sau khi migrate)
- [ ] Xóa Validators folder cũ (sau khi migrate)
- [ ] Update solution structure

### 5.7 Verify
- [ ] Build toàn bộ solution thành công
- [ ] Tất cả controllers hoạt động

---

## Phase 6: Testing & Verification

### 6.1 Build & Compile
- [ ] `dotnet build` thành công
- [ ] Không có warnings quan trọng
- [ ] Không có errors

### 6.2 API Testing

#### Auth Endpoints
- [ ] POST /api/auth/login - Login thành công
- [ ] POST /api/auth/login - Validation error (empty credentials)
- [ ] POST /api/auth/logout - Logout thành công
- [ ] POST /api/auth/refresh-token - Refresh token thành công

#### Products Endpoints
- [ ] GET /api/admin/products - Lấy danh sách với pagination
- [ ] GET /api/admin/products/{id} - Lấy chi tiết
- [ ] GET /api/admin/products/{invalid-id} - 404 Not Found
- [ ] POST /api/admin/products - Tạo mới thành công
- [ ] POST /api/admin/products - Validation error
- [ ] PUT /api/admin/products/{id} - Update thành công
- [ ] DELETE /api/admin/products/{id} - Delete thành công

#### Orders Endpoints
- [ ] GET /api/admin/orders - Lấy danh sách
- [ ] GET /api/admin/orders/{id} - Lấy chi tiết
- [ ] POST /api/admin/orders - Tạo đơn hàng
- [ ] PUT /api/admin/orders/{id}/status - Cập nhật status

#### Other Endpoints
- [ ] Test Categories endpoints
- [ ] Test Customers endpoints
- [ ] Test Suppliers endpoints
- [ ] Test Inventory endpoints
- [ ] Test Promotions endpoints
- [ ] Test Users endpoints
- [ ] Test Reports endpoints

### 6.3 CQRS Flow Verification
- [ ] Verify ValidationBehaviour được trigger
- [ ] Verify GlobalExceptionHandler xử lý exceptions
- [ ] Verify response format thống nhất (ApiResponse<T>)

### 6.4 Documentation
- [ ] Tạo walkthrough.md
- [ ] Cập nhật README.md
- [ ] Swagger documentation đầy đủ

---

## Ước Tính Effort

| Phase | Thời Gian | Files | Complexity |
|-------|-----------|-------|------------|
| Phase 2: Domain | 1-2 ngày | ~20 files | Thấp |
| Phase 3: Application | 4-5 ngày | ~120 files | Cao |
| Phase 4: Infrastructure | 1-2 ngày | ~15 files | Trung bình |
| Phase 5: WebApi | 1-2 ngày | ~15 files | Trung bình |
| Phase 6: Testing | 1-2 ngày | - | Trung bình |
| **TOTAL** | **8-13 ngày** | **~170 files** | - |

---

## Notes

> [!IMPORTANT]
> - Backup code hiện tại trước khi bắt đầu
> - Có thể thực hiện incremental (từng module một)
> - Test kỹ từng phase trước khi chuyển sang phase tiếp theo

> [!TIP]
> - Sử dụng Git branches cho mỗi phase
> - Commit thường xuyên với message rõ ràng
> - Review code trước khi merge

---

*Tài liệu hoàn chỉnh - Sẵn sàng cho User Review*
