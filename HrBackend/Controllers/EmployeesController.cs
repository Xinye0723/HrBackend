using Azure.Core;
using HrBackend.DTOs;   
using HrBackend.Models;
using Microsoft.AspNetCore.Authorization; // 引用授權功能
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
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
                    e.OnboardDate,
                    e.Role
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
        [Authorize(Roles = "Admin")] // 【關鍵】只有 Admin 能呼叫
        [HttpPost]
        public async Task<ActionResult<Employee>> CreateEmployee(CreateEmployeeDto dto)
        {
            // 1. 自動產生 EmployeeId (避免前端傳入導致 Race Condition)
            // 鎖定資料表或使用交易可能更好，但基礎解法是先在此處產生
            var lastEmployee = await _context.Employees
                .OrderByDescending(e => e.EmployeeId)
                .FirstOrDefaultAsync();

            string nextId = "A0001";
            if (lastEmployee != null)
            {
                string currentId = lastEmployee.EmployeeId;
                if (int.TryParse(currentId.Substring(1), out int maxNum))
                {
                    maxNum++;
                    nextId = $"A{maxNum:D4}";
                }
            }

            // 再次確認該 ID 是否真的未被使用 (防呆)
            while (await _context.Employees.AnyAsync(e => e.EmployeeId == nextId))
            {
                 // 簡單的碰撞處理：如果剛好有人搶走了，就再加 1
                 // 注意：實務上高併發建議用 Sequence 或轉由資料庫觸發器處理
                 int currentNum = int.Parse(nextId.Substring(1));
                 currentNum++;
                 nextId = $"A{currentNum:D4}";
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
                EmployeeId = nextId, // 使用內部產生的 ID
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
        //編輯員工
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")] // 【關鍵】只有 Admin 能呼叫
        public async Task<IActionResult> UpdateEmployee(string id, [FromBody] CreateEmployeeDto dto)
        {

            var employee = await _context.Employees
                                     .FirstOrDefaultAsync(e => e.EmployeeId == id);
            if (employee == null)
            {
                return NotFound("找不到該員工資料");
            }
            //更新欄位
            employee.FullName = dto.FullName;
            employee.Email = dto.Email;
            employee.Department = dto.Department;
            employee.Position = dto.Position;
            employee.OnboardDate = dto.OnboardDate;
            employee.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();
            return Ok(new { message = $"{dto.FullName}資料更新成功" });
        }
        //刪除員工
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")] 
        public async Task<IActionResult> DeleteEmployee(string id)
        {
            var employee = await _context.Employees
                                     .FirstOrDefaultAsync(e => e.EmployeeId == id);

            if (employee == null)
            {
                // 找不到時回傳標準的 404，這樣前端攔截器就會顯示 "找不到資料"
                return NotFound("找不到該員工資料");
            }

            _context.Employees.Remove(employee);
            await _context.SaveChangesAsync();

            return Ok(new { message = $"{employee.FullName}資料刪除成功" });
        }
        // 取得下一個可用的員工編號
        // GET: api/employees/next-id
        [HttpGet("next-id")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<object>> GetNextId()
        {
            // 1. 找出目前資料庫中最大的 EmployeeId
            // 我們假設格式固定是 "A" + 4位數字
            var lastEmployee = await _context.Employees
                .OrderByDescending(e => e.EmployeeId)
                .FirstOrDefaultAsync();

            string nextId = "A0001"; // 預設值 (如果資料庫是空的)

            if (lastEmployee != null)
            {
                // 2. 拆解字串
                string currentId = lastEmployee.EmployeeId; // 例如 "A0099"

                // 嘗試取得後面的數字部分 (從第1個字元開始切)
                if (int.TryParse(currentId.Substring(1), out int maxNum))
                {
                    // 3. 數字 + 1 並補零
                    maxNum++;
                    // "A" + 數字補成4位 (例如 100 -> "0100")
                    nextId = $"A{maxNum:D4}";
                }
            }

            return Ok(new { nextId });
        }
    }
}