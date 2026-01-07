using System.ComponentModel.DataAnnotations;

namespace HrBackend.DTOs
{
    public class CreateEmployeeDto
    {
        [Required]
        [StringLength(20)]
        public string EmployeeId { get; set; }// 員工編號 (必填)

        [Required]
        [StringLength(50)]
        public string FullName { get; set; }

        [Required]
        [EmailAddress]
        [StringLength(100)]
        public string Email { get; set; }      // Email (必填且格式正確)

        [StringLength(50)]
        public string Department { get; set; } // 部門
        [StringLength(50)]
        public string Position { get; set; }   // 職位

        public DateOnly? OnboardDate { get; set; } // 到職日
    }
}
