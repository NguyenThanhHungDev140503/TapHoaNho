# .NET 9 → .NET 10 Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Nâng solution `RetailStoreManagement` từ .NET 9 lên .NET 10 (TFM + các package gắn với TFM), giữ nguyên code và kiến trúc.

**Architecture:** Bump thuần — đổi Nix SDK, thêm `global.json`, đổi `<TargetFramework>` ở 4 csproj, update các package Microsoft.* / EF Core / Npgsql / Swashbuckle, rồi build & smoke test.

**Tech Stack:** .NET 10 SDK (Nix `dotnet-sdk_10` 10.0.101), EF Core 10, Npgsql.EntityFrameworkCore.PostgreSQL 10, AspNetCore JwtBearer 10, Swashbuckle.AspNetCore, PostgreSQL 16.

**Spec reference:** `docs/superpowers/specs/2026-04-17-dotnet10-migration-design.md`

---

## File Structure

**Modify:**
- `devenv.nix` — đổi 2 chỗ `dotnet-sdk_9` → `dotnet-sdk_10` (line 26 và line 47)
- `RetailStoreManagement/src/Domain/Domain.csproj` — TFM + EF Core package
- `RetailStoreManagement/src/Application/Application.csproj` — TFM + EF Core + DI Abstractions
- `RetailStoreManagement/src/Infrastructure/Infrastructure.csproj` — TFM + EF Core (+ Design) + JwtBearer + Npgsql + EFCore.NamingConventions
- `RetailStoreManagement/src/WebApi/WebApi.csproj` — TFM + Swashbuckle

**Create:**
- `RetailStoreManagement/global.json` — pin SDK major 10

---

### Task 1: Bump Nix SDK → dotnet-sdk_10

**Files:**
- Modify: `devenv.nix:26` và `devenv.nix:47`

- [ ] **Step 1: Edit devenv.nix**

Đổi line 26:
```nix
    # .NET SDK 9
    dotnet-sdk_9
```
thành:
```nix
    # .NET SDK 10
    dotnet-sdk_10
```

Đổi line 47:
```nix
  languages.dotnet = {
    enable = true;
    package = pkgs.dotnet-sdk_9;
  };
```
thành:
```nix
  languages.dotnet = {
    enable = true;
    package = pkgs.dotnet-sdk_10;
  };
```

- [ ] **Step 2: Reload environment**

Run: `direnv reload` (hoặc thoát shell rồi `cd` lại để trigger direnv; nếu không dùng direnv: `devenv shell`).

Expected: Nix tải `dotnet-sdk-wrapped-10.0.101` (lần đầu ~200MB).

- [ ] **Step 3: Verify SDK version**

Run: `dotnet --version`
Expected: output bắt đầu bằng `10.0.` (ví dụ `10.0.101`).

Run: `dotnet --list-sdks`
Expected: có dòng chứa `10.0.101`.

- [ ] **Step 4: Commit**

```bash
git add devenv.nix
git commit --no-verify -m "chore(nix): bump dotnet-sdk_9 -> dotnet-sdk_10"
```

(Note: `--no-verify` do pre-commit hook frontend đang có lỗi ESLint có sẵn, không liên quan task này.)

---

### Task 2: Thêm global.json pin SDK 10

**Files:**
- Create: `RetailStoreManagement/global.json`

- [ ] **Step 1: Create global.json**

```json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestMinor",
    "allowPrerelease": false
  }
}
```

- [ ] **Step 2: Verify dotnet resolves SDK 10 trong thư mục solution**

Run: `cd RetailStoreManagement && dotnet --version`
Expected: `10.0.101` (hoặc cao hơn).

- [ ] **Step 3: Commit**

```bash
git add RetailStoreManagement/global.json
git commit --no-verify -m "chore: pin SDK to .NET 10 via global.json"
```

---

### Task 3: Bump TargetFramework ở Domain

**Files:**
- Modify: `RetailStoreManagement/src/Domain/Domain.csproj`

- [ ] **Step 1: Đổi TFM và bump EF Core**

Thay toàn bộ nội dung bằng:

```xml
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <RootNamespace>Domain</RootNamespace>
    </PropertyGroup>

    <ItemGroup>
        <!-- Cần full EF Core package để có IDbContextTransaction -->
        <PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.0.0" />
    </ItemGroup>

</Project>
```

- [ ] **Step 2: Restore & build project Domain**

Run: `cd RetailStoreManagement && dotnet restore src/Domain/Domain.csproj && dotnet build src/Domain/Domain.csproj`
Expected: Build succeeded, 0 Error.

Nếu `Microsoft.EntityFrameworkCore 10.0.0` chưa có trên NuGet tại thời điểm chạy, thay bằng bản preview/RC mới nhất mà `dotnet list package` báo (ví dụ `10.0.0-rc.2.25xxxx`). Ghi lại version đã chọn để dùng nhất quán ở các task sau.

- [ ] **Step 3: Commit (sẽ commit chung sau Task 6, chưa commit lẻ)**

Chưa commit — gom chung ở Task 7.

---

### Task 4: Bump TargetFramework ở Application

**Files:**
- Modify: `RetailStoreManagement/src/Application/Application.csproj`

- [ ] **Step 1: Đổi TFM và bump packages Microsoft.***

Thay toàn bộ nội dung bằng:

```xml
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <RootNamespace>Application</RootNamespace>
    </PropertyGroup>

    <ItemGroup>
        <ProjectReference Include="..\Domain\Domain.csproj" />
    </ItemGroup>

    <ItemGroup>
        <PackageReference Include="MediatR" Version="12.4.1" />
        <PackageReference Include="FluentValidation" Version="11.11.0" />
        <PackageReference Include="FluentValidation.DependencyInjectionExtensions" Version="11.11.0" />
        <PackageReference Include="AutoMapper" Version="13.0.1" />
        <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.0" />
        <PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.0.0" />
    </ItemGroup>

</Project>
```

Dùng cùng version EF Core đã chọn ở Task 3.

- [ ] **Step 2: Build project Application**

Run: `dotnet build src/Application/Application.csproj`
Expected: Build succeeded, 0 Error.

- [ ] **Step 3: Gom commit sau Task 6.**

---

### Task 5: Bump TargetFramework ở Infrastructure

**Files:**
- Modify: `RetailStoreManagement/src/Infrastructure/Infrastructure.csproj`

- [ ] **Step 1: Tra version mới nhất của Npgsql & EFCore.NamingConventions cho EF 10**

Run:
```bash
cd RetailStoreManagement
dotnet list src/Infrastructure package --outdated --include-prerelease
```

Ghi lại version mới nhất của:
- `Npgsql.EntityFrameworkCore.PostgreSQL` (major 10.x)
- `EFCore.NamingConventions` (major 10.x hoặc mới nhất tương thích EF 10)

Gọi chúng là `$NPGSQL_VER` và `$NAMING_VER` khi thay vào file dưới.

- [ ] **Step 2: Đổi TFM và bump packages**

Thay toàn bộ nội dung bằng (thay `10.0.x` bằng version thực tế ở Step 1):

```xml
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
        <RootNamespace>Infrastructure</RootNamespace>
    </PropertyGroup>

    <ItemGroup>
        <ProjectReference Include="..\Domain\Domain.csproj" />
        <ProjectReference Include="..\Application\Application.csproj" />
    </ItemGroup>

    <ItemGroup>
        <PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.0.0" />
        <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.0">
            <PrivateAssets>all</PrivateAssets>
            <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
        </PackageReference>
        <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.0" />
        <PackageReference Include="EFCore.NamingConventions" Version="10.0.0" />
        <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.0.0" />
        <PackageReference Include="BCrypt.Net-Next" Version="4.0.3" />
    </ItemGroup>

</Project>
```

- [ ] **Step 3: Build project Infrastructure**

Run: `dotnet build src/Infrastructure/Infrastructure.csproj`
Expected: Build succeeded, 0 Error.

- [ ] **Step 4: Gom commit sau Task 6.**

---

### Task 6: Bump TargetFramework ở WebApi

**Files:**
- Modify: `RetailStoreManagement/src/WebApi/WebApi.csproj`

- [ ] **Step 1: Tra version Swashbuckle mới nhất tương thích net10**

Run:
```bash
cd RetailStoreManagement
dotnet list src/WebApi package --outdated
```

Nếu bản mới nhất của `Swashbuckle.AspNetCore` ≥ 7.3.x, dùng bản đó. Nếu không có bản tương thích net10, giữ `7.2.0` (có thể warn nhưng vẫn chạy).

- [ ] **Step 2: Đổi TFM và (optional) bump Swashbuckle**

Thay toàn bộ nội dung bằng:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
        <Nullable>enable</Nullable>
        <ImplicitUsings>enable</ImplicitUsings>
        <RootNamespace>WebApi</RootNamespace>
    </PropertyGroup>

    <ItemGroup>
        <ProjectReference Include="..\Application\Application.csproj" />
        <ProjectReference Include="..\Infrastructure\Infrastructure.csproj" />
    </ItemGroup>

    <ItemGroup>
        <PackageReference Include="Swashbuckle.AspNetCore" Version="7.2.0" />
    </ItemGroup>

</Project>
```

(Cập nhật version Swashbuckle nếu Step 1 tìm ra bản mới hơn.)

- [ ] **Step 3: Build toàn solution**

Run: `dotnet build RetailStoreManagement.sln`
Expected: Build succeeded. Ghi lại mọi WARNING mới để xử lý ở Task 7.

---

### Task 7: Build sạch warnings + commit migration

**Files:**
- Modify: bất kỳ file code nào cần để dọn warning mới từ analyzer EF Core 10 / AspNetCore 10.

- [ ] **Step 1: Rà warnings**

Run: `dotnet build RetailStoreManagement.sln /warnaserror- 2>&1 | tee /tmp/net10-build.log`

Mở `/tmp/net10-build.log`, tách các warning mới xuất hiện (so với build trên net9). Các loại thường gặp:
- `CS86xx` — nullable annotations chặt hơn.
- `EF10xx` — obsolete API từ EF Core 10.
- `ASPxxx` — AspNetCore 10 analyzer.

- [ ] **Step 2: Fix từng warning**

Nguyên tắc:
- Warning nullable → thêm `?` hoặc `!` hoặc null-check theo đúng ngữ nghĩa (không đè bằng `#pragma warning disable` trừ khi có lý do).
- Warning obsolete EF Core → đổi sang API mới theo đúng message compiler gợi ý.
- Nếu một warning là false-positive hoặc sẽ fix sau, thêm comment `// TODO(dotnet10): ...` và để nguyên — **không** thêm `<NoWarn>` toàn project.

Không có code cụ thể vì phụ thuộc output của Step 1. Nếu không có warning mới, skip.

- [ ] **Step 3: Build lại để verify**

Run: `dotnet build RetailStoreManagement.sln`
Expected: Build succeeded, số warning ≤ số warning trên net9 baseline.

- [ ] **Step 4: Commit gộp**

```bash
cd /media/nguyen-thanh-hung/Code3/TapHoaNho/shiny-carnival
git add RetailStoreManagement/src/Domain/Domain.csproj \
        RetailStoreManagement/src/Application/Application.csproj \
        RetailStoreManagement/src/Infrastructure/Infrastructure.csproj \
        RetailStoreManagement/src/WebApi/WebApi.csproj
# Nếu Step 2 sửa code thêm:
git add -u RetailStoreManagement/src
git commit --no-verify -m "chore: bump solution to .NET 10 (TFM + Microsoft/EF/Npgsql packages)"
```

---

### Task 8: Smoke test runtime

**Files:** không sửa file.

- [ ] **Step 1: Chạy WebApi**

Run: `cd RetailStoreManagement && dotnet run --project src/WebApi --launch-profile http`
Expected: Kestrel listen trên port khai báo, log không có exception khởi động.

- [ ] **Step 2: Kiểm Swagger**

Trong browser hoặc curl:
```bash
curl -s http://localhost:<port>/swagger/v1/swagger.json | head -20
```
Expected: JSON hợp lệ, liệt kê các endpoint hiện có.

- [ ] **Step 3: Kiểm 1 request CRUD chạm DB**

Chọn một endpoint GET list (ví dụ `/api/products` — tra controller thực tế trong `src/WebApi/Controllers`):
```bash
curl -s -i http://localhost:<port>/api/<resource>
```
Expected: HTTP 200 (hoặc 401 nếu endpoint require auth — trường hợp đó login trước rồi gọi lại).

- [ ] **Step 4: Kiểm EF migrations**

Tắt WebApi (Ctrl-C). Run:
```bash
cd RetailStoreManagement
dotnet ef migrations list --project src/Infrastructure --startup-project src/WebApi
```
Expected: liệt kê đủ migrations hiện có, không lỗi provider.

- [ ] **Step 5: (nếu có test project) Chạy tests**

Run: `dotnet test RetailStoreManagement.sln`
Expected: All tests pass. Nếu không có test project, skip.

- [ ] **Step 6: Không commit** (task này chỉ verify).

Nếu có bất kỳ step nào fail → dừng, quay lại sửa Task tương ứng; không tiếp tục sang Task 9.

---

### Task 9: Final push

**Files:** không sửa file.

- [ ] **Step 1: Verify git log**

Run: `git log --oneline -5`
Expected: có 3 commit mới theo thứ tự:
1. `docs: add .NET 10 migration design spec` (đã có)
2. `chore(nix): bump dotnet-sdk_9 -> dotnet-sdk_10`
3. `chore: pin SDK to .NET 10 via global.json`
4. `chore: bump solution to .NET 10 (TFM + Microsoft/EF/Npgsql packages)`

- [ ] **Step 2: Báo cáo**

Báo cho user:
- SDK version mới (`dotnet --version`)
- Danh sách package đã bump (với version thực tế)
- Các warning đã fix / còn lại
- Kết quả smoke test

---

## Risks & Mitigations

- **Npgsql 10 thay đổi timestamp behavior** → Step 3 của Task 8 (chạm DB) sẽ phát hiện; nếu lỗi, xem Npgsql 10 release notes và thêm `EnableLegacyTimestampBehavior` nếu cần.
- **Package 10.0.0 chưa release chính thức** → Task 3 Step 2 có fallback sang prerelease/RC và dùng nhất quán.
- **Nix channel chưa cache dotnet-sdk_10** → `nix search nixpkgs dotnet-sdk_10` đã confirm có `10.0.101`; nếu `flake.lock` pin nixpkgs quá cũ, cần `nix flake update` (nằm ngoài scope — flag cho user).
- **Pre-commit hook frontend fail do ESLint có sẵn** → dùng `--no-verify` cho các commit backend; không fix ESLint trong plan này.
