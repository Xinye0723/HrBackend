using HrBackend.DTOs;
using HrBackend.Models;
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
        // 用法：啟動後在瀏覽器輸入 /api/auth/hash?pwd=123456
        [HttpGet("hash")]
        public ActionResult<string> GetHash(string pwd)
        {
            if (string.IsNullOrEmpty(pwd))
            {
                return BadRequest("Password is required");
            }
            var hash = BCrypt.Net.BCrypt.HashPassword(pwd);
            return Ok(hash);
        }
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

            //回傳結果
            return Ok(new LoginResponseDto
            {
                Token = token,
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
    }
}
