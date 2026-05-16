using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using КР_Ханников.Core;

namespace КР_Ханников.Data
{
    public static class DbSeeder
    {
        public static async Task SeedAsync(AppDbContext context)
        {
            // Проверяем, есть ли уже данные. Если есть хотя бы один тикет — не трогаем базу.
            if (context.Tickets.Any()) return;

            // Стандартный пароль для всех тестовых учеток: "password"
            string passHash = BCrypt.Net.BCrypt.EnhancedHashPassword("password", 13);
            var now = DateTime.UtcNow;

            // --- 1. СОЗДАЕМ ПОЛЬЗОВАТЕЛЕЙ (Сотрудники поддержки) ---
            var techUser1 = new User { Username = "tech.ivanov", PasswordHash = passHash, Role = Constants.UserRoles.Support, CreatedAt = now };
            var techUser2 = new User { Username = "tech.smirnov", PasswordHash = passHash, Role = Constants.UserRoles.Support, CreatedAt = now };
            var techUser3 = new User { Username = "tech.orlov", PasswordHash = passHash, Role = Constants.UserRoles.Support, CreatedAt = now };

            context.Users.AddRange(techUser1, techUser2, techUser3);
            await context.SaveChangesAsync();

            // Привязываем их к сущности Employee
            var emp1 = new Employee { UserId = techUser1.Id, Name = "Иван Иванов", Role = Constants.UserRoles.Support };
            var emp2 = new Employee { UserId = techUser2.Id, Name = "Сергей Смирнов", Role = Constants.UserRoles.Support };
            var emp3 = new Employee { UserId = techUser3.Id, Name = "Алексей Орлов", Role = Constants.UserRoles.Support };

            context.Employees.AddRange(emp1, emp2, emp3);
            await context.SaveChangesAsync();

            // --- 2. СОЗДАЕМ ПОЛЬЗОВАТЕЛЕЙ (Клиенты) ---
            var clientUser1 = new User { Username = "client.ooo", PasswordHash = passHash, Role = Constants.UserRoles.Client, CreatedAt = now };
            var clientUser2 = new User { Username = "client.ip", PasswordHash = passHash, Role = Constants.UserRoles.Client, CreatedAt = now };

            context.Users.AddRange(clientUser1, clientUser2);
            await context.SaveChangesAsync();

            var client1 = new Client { UserId = clientUser1.Id, Name = "ООО 'Ромашка'", Email = "info@romashka.ru" };
            var client2 = new Client { UserId = clientUser2.Id, Name = "ИП Петров", Email = "petrov@mail.ru" };

            context.Clients.AddRange(client1, client2);
            await context.SaveChangesAsync();

            // --- 3. ГЕНЕРИРУЕМ ТИКЕТЫ ДЛЯ КРАСИВЫХ ГРАФИКОВ KPI ---
            var tickets = new List<Ticket>
            {
                // Тикеты Иванова (Перегружен, есть просрочки)
                CreateTicket(client1.Id, "Не работает 1С", "При входе выдает ошибку лицензии.", Constants.TicketStatus.InProgress, TicketPriority.Critical, TicketCategory.Software, emp1.Id, now.AddDays(-2), now.AddHours(-5)),
                CreateTicket(client2.Id, "Отвалился VPN", "Сотрудники на удаленке не могут подключиться.", Constants.TicketStatus.InProgress, TicketPriority.High, TicketCategory.Network, emp1.Id, now.AddDays(-1), now.AddHours(2)),
                CreateTicket(client1.Id, "Настройка принтера", "Нужно подключить сетевой принтер в бухгалтерии.", Constants.TicketStatus.Open, TicketPriority.Low, TicketCategory.Hardware, emp1.Id, now.AddHours(-10), now.AddDays(2)),
                CreateTicket(client2.Id, "Зависает сервер БД", "Постоянные таймауты при запросах.", Constants.TicketStatus.InProgress, TicketPriority.Critical, TicketCategory.Network, emp1.Id, now.AddHours(-2), now.AddHours(2)),

                // Тикеты Смирнова (Работает нормально, соблюдает SLA)
                CreateTicket(client1.Id, "Забыл пароль", "Прошу сбросить пароль от почты.", Constants.TicketStatus.Closed, TicketPriority.Normal, TicketCategory.Software, emp2.Id, now.AddDays(-5), now.AddDays(-2), now.AddDays(-4)),
                CreateTicket(client2.Id, "Выдать права новому сотруднику", "Нужен доступ к CRM.", Constants.TicketStatus.Closed, TicketPriority.Normal, TicketCategory.Software, emp2.Id, now.AddDays(-4), now.AddDays(-1), now.AddDays(-3)),
                CreateTicket(client1.Id, "Замена мышки", "Сломалось колесико.", Constants.TicketStatus.InProgress, TicketPriority.Low, TicketCategory.Hardware, emp2.Id, now.AddHours(-5), now.AddDays(5)),

                // Тикеты Орлова (Свободен)
                CreateTicket(client2.Id, "Обновить сертификаты", "Заканчивается срок действия SSL.", Constants.TicketStatus.InProgress, TicketPriority.High, TicketCategory.Network, emp3.Id, now.AddDays(-1), now.AddDays(1)),
                CreateTicket(client1.Id, "Синий экран на ПК", "Компьютер директора уходит в BSOD.", Constants.TicketStatus.Closed, TicketPriority.Critical, TicketCategory.Hardware, emp3.Id, now.AddDays(-10), now.AddDays(-9), now.AddDays(-9).AddHours(2)),

                // Нераспределенные тикеты (Очередь)
                CreateTicket(client2.Id, "Заявка на закупку", "Нужно купить 5 мониторов.", Constants.TicketStatus.Open, TicketPriority.Low, TicketCategory.Hardware, null, now.AddHours(-1), now.AddDays(7)),
                CreateTicket(client1.Id, "Ошибка на сайте", "Клиенты жалуются на нерабочую корзину.", Constants.TicketStatus.Open, TicketPriority.High, TicketCategory.Software, null, now.AddMinutes(-30), now.AddHours(24))
            };

            context.Tickets.AddRange(tickets);
            await context.SaveChangesAsync();

            // --- 4. ДОБАВЛЯЕМ СТАТЬИ В БАЗУ ЗНАНИЙ ---
            context.KnowledgeBase.AddRange(
                new KnowledgeArticle { Title = "Регламент сброса паролей", Content = "Для сброса пароля пользователя необходимо...", AuthorId = techUser1.Id, CreatedAt = now, UpdatedAt = now },
                new KnowledgeArticle { Title = "Настройка VPN (WireGuard)", Content = "Инструкция по генерации конфигов для удаленных сотрудников...", AuthorId = techUser2.Id, CreatedAt = now, UpdatedAt = now }
            );

            // --- 5. ДОБАВЛЯЕМ ПАРУ ЗАПИСЕЙ В АУДИТ ---
            context.AuditLogs.AddRange(
                new AuditLog { Username = "System", Action = "Database Seed", Details = "Успешная генерация тестовых данных.", Timestamp = now }
            );

            await context.SaveChangesAsync();
        }

        private static Ticket CreateTicket(int clientId, string title, string desc, string status, TicketPriority priority, TicketCategory category, int? assigneeId, DateTime createdAt, DateTime dueAt, DateTime? closedAt = null)
        {
            var ticket = new Ticket
            {
                ClientId = clientId,
                Title = title,
                Description = desc,
                Status = status,
                Priority = priority,
                Category = category,
                AssigneeEmployeeId = assigneeId,
                CreatedAt = createdAt,
                UpdatedAt = createdAt,
                DueAt = dueAt,
                ClosedAt = closedAt,
                History = new List<TicketHistory>
                {
                    new TicketHistory { Action = "Создание", Details = "Заявка создана через генератор данных.", Timestamp = createdAt }
                }
            };

            if (closedAt.HasValue)
            {
                ticket.Solution = new Solution
                {
                    ResolutionText = "Проблема успешно устранена.",
                    ResolutionDate = closedAt.Value
                };
            }

            return ticket;
        }
    }
}