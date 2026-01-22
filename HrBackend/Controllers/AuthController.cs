using HrBackend.DTOs;
using HrBackend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace HrBackend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly HRContext _context;
        private readonly IConfiguration _configuration;
        public AuthController(HRContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }
        // --- 小工具：產生 Hash (取得 Hash 後請刪除此 API) ---
        // 已刪除 insecure endpoint
        // POST: api/auth/login
        [HttpPost("login")]
        public async Task<ActionResult<LoginResponseDto>> Login([FromBody] LoginDto request)
        {
            //檢查帳號是否存在
            var user = await _context.Employees
                .FirstOrDefaultAsync(u => u.EmployeeId == request.EmployeeId);
            if (user == null)
            {
                return BadRequest("帳號或密碼錯誤!");
            }
            //檢查是否在職
            if(!user.IsActive)
            {
                return BadRequest("帳號已被停用，請聯繫管理員!");
            }
            //檢查密碼是否正確
            if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                return BadRequest("帳號或密碼錯誤!");
            }
            //產生 JWT
            string token = CreateToken(user);

            //將 Token 設定為 HttpOnly Cookie
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true, // 若使用 HTTPS，請設為 true
                Expires = DateTimeOffset.Now.AddHours(8),
                SameSite = SameSiteMode.None,
            };
            // 將 Token 寫入回應的 Cookie 中，Key 命名為 "Authorization" 或 "token"
            Response.Cookies.Append("token", token, cookieOptions);
            //回傳結果
            return Ok(new LoginResponseDto
            {
                FullName = user.FullName,
                Role = user.Role
            });
        }
        // 私有方法：製作 JWT
        private string CreateToken(Employee user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.EmployeeId),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Role, user.Role)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                _configuration.GetSection("JwtSettings:Key").Value!));

            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha512Signature);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.Now.AddHours(8), // Token 有效期
                SigningCredentials = creds
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);

            return tokenHandler.WriteToken(token);
        }
        //登出
        [HttpPost("logout")]
        public IActionResult Logout()
        {
            Response.Cookies.Delete("token", new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
            });
            return Ok(new { message = "登出成功" });
        }
        //修改密碼
        [Authorize]
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody]ChangePasswordDto request)
        {
            //取id
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdString))
            {
                return Unauthorized(new { message = "無法識別使用者身分" });
            }
            //找使用者
            var employee = await _context.Employees.FirstOrDefaultAsync(u => u.EmployeeId == userIdString);
            if (employee == null)
            {
                return NotFound(new { message = "使用者不存在" });
            }
            //驗證舊密碼
            if(!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, employee.PasswordHash))
            {
                return BadRequest(new { message = "舊密碼錯誤" });
            }

            //更新新密碼
            string newHashedPassword = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            employee.PasswordHash = newHashedPassword;

            await _context.SaveChangesAsync();
            return Ok(new { message = "密碼更新成功" } );
        }
    }
}
