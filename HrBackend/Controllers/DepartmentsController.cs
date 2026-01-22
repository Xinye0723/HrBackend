using HrBackend.DTOs;
using HrBackend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HrBackend.Controllers;

[Route("api/[controller]")]
[ApiController]
public class DepartmentsController : ControllerBase
{
    private readonly HRContext _context;

    public DepartmentsController(HRContext context)
    {
        _context = context;
    }

    [HttpPost("init-schema")]
    [AllowAnonymous] // For testing purposes
    public IActionResult InitSchema()
    {
        // Simplified SQL Server syntax
        var sql = @"
            IF OBJECT_ID('dbo.Departments', 'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[Departments](
                    [Id] [int] IDENTITY(1,1) NOT NULL,
                    [Name] [nvarchar](100) NOT NULL,
                    [ParentId] [int] NULL,
                    [Order] [int] NOT NULL,
                    [ManagerId] [int] NULL,
                    PRIMARY KEY CLUSTERED ([Id] ASC)
                );
                
                ALTER TABLE [dbo].[Departments]  WITH CHECK ADD  CONSTRAINT [FK_Departments_Departments_ParentId] FOREIGN KEY([ParentId])
                REFERENCES [dbo].[Departments] ([Id]);

                ALTER TABLE [dbo].[Departments] CHECK CONSTRAINT [FK_Departments_Departments_ParentId];

                INSERT INTO [dbo].[Departments] (Name, [Order], ParentId) VALUES (N'CEO Office', 0, NULL);
                INSERT INTO [dbo].[Departments] (Name, [Order], ParentId) VALUES (N'HR Department', 1, 1);
                INSERT INTO [dbo].[Departments] (Name, [Order], ParentId) VALUES (N'Engineering', 2, 1);
            END
        ";
        _context.Database.ExecuteSqlRaw(sql);
        return Ok("Schema Initialized");
    }


    // GET: api/Departments
    [HttpGet]
    public async Task<ActionResult<IEnumerable<DepartmentDto>>> GetDepartments()
    {
        var departments = await _context.Departments.OrderBy(d => d.Order).ToListAsync();
        var departmentDtos = departments.Select(d => new DepartmentDto
        {
            Id = d.Id,
            Name = d.Name,
            ParentId = d.ParentId,
            Order = d.Order,
            ManagerId = d.ManagerId
        }).ToList();

        // Build Tree
        var rootDepartments = BuildTree(departmentDtos);
        return rootDepartments;
    }

    private List<DepartmentDto> BuildTree(List<DepartmentDto> allDepts)
    {
        var dict = allDepts.ToDictionary(d => d.Id);
        var roots = new List<DepartmentDto>();

        foreach (var dept in allDepts)
        {
            if (dept.ParentId.HasValue && dict.ContainsKey(dept.ParentId.Value))
            {
                dict[dept.ParentId.Value].Children.Add(dept);
            }
            else
            {
                roots.Add(dept);
            }
        }
        return roots;
    }

    // GET: api/Departments/5
    [HttpGet("{id}")]
    public async Task<ActionResult<Department>> GetDepartment(int id)
    {
        var department = await _context.Departments.FindAsync(id);

        if (department == null)
        {
            return NotFound();
        }

        return department;
    }

    // POST: api/Departments
    [HttpPost]
    [Authorize(Roles = "Admin,HR")]
    public async Task<ActionResult<Department>> PostDepartment(CreateDepartmentDto createDto)
    {
        // Calculate Order: add to end of list for the given parent
        var maxOrder = await _context.Departments
            .Where(d => d.ParentId == createDto.ParentId)
            .MaxAsync(d => (int?)d.Order) ?? 0;

        var department = new Department
        {
            Name = createDto.Name,
            ParentId = createDto.ParentId,
            ManagerId = createDto.ManagerId,
            Order = maxOrder + 1
        };

        _context.Departments.Add(department);
        await _context.SaveChangesAsync();

        return CreatedAtAction("GetDepartment", new { id = department.Id }, department);
    }

    // PUT: api/Departments/5
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,HR")]
    public async Task<IActionResult> PutDepartment(int id, UpdateDepartmentDto updateDto)
    {
        var department = await _context.Departments.FindAsync(id);
        if (department == null) return NotFound();

        department.Name = updateDto.Name;
        department.ManagerId = updateDto.ManagerId;

        await _context.SaveChangesAsync();

        return NoContent();
    }

    // PUT: api/Departments/5/move
    [HttpPut("{id}/move")]
    [Authorize(Roles = "Admin,HR")]
    public async Task<IActionResult> MoveDepartment(int id, MoveDepartmentDto moveDto)
    {
        var department = await _context.Departments.FindAsync(id);
        if (department == null) return NotFound();

        // 1. If parent changes, we need to reorder old siblings
        if (department.ParentId != moveDto.NewParentId)
        {
             // Shift order of old siblings down
             var oldSiblings = await _context.Departments
                 .Where(d => d.ParentId == department.ParentId && d.Order > department.Order)
                 .ToListAsync();
             foreach (var sib in oldSiblings) sib.Order--;

             department.ParentId = moveDto.NewParentId;
        }

        // 2. Handle new siblings Reordering
        // This is a naive implementation; for full drag-drop support we expect the client to be smart or we handle insert logic.
        // Assuming "NewOrder" is the target index.
        
        var newSiblings = await _context.Departments
            .Where(d => d.ParentId == department.ParentId && d.Id != id)
            .OrderBy(d => d.Order)
            .ToListAsync();
        
        // Insert into list
        if (moveDto.NewOrder > newSiblings.Count) moveDto.NewOrder = newSiblings.Count;
        newSiblings.Insert(moveDto.NewOrder, null); // Placeholder logic, actually we just need to shift

        // Update all logical orders
        // Simpler approach: Shift items >= NewOrder up by 1
        
        // Fetch all siblings again including the moved one if parent didn't change (but we need to be careful with concurrency in real apps)
        // Let's do a bulk update logic:
        
        var siblingsToShift = await _context.Departments
            .Where(d => d.ParentId == department.ParentId && d.Id != id && d.Order >= moveDto.NewOrder)
            .ToListAsync();
        
        foreach (var sib in siblingsToShift) sib.Order++;
        
        department.Order = moveDto.NewOrder;

        await _context.SaveChangesAsync();
        
        // Normalize orders to be safe (1, 2, 3...)
        var finalSiblings = await _context.Departments
             .Where(d => d.ParentId == department.ParentId)
             .OrderBy(d => d.Order)
             .ToListAsync();
        
        for(int i=0; i < finalSiblings.Count; i++)
        {
            finalSiblings[i].Order = i;
        }
        await _context.SaveChangesAsync();

        return NoContent();
    }

    // DELETE: api/Departments/5
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin,HR")]
    public async Task<IActionResult> DeleteDepartment(int id)
    {
        var department = await _context.Departments.FindAsync(id);
        if (department == null)
        {
            return NotFound();
        }
        
        // Check for children
        var hasChildren = await _context.Departments.AnyAsync(d => d.ParentId == id);
        if (hasChildren)
        {
            return BadRequest("Cannot delete department with children. Move or delete children first.");
        }

        _context.Departments.Remove(department);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
