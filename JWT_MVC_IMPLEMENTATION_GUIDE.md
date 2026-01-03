# JWT & MVC API Implementation Guide

## ✅ IMPLEMENTED FEATURES

### 1. JWT Authentication Service
- **Location**: `attendence.Services/Services/JwtService.cs`
- **Features**:
  - Token generation with user claims (UserId, Email, Role, FullName)
  - Token validation
  - 8-hour token expiration
  - HMAC SHA256 signing

### 2. Session Management - Browser Close Expiration
- **Location**: `Program.cs`
- **Implementation**:
  - Cookie `MaxAge = null` → Session cookie (expires when browser closes)
  - `IsPersistent = false` → No persistent cookie storage
  - `AllowRefresh = false` → Token doesn't auto-refresh
  - `IdleTimeout = 30 minutes` → Additional security layer

### 3. MVC API Controllers
Created RESTful API endpoints with JWT Bearer authentication:

#### **AuthController** (`/api/auth`)
- `POST /api/auth/login` - Login and get JWT token
  ```json
  Request: { "email": "admin@university.edu", "password": "password" }
  Response: { "token": "eyJhbG...", "userId": 1, "email": "...", "role": "Admin", "expiresIn": 28800 }
  ```
- `GET /api/auth/validate` - Validate current JWT token (requires Bearer token)
- `POST /api/auth/refresh` - Refresh token endpoint (placeholder)

#### **AttendanceController** (`/api/attendance`)
All endpoints require JWT Bearer authentication:
- `GET /api/attendance/student/{studentId}` - Get student attendance records
  - Optional query params: `startDate`, `endDate`
- `GET /api/attendance/student/{studentId}/summary` - Get attendance summary
- `GET /api/attendance/lecture/{lectureId}` - Get lecture attendance (Teacher/Admin only)
- `POST /api/attendance/mark` - Mark attendance for students (Teacher/Admin only)

#### **StudentsController** (`/api/students`)
All endpoints require JWT Bearer authentication:
- `GET /api/students` - Get all students with pagination (Admin/Teacher only)
  - Query params: `sectionId`, `page`, `pageSize`
- `GET /api/students/{id}` - Get student by ID
- `GET /api/students/section/{sectionId}` - Get students by section (Admin/Teacher only)

---

## 🔧 CONFIGURATION

### JWT Settings (appsettings.json)
```json
{
  "JwtSettings": {
    "SecretKey": "UniversityAttendanceSystemSecretKey2024VeryLongAndSecure!@#$",
    "Issuer": "UniversityAttendanceSystem",
    "Audience": "UniversityAttendanceClient",
    "ExpirationMinutes": "480"
  }
}
```

---

## 🧪 TESTING THE API

### 1. Test Login and Get JWT Token
```bash
# Using PowerShell
$body = @{
    email = "admin@university.edu"
    password = "Admin@123"
} | ConvertTo-Json

$response = Invoke-RestMethod -Uri "http://localhost:5100/api/auth/login" -Method Post -Body $body -ContentType "application/json"
$token = $response.token
Write-Host "Token: $token"
```

### 2. Test API with JWT Token
```bash
# Using PowerShell
$headers = @{
    "Authorization" = "Bearer $token"
}

# Validate token
Invoke-RestMethod -Uri "http://localhost:5100/api/auth/validate" -Method Get -Headers $headers

# Get students
Invoke-RestMethod -Uri "http://localhost:5100/api/students?page=1&pageSize=10" -Method Get -Headers $headers
```

### 3. Using Postman or Insomnia
1. **Login**: POST to `http://localhost:5100/api/auth/login`
   - Body (JSON): `{ "email": "admin@university.edu", "password": "Admin@123" }`
   - Copy the `token` from response

2. **Test Protected Endpoint**: GET `http://localhost:5100/api/students`
   - Add Header: `Authorization: Bearer {paste-token-here}`

---

## 🔐 AUTHENTICATION SCHEMES

The application now supports **DUAL AUTHENTICATION**:

1. **Cookie Authentication** (Default for Razor Pages)
   - Used by: `/Account/Login`, `/Admin/*`, `/Teacher/*`, `/Student/*`
   - Expires: When browser/tab closes
   - Scheme: "Cookies"

2. **JWT Bearer Authentication** (For API endpoints)
   - Used by: `/api/*` endpoints
   - Expires: 8 hours after token generation
   - Scheme: "Bearer"

---

## 📋 SESSION EXPIRATION BEHAVIOR

### Current Implementation:
✅ **Session expires when browser/tab closes**
- Cookie `MaxAge = null` - Creates session cookie (not persistent)
- `IsPersistent = false` - No storage beyond browser session
- `AllowRefresh = false` - Prevents automatic extension

### What happens:
1. User logs in → Session cookie created
2. User closes tab → Cookie deleted
3. User closes browser → All session cookies deleted
4. User reopens browser → Must login again

### Additional Security:
- 30-minute idle timeout (if no activity)
- No sliding expiration (cookie doesn't extend on activity)

---

## ❌ WPF LIMITATION

**WPF (Windows Presentation Foundation) cannot be added to this project because:**
- WPF is for **Windows Desktop Applications** (XAML-based UI)
- This project is **ASP.NET Core Web Application** (HTML/Razor-based UI)
- WPF requires `Microsoft.NET.Sdk.WindowsDesktop` SDK
- Incompatible project types

**Alternative Solutions:**
1. **Create a separate WPF project** that consumes the Web API:
   - WPF Desktop App → Calls `/api/auth/login` → Uses JWT tokens → Displays data
   - You'd have a desktop app + web app solution

2. **Use Blazor Desktop** (similar to WPF but web-based):
   - Blazor Hybrid (WebView2) for desktop apps
   - Reuses your existing web components

**If you want a desktop app**, I can help create a separate WPF project that connects to this API.

---

## 📊 ARCHITECTURE DIAGRAM

```
┌─────────────────────────────────────────────────────────────┐
│                    Client Applications                       │
├───────────────────────┬─────────────────────────────────────┤
│   Web Browser         │    Mobile/Desktop App (Future)      │
│   (Razor Pages)       │    (Consumes JWT API)               │
│   Cookie Auth         │    Bearer Token Auth                │
└──────────┬────────────┴────────────────┬────────────────────┘
           │                              │
           ▼                              ▼
┌──────────────────────┐      ┌──────────────────────┐
│  Razor Pages         │      │  MVC API Controllers │
│  /Account/Login      │      │  /api/auth/login     │
│  /Admin/*            │      │  /api/students       │
│  /Teacher/*          │      │  /api/attendance     │
│  /Student/*          │      │                      │
└──────────┬───────────┘      └──────────┬───────────┘
           │                              │
           └──────────────┬───────────────┘
                          ▼
           ┌──────────────────────────────┐
           │  Authentication Middleware   │
           │  - Cookie Auth (Razor)       │
           │  - JWT Bearer (API)          │
           └──────────────┬───────────────┘
                          ▼
           ┌──────────────────────────────┐
           │  Services Layer              │
           │  - JwtService                │
           │  - AuthService               │
           │  - PasswordHasher            │
           └──────────────┬───────────────┘
                          ▼
           ┌──────────────────────────────┐
           │  Entity Framework Core       │
           │  (ApplicationDbContext)      │
           └──────────────┬───────────────┘
                          ▼
           ┌──────────────────────────────┐
           │  SQL Server Database         │
           │  (UniversityAttendanceDB)    │
           └──────────────────────────────┘
```

---

## 🚀 NEXT STEPS

1. **Test the API endpoints** using Postman or PowerShell
2. **Build a mobile app** (React Native, Flutter) that uses JWT API
3. **Create a separate WPF project** if desktop app is needed
4. **Implement refresh tokens** for long-lived sessions
5. **Add API rate limiting** for security
6. **Add Swagger/OpenAPI** documentation for API discovery

---

## 📝 SUMMARY OF CHANGES

| Feature | Status | Details |
|---------|--------|---------|
| JWT Service | ✅ Implemented | Token generation & validation |
| JWT Configuration | ✅ Added | appsettings.json + Program.cs |
| Session Expiration | ✅ Fixed | Expires on browser close |
| MVC Controllers | ✅ Added | Auth, Attendance, Students APIs |
| Dual Authentication | ✅ Configured | Cookie + JWT Bearer |
| API Documentation | ✅ Created | This file |
| WPF Integration | ❌ Not Possible | Incompatible project type |

---

## 🔗 USEFUL LINKS

- JWT Debugger: https://jwt.io/
- Test API: http://localhost:5100/api/auth/login
- Swagger (if needed): Add `Swashbuckle.AspNetCore` package
