using System;
using System.Text.Json.Serialization;

namespace TodoListApp.Models
{
    public class UserProfile
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Primeira letra do nome em maiúscula para exibir no avatar
        [JsonIgnore]
        public string Initial
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Name))
                    return "?";

                return Name.Trim().Substring(0, 1).ToUpperInvariant();
            }
        }

        // Estado ativo em tempo de execução para indicação visual no dropdown
        [JsonIgnore]
        public bool IsActive { get; set; }
    }
}
