namespace HrBackend.DTOs
{
    public class LoginResponseDto
    {
        public string Token { get; set; } = string.Empty;//JWT token
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }
}
