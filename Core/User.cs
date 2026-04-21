using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace КР_Ханников.Core
{
    public class User
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Role { get; set; } = Constants.UserRoles.Client;

        /// <summary>
        /// Путь к картинке аватара
        /// </summary>
        public string? AvatarPath { get; set; }

        /// <summary>
        /// Email пользователя
        /// </summary>
        [MaxLength(100)]
        public string? Email { get; set; }

        /// <summary>
        /// Дата регистрации
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Дата последнего входа
        /// </summary>
        public DateTime? LastLoginAt { get; set; }

        // === НОВЫЕ ПОЛЯ ДЛЯ ВЕРИФИКАЦИИ ===

        /// <summary>
        /// Подтвержден ли Email
        /// </summary>
        public bool IsEmailVerified { get; set; } = false;

        /// <summary>
        /// Код верификации (6 цифр)
        /// </summary>
        [MaxLength(10)]
        public string? VerificationCode { get; set; }

        // ===================================

        // Навигационные свойства (не хранятся в БД, загружаются через Include)
        [NotMapped]
        public Employee? Employee { get; set; }

        [NotMapped]
        public Client? Client { get; set; }
    }
}