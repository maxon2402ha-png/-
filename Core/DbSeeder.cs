using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using КР_Ханников.Core;

namespace КР_Ханников.Data
{
    public static class DbSeeder
    {
      
        private static readonly Random Rnd = new(20240517);

        public static async Task SeedAsync(AppDbContext context)
        {
           
            if (context.Tickets.Any()) return;

            string passHash = BCrypt.Net.BCrypt.EnhancedHashPassword("password", 13);
            var now = DateTime.UtcNow;


            var techUser1 = new User { Username = "tech.ivanov", PasswordHash = passHash, Role = Constants.UserRoles.Support, CreatedAt = now };
            var techUser2 = new User { Username = "tech.smirnov", PasswordHash = passHash, Role = Constants.UserRoles.Support, CreatedAt = now };
            var techUser3 = new User { Username = "tech.orlov", PasswordHash = passHash, Role = Constants.UserRoles.Support, CreatedAt = now };

            context.Users.AddRange(techUser1, techUser2, techUser3);
            await context.SaveChangesAsync();

            var emp1 = new Employee { UserId = techUser1.Id, Name = "Иван Иванов", Role = Constants.UserRoles.Support, MaxActiveTickets = 8 };
            var emp2 = new Employee { UserId = techUser2.Id, Name = "Сергей Смирнов", Role = Constants.UserRoles.Support, MaxActiveTickets = 6 };
            var emp3 = new Employee { UserId = techUser3.Id, Name = "Алексей Орлов", Role = Constants.UserRoles.Support, MaxActiveTickets = 6 };

            context.Employees.AddRange(emp1, emp2, emp3);
            await context.SaveChangesAsync();

            var clientUser1 = new User { Username = "client.ooo", PasswordHash = passHash, Role = Constants.UserRoles.Client, CreatedAt = now };
            var clientUser2 = new User { Username = "client.ip", PasswordHash = passHash, Role = Constants.UserRoles.Client, CreatedAt = now };

            context.Users.AddRange(clientUser1, clientUser2);
            await context.SaveChangesAsync();

            var client1 = new Client { UserId = clientUser1.Id, Name = "ООО 'Ромашка'", Email = "info@romashka.ru" };
            var client2 = new Client { UserId = clientUser2.Id, Name = "ИП Петров", Email = "petrov@mail.ru" };

            context.Clients.AddRange(client1, client2);
            await context.SaveChangesAsync();

      


            var empToUser = new Dictionary<int, int>
            {
                { emp1.Id, techUser1.Id },
                { emp2.Id, techUser2.Id },
                { emp3.Id, techUser3.Id }
            };

            var employeeIds = new int[] { emp1.Id, emp2.Id, emp3.Id };
            var clientIds = new int[] { client1.Id, client2.Id };

            var tickets = new List<Ticket>();
            int dayCount = 70;

            for (int d = dayCount; d >= 1; d--)
            {
                var day = now.Date.AddDays(-d);


                bool isWeekend = day.DayOfWeek == DayOfWeek.Saturday
                              || day.DayOfWeek == DayOfWeek.Sunday;

                int ticketsToday = isWeekend
                    ? Rnd.Next(0, 3)   
                    : Rnd.Next(2, 7);  

                for (int t = 0; t < ticketsToday; t++)
                {
                    var createdAt = day.AddHours(Rnd.Next(8, 19))
                                        .AddMinutes(Rnd.Next(0, 60));

                    tickets.Add(GenerateTicket(createdAt, employeeIds, clientIds, empToUser, now));
                }
            }


            for (int i = 0; i < 4; i++)
            {
                var createdAt = now.AddHours(-Rnd.Next(1, 12));
                var ticket = GenerateTicket(createdAt, employeeIds, clientIds, empToUser, now);
                ticket.AssigneeEmployeeId = null;
                ticket.Status = Constants.TicketStatus.Open;
                ticket.ClosedAt = null;
                ticket.Solution = null;
                tickets.Add(ticket);
            }

            context.Tickets.AddRange(tickets);
            await context.SaveChangesAsync();

         
            context.KnowledgeBase.AddRange(
                new KnowledgeArticle { Title = "Регламент сброса паролей", Content = "Для сброса пароля пользователя необходимо...", AuthorId = techUser1.Id, CreatedAt = now, UpdatedAt = now },
                new KnowledgeArticle { Title = "Настройка VPN (WireGuard)", Content = "Инструкция по генерации конфигов для удаленных сотрудников...", AuthorId = techUser2.Id, CreatedAt = now, UpdatedAt = now }
            );

 
            context.AuditLogs.Add(
                new AuditLog { Username = "System", Action = "Database Seed", Details = $"Сгенерировано {tickets.Count} тестовых тикетов за {dayCount} дней.", Timestamp = now }
            );

            await context.SaveChangesAsync();
        }


        private static readonly (string Title, string Desc, TicketCategory Cat)[] Templates =
        {
            ("Не работает 1С", "При входе выдает ошибку лицензии.", TicketCategory.Software),
            ("Отвалился VPN", "Сотрудники на удаленке не могут подключиться.", TicketCategory.Network),
            ("Настройка принтера", "Нужно подключить сетевой принтер.", TicketCategory.Hardware),
            ("Зависает сервер БД", "Постоянные таймауты при запросах.", TicketCategory.Network),
            ("Забыл пароль", "Прошу сбросить пароль от почты.", TicketCategory.Software),
            ("Выдать права сотруднику", "Нужен доступ к CRM.", TicketCategory.Software),
            ("Замена мышки", "Сломалось колесико.", TicketCategory.Hardware),
            ("Обновить сертификаты", "Заканчивается срок действия SSL.", TicketCategory.Network),
            ("Синий экран на ПК", "Компьютер уходит в BSOD.", TicketCategory.Hardware),
            ("Ошибка на сайте", "Клиенты жалуются на нерабочую корзину.", TicketCategory.Software),
            ("Медленный интернет", "Низкая скорость в офисе.", TicketCategory.Network),
            ("Не открывается почта", "Outlook не запускается.", TicketCategory.Software),
        };

        private static readonly TicketPriority[] Priorities =
        {
            TicketPriority.Low, TicketPriority.Normal, TicketPriority.Normal,
            TicketPriority.High, TicketPriority.Critical
        };

                                private static Ticket GenerateTicket(
            DateTime createdAt, int[] employeeIds, int[] clientIds,
            Dictionary<int, int> empToUser, DateTime now)
        {
            var tpl = Templates[Rnd.Next(Templates.Length)];
            var priority = Priorities[Rnd.Next(Priorities.Length)];
            int assigneeId = employeeIds[Rnd.Next(employeeIds.Length)];
            int clientId = clientIds[Rnd.Next(clientIds.Length)];

            int dueHours = priority switch
            {
                TicketPriority.Critical => 8,
                TicketPriority.High => 24,
                TicketPriority.Normal => 72,
                _ => 120
            };
            var dueAt = createdAt.AddHours(dueHours);

            double ageDays = (now - createdAt).TotalDays;

            string status;
            DateTime? closedAt = null;

          
            if (ageDays > 5 && Rnd.NextDouble() < 0.85)
            {
                status = Constants.TicketStatus.Closed;
                double resolveHours = Rnd.Next(2, 96);
                closedAt = createdAt.AddHours(resolveHours);
                if (closedAt > now) closedAt = now;
            }
            else if (ageDays > 1 && Rnd.NextDouble() < 0.5)
            {
                status = Constants.TicketStatus.InProgress;
            }
            else
            {
                status = Constants.TicketStatus.Open;
            }

            var ticket = new Ticket
            {
                ClientId = clientId,
                Title = tpl.Title,
                Description = tpl.Desc,
                Status = status,
                Priority = priority,
                Category = tpl.Cat,
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

            
            var firstReplyAt = createdAt.AddHours(Rnd.Next(1, 9));
            if (firstReplyAt < now && empToUser.TryGetValue(assigneeId, out int authorUserId))
            {
                ticket.Comments = new List<TicketComment>
                {
                    new TicketComment
                    {
                        UserId = authorUserId,
                        Text = "Принято в работу, разбираемся.",
                        IsInternal = false,
                        CreatedAt = firstReplyAt
                    }
                };
            }

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