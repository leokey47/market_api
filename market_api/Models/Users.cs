public class User
{
    public int UserId { get; set; }  // Идентификатор пользователя
    public string Username { get; set; }
    public string Email { get; set; }
    public string PasswordHash { get; set; }
    public string Role { get; set; }  // Роль пользователя
    public DateTime CreatedAt { get; set; }  // Дата создания
    public string? ProfileImageUrl { get; set; }

    // Бизнес-аккаунт поля
    public bool IsBusiness { get; set; } = false;
    public string? CompanyName { get; set; }
    public string? CompanyAvatar { get; set; }
    public string? CompanyDescription { get; set; }
}