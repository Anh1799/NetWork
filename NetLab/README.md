# NetLab API

[![CI](https://github.com/<your-username>/NetLab/actions/workflows/ci.yml/badge.svg)](https://github.com/<your-username>/NetLab/actions)
![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)
![License](https://img.shields.io/badge/license-MIT-green)

Phòng thí nghiệm **networking** bằng ASP.NET Core / C#. **Không dùng database**: mọi thứ xoay quanh socket, HTTP, DNS, TLS, WebSocket và SSE.

## Bạn sẽ học được gì

| Chủ đề | Nằm ở đâu | API .NET chính |
|--------|-----------|----------------|
| DNS lookup | `NetworkDiagnostics.ResolveDnsAsync` | `Dns.GetHostAddressesAsync` |
| ICMP ping | `NetworkDiagnostics.PingAsync` | `System.Net.NetworkInformation.Ping` |
| Kiểm tra cổng TCP + timeout | `NetworkDiagnostics.CheckTcpAsync` | `TcpClient`, `CancellationTokenSource.CancelAfter` |
| HTTP probe (status, header, độ trễ) | `NetworkDiagnostics.CheckHttpAsync` | `HttpClient`, `HttpCompletionOption.ResponseHeadersRead` |
| Đọc chứng chỉ TLS | `NetworkDiagnostics.GetTlsInfoAsync` | `SslStream`, `X509Certificate2` |
| Chống SSRF / DNS rebinding | `HostGuard`, `GuardedHandler` | `SocketsHttpHandler.ConnectCallback` |
| Typed HttpClient + resilience | `Clients/` | `IHttpClientFactory`, `AddStandardResilienceHandler` |
| Truyền correlation id ra request bên ngoài | `CorrelationIdHandler` | `DelegatingHandler` |
| WebSocket echo + chat broadcast | `WebSockets/` | `System.Net.WebSockets` |
| Server-Sent Events | `StreamController` | `text/event-stream` |
| Rate limiting, CORS, API key | `Program.cs`, `Middleware/` | `RateLimiter`, `CryptographicOperations.FixedTimeEquals` |

## Chạy nhanh

Yêu cầu: [.NET SDK 10.0+](https://dotnet.microsoft.com/download)

```bash
git clone https://github.com/<your-username>/NetLab.git
cd NetLab
dotnet run --project src/NetLab.Api
```

- Trang thử nghiệm (DNS/TCP/TLS, WebSocket chat, SSE): <http://localhost:5090>
- Swagger: <http://localhost:5090/swagger>

```bash
dotnet test     # không cần Internet
```

Docker:

```bash
docker compose up --build   # http://localhost:8080  (nhớ đổi Security__ApiKey)
```

## Endpoint

| Method | URL | Mô tả |
|--------|-----|-------|
| GET | `/api/diagnostics/dns/{host}` | Phân giải DNS |
| GET | `/api/diagnostics/ping/{host}?count=4` | ICMP ping |
| GET | `/api/diagnostics/tcp?host=&port=&timeoutMs=` | Thử mở kết nối TCP |
| GET | `/api/diagnostics/http?url=` | HTTP GET, không theo redirect |
| GET | `/api/diagnostics/tls?host=&port=443` | Thông tin chứng chỉ TLS |
| GET | `/api/external/holidays/{year}/{cc}` | Gọi API ngoài (Nager.Date) qua typed client |
| GET | `/api/external/public-ip` | IP public của server (ipify) |
| GET | `/api/info/request` | IP/port/header của request hiện tại |
| GET | `/api/info/server` | Network interface của server |
| GET | `/api/stream/time?count=5&intervalMs=1000` | Server-Sent Events |
| WS | `/ws/echo` | Echo WebSocket |
| WS | `/ws/chat?name=alice` | Phòng chat broadcast |
| GET | `/health` | Health check |

Ví dụ:

```bash
curl "http://localhost:5090/api/diagnostics/tcp?host=example.com&port=443"
curl "http://localhost:5090/api/diagnostics/tls?host=example.com"
curl -N "http://localhost:5090/api/stream/time?count=5&intervalMs=500"

# WebSocket (cần websocat: brew install websocat)
websocat "ws://localhost:5090/ws/chat?name=alice"
```

## Bảo mật (quan trọng)

Vì API này **kết nối mạng thay cho người gọi**, nó là mục tiêu điển hình của tấn công **SSRF**. Project đã có sẵn:

- **`HostGuard`** chặn loopback, mạng riêng (10.x, 172.16/12, 192.168.x), link-local (`169.254.169.254` – cloud metadata), CGNAT, multicast, IPv6 nội bộ và IPv4-mapped IPv6.
- **`GuardedHandler`** kiểm tra lại IP *ngay lúc kết nối* (chống DNS rebinding) và **tắt tự theo redirect**.
- Giới hạn timeout và số lần ping, kiểm tra định dạng host bằng regex.
- **API key** (`X-API-Key`) cho mọi `/api/*`. Ở môi trường Production app **từ chối khởi động** nếu chưa đặt `Security__ApiKey`.
- Rate limit 100 request/phút/IP; trang web dùng `textContent` để tránh XSS trong chat.

Chỉ đặt `Network:AllowPrivateAddresses=true` khi bạn tự chạy cục bộ để thử với `localhost`.

## Cấu hình

| Khoá | Mô tả |
|------|-------|
| `Security__ApiKey` | API key; bắt buộc ở Production |
| `Network__AllowPrivateAddresses` | Cho phép địa chỉ nội bộ (mặc định `false`) |
| `Network__MaxPingCount`, `Network__MaxTimeoutMs` | Giới hạn an toàn |
| `HolidayApi__BaseUrl`, `PublicIpApi__BaseUrl` | URL gốc API ngoài (có `/` cuối) |
| `Swagger__Enabled` | Bật Swagger ngoài Development |

## Ghi chú vận hành

- **Ping** dùng ICMP, trong Docker/Linux có thể cần quyền (`CAP_NET_RAW`) → API trả `503` nếu không dùng được.
- Sau reverse proxy (nginx, Cloudflare) cần cấu hình `ForwardedHeaders` để `clientIp` đúng; hiện `/api/info/request` chỉ hiển thị `X-Forwarded-For` thô.
- Phòng chat lưu trong bộ nhớ một instance; muốn scale nhiều instance cần backplane (Redis/SignalR).

## Hướng phát triển

- [ ] SignalR thay cho WebSocket thô
- [ ] UDP echo / DNS over HTTPS
- [ ] gRPC service và client
- [ ] OpenTelemetry cho các lệnh gọi HttpClient
- [ ] Traceroute

## Đưa lên GitHub

```bash
git init
git add .
git commit -m "Initial commit: NetLab API"
git branch -M main
git remote add origin https://github.com/<your-username>/NetLab.git
git push -u origin main
```

## License

[MIT](LICENSE)
