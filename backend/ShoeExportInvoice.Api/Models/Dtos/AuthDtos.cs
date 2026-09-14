using ShoeExportInvoice.Api.Models.Entities;

namespace ShoeExportInvoice.Api.Models.Dtos;

public class LoginRequestDto
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool RememberMe { get; set; } = false;
}

public class UserDto
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
    public Department Department { get; set; }
    public string DepartmentName => Department switch
    {
        Department.Admin => "Ban Giám Đốc",
        Department.Xnk => "Phòng Xuất Nhập Khẩu",
        Department.XnkManager => "Trưởng phòng Xuất Nhập Khẩu",
        Department.Kho => "Kho Thành Phẩm",
        Department.KeToan => "Phòng Kế Toán",
        _ => Department.ToString()
    };
}

public class LoginResponseDto
{
    public string Token { get; set; } = string.Empty;
    public UserDto User { get; set; } = null!;
}

public class ChangePasswordRequestDto
{
    public string OldPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}
