using Azure.Core;
using HrBackend.DTOs;   
using HrBackend.Models;
using Microsoft.AspNetCore.Authorization; // 引用授權功能
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
namespace HrBackend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // 關鍵：加上這行，沒登入(沒帶有效Cookie)的人連線會直接被踢掉 (401)
    public class EmployeesController : ControllerBase
    {
        private readonly HRContext _context;

        public EmployeesController(HRContext context)
        {
            _context = context;
        }

        // 取得當前登入者的個人資料
        // GET: api/employees/me
        [HttpGet("me")]
        public async Task<ActionResult<object>> GetMyProfile()
        {
            // 從 Token (Claims) 中取得 EmployeeId
            var employeeId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (employeeId == null) return Unauthorized();

            var employee = await _context.Employees
                .Where(e => e.EmployeeId == employeeId)
                .Select(e => new
                {
                    e.EmployeeId,
                    e.FullName,
                    e.Email,
                    e.Department,
                    e.Position,
                    e.OnboardDate
                })
                .FirstOrDefaultAsync();

            if (employee == null) return NotFound("找不到員工資料");

            return Ok(employee);
        }
        //取得所有員工列表
        //GET: api/employees
        [HttpGet]
        // [Authorize(Roles = "Admin,Manager")] // 未來可以限制只有管理員能看
        public async Task<ActionResult<IEnumerable<object>>> GetAllEmployees()
        {
            var employees = await _context.Employees
                .Select(e => new
                {
                    e.EmployeeId,
                    e.FullName,
                    e.EnglishName,
                    e.Department,
                    e.Position,
                    e.Email,
                    e.Phone,
                    e.IsActive,
                    e.OnboardDate,
                })
                .ToListAsync();
            return Ok(employees);
        }

        //新增員工
        // POST: api/employees
        [HttpPost]
        public async Task<ActionResult<Employee>> CreateEmployee(CreateEmployeeDto dto)
        {
            // 檢查是否已有相同 EmployeeId 的員工
            if (await _context.Employees.AnyAsync(e => e.EmployeeId == dto.EmployeeId))
            {
                return Conflict("已存在相同的員工編號");
            }
            // 2. 檢查 Email 是否重複
            if (await _context.Employees.AnyAsync(e => e.Email == dto.Email))
            {
                return BadRequest("Email 已被使用");
            }
            //建立預設密碼 (員工編號的 BCrypt Hash)
            string defaultPasswordHash = BCrypt.Net.BCrypt.HashPassword($"{dto.EmployeeId}");

            var employee = new Employee
            {
                EmployeeId = dto.EmployeeId,
                FullName = dto.FullName,
                Email = dto.Email,
                Department = dto.Department,
                Position = dto.Position,
                OnboardDate = dto.OnboardDate,
                // 系統預設值
                IsActive = true,
                PasswordHash = defaultPasswordHash,
                Role = "User",
                CreatedAt = DateTime.Now
            };

            _context.Employees.Add(employee);
            await _context.SaveChangesAsync();
            return Ok(new { message = "員工新增成功" });
        }
    }
}