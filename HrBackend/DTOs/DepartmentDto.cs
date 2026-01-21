using System.Collections.Generic;

namespace HrBackend.DTOs;

public class DepartmentDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public int? ParentId { get; set; }
    public int Order { get; set; }
    public int? ManagerId { get; set; }
    
    // For tree structure responses
    public List<DepartmentDto> Children { get; set; } = new List<DepartmentDto>();
}

public class CreateDepartmentDto
{
    public string Name { get; set; }
    public int? ParentId { get; set; }
    public int? ManagerId { get; set; }
}

public class UpdateDepartmentDto
{
    public string Name { get; set; }
    public int? ManagerId { get; set; }
}

public class MoveDepartmentDto
{
    public int? NewParentId { get; set; }
    public int NewOrder { get; set; }
}
