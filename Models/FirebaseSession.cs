using System;

namespace TodoListApp.Models
{
    public class FirebaseSession
    {
        public string LocalId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string IdToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; } = DateTime.MinValue;

        public bool IsExpired => DateTime.UtcNow >= ExpiresAt.AddMinutes(-5);
    }
}
