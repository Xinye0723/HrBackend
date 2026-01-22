using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace HrBackend.Models;

public partial class Department
{
    public int Id { get; set; }

    public string Name { get; set; }

    public int? ParentId { get; set; }

    public int Order { get; set; }

    public int? ManagerId { get; set; } // Optional: Link to an Employee

    [JsonIgnore]
    public virtual Department Parent { get; set; }

    public virtual ICollection<Department> InverseParent { get; set; } = new List<Department>();
}
