using System.Collections.Generic;

namespace TodoListApp.Models
{
    public class ProfileStorageData
    {
        public string? ActiveProfileId { get; set; }
        public List<UserProfile> Profiles { get; set; } = new();
    }
}
