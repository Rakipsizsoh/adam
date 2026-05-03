namespace MyIRC.Domain.Entities.Users
{
    public class UserAccount
    {
        public int Id { get; set; }

        public string Nick { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public string PasswordHash { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsActive { get; set; } = true;
    }
}