# TaskFlow API

[![CI](https://github.com/<your-username>/TaskFlow/actions/workflows/ci.yml/badge.svg)](https://github.com/<your-username>/TaskFlow/actions)
![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)
![License](https://img.shields.io/badge/license-MIT-green)

REST Web API quản lý công việc (task) xây dựng bằng **ASP.NET Core 10**, dùng để học và làm mẫu cho các kỹ thuật **web API & networking** trong C#.

## Tính năng

| Nhóm | Nội dung |
|------|----------|
| **REST API** | CRUD đầy đủ, lọc, tìm kiếm, sắp xếp, phân trang, mã trạng thái HTTP chuẩn |
| **Bảo mật** | Đăng ký/đăng nhập, JWT Bearer, băm mật khẩu BCrypt, mỗi user chỉ thấy dữ liệu của mình |
| **Networking** | Typed `HttpClient` gọi API bên ngoài (Nager.Date), retry/timeout/circuit breaker (`AddStandardResilienceHandler`), cache, ánh xạ lỗi mạng → 502/504 |
| **Hạ tầng** | CORS, Rate limiting theo IP, Health check (`/health`), xử lý lỗi tập trung (ProblemDetails RFC 7807) |
| **Dữ liệu** | Entity Framework Core + SQLite |
| **Tài liệu** | Swagger UI (có nút Authorize cho JWT), XML comments |
| **Chất lượng** | Unit test + Integration test (xUnit, `WebApplicationFactory`), GitHub Actions CI, Dockerfile |

## Cấu trúc thư mục

```
TaskFlow/
├── src/TaskFlow.Api/
│   ├── Controllers/      # Auth, Tasks, Holidays, Network
│   ├── Services/         # AuthService, TaskService, TokenService, HolidayClient
│   ├── Middleware/       # ExceptionHandlingMiddleware
│   ├── Data/             # AppDbContext (EF Core)
│   ├── Models/ Dtos/ Options/
│   └── Program.cs        # cấu hình DI + pipeline
├── tests/TaskFlow.Tests/ # unit + integration tests
├── .github/workflows/    # CI
├── Dockerfile / docker-compose.yml
└── requests.http         # request mẫu
```

## Yêu cầu

- [.NET SDK 10.0+](https://dotnet.microsoft.com/download)
- (tuỳ chọn) Docker

## Chạy nhanh

```bash
git clone https://github.com/<your-username>/TaskFlow.git
cd TaskFlow
dotnet run --project src/TaskFlow.Api
```

Mở Swagger UI: <http://localhost:5080/swagger>

Chạy test:

```bash
dotnet test
```

Chạy bằng Docker:

```bash
docker compose up --build
# Swagger: http://localhost:8080/swagger
```

## Danh sách endpoint

| Method | URL | Auth | Mô tả |
|--------|-----|:----:|-------|
| POST | `/api/auth/register` | ✗ | Đăng ký, trả về JWT |
| POST | `/api/auth/login` | ✗ | Đăng nhập, trả về JWT |
| GET | `/api/tasks` | ✓ | Danh sách task (`status`, `priority`, `search`, `sortBy`, `descending`, `page`, `pageSize`) |
| GET | `/api/tasks/{id}` | ✓ | Chi tiết task |
| POST | `/api/tasks` | ✓ | Tạo task |
| PUT | `/api/tasks/{id}` | ✓ | Cập nhật task |
| PATCH | `/api/tasks/{id}/complete` | ✓ | Đánh dấu hoàn thành |
| DELETE | `/api/tasks/{id}` | ✓ | Xoá task |
| GET | `/api/holidays/{year}/{countryCode}` | ✓ | Ngày lễ (gọi API ngoài qua HttpClient) |
| GET | `/api/network/info` | ✗ | Thông tin kết nối của request |
| GET | `/health` | ✗ | Health check |

### Ví dụ với curl

```bash
# 1. Đăng ký
curl -X POST http://localhost:5080/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"username":"alice","email":"alice@example.com","password":"Secret123"}'

# 2. Dùng token nhận được
TOKEN="<paste-token>"

curl -X POST http://localhost:5080/api/tasks \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"title":"Học ASP.NET Core","priority":"High"}'

curl -H "Authorization: Bearer $TOKEN" \
  "http://localhost:5080/api/tasks?status=Todo&sortBy=title&page=1&pageSize=10"

curl -H "Authorization: Bearer $TOKEN" http://localhost:5080/api/holidays/2026/VN
```

## Cấu hình

| Khoá | Mô tả |
|------|-------|
| `Jwt__Key` | **Bắt buộc ở Production**, ≥ 32 ký tự. Không commit khoá thật lên GitHub |
| `Jwt__Issuer`, `Jwt__Audience`, `Jwt__ExpiryMinutes` | Thông số JWT |
| `ConnectionStrings__Default` | Chuỗi kết nối SQLite |
| `HolidayApi__BaseUrl` | URL gốc API ngày lễ (phải có dấu `/` cuối) |
| `Cors__AllowedOrigins__0` | Origin được phép gọi từ trình duyệt |
| `Swagger__Enabled` | Bật Swagger ngoài môi trường Development |

## Kiến thức rút ra từ project

- **Typed HttpClient + `IHttpClientFactory`**: tránh lỗi cạn kiệt socket, dễ test bằng cách thay `HttpMessageHandler` (xem `HolidayClientTests`).
- **Resilience**: retry với backoff, timeout từng lần gọi và tổng, circuit breaker.
- **Pipeline middleware**: thứ tự `Exception → CORS → RateLimiter → Authentication → Authorization`.
- **Không rò rỉ dữ liệu giữa user**: mọi truy vấn đều lọc theo `UserId` lấy từ JWT.

## Hướng phát triển

- [ ] Chuyển từ `EnsureCreated` sang EF Core Migrations + PostgreSQL/SQL Server
- [ ] Refresh token
- [ ] API versioning
- [ ] Logging có cấu trúc (Serilog) và OpenTelemetry
- [ ] Phân quyền theo role


## License

[MIT](LICENSE)
