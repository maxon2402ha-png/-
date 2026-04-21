using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using КР_Ханников.Core;
using КР_Ханников.Data;

namespace КР_Ханников.Services
{
    public class CrmService
    {
        private readonly HttpClient _httpClient;

        // DTO под формат внешнего CRM-API
        private sealed class CrmClientDto
        {
            public int Id { get; set; }
            public string? Name { get; set; }
            public string? Email { get; set; }
            public string? Company { get; set; }
            // Если в API когда-то появится телефон — добавьте тут поле Phone и
            // отдельно решите, куда его сохранять в вашей модели (например, в отдельный столбец).
        }

        public CrmService(HttpClient httpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

            if (_httpClient.BaseAddress == null)
                _httpClient.BaseAddress = new Uri("https://api.crm.com/");
            if (_httpClient.Timeout == default)
                _httpClient.Timeout = TimeSpan.FromSeconds(15);
        }

        public async Task SyncClientsAsync(AppDbContext context, CancellationToken ct = default)
        {
            if (context is null) throw new ArgumentNullException(nameof(context));

            try
            {
                using var response = await _httpClient.GetAsync("clients", ct);
                if (!response.IsSuccessStatusCode)
                {
                    // тут можно залогировать статус и тело ответа
                    return;
                }

                var json = await response.Content.ReadAsStringAsync(ct);
                var clients = JsonConvert.DeserializeObject<List<CrmClientDto>>(json);

                if (clients == null || clients.Count == 0)
                    return;

                foreach (var dto in clients)
                {
                    // ищем по первичному ключу/внешнему Id
                    var existing = await context.Clients.FirstOrDefaultAsync(c => c.Id == dto.Id, ct);
                    if (existing == null)
                    {
                        // создаём нового клиента на основе DTO
                        var client = new Client
                        {
                            Id = dto.Id,
                            Name = dto.Name ?? string.Empty,
                            Email = dto.Email ?? string.Empty,
                            Company = dto.Company ?? string.Empty
                        };
                        await context.Clients.AddAsync(client, ct);
                    }
                    else
                    {
                        // обновляем только существующие поля модели
                        existing.Name = dto.Name ?? existing.Name;
                        existing.Email = dto.Email ?? existing.Email;
                        existing.Company = dto.Company ?? existing.Company;

                        context.Clients.Update(existing);
                    }
                }

                await context.SaveChangesAsync(ct);
            }
            catch (OperationCanceledException)
            {
                // отмена — норм, игнорируем
            }
            catch (HttpRequestException)
            {
                // сетевые проблемы — можно логировать
            }
            catch (Exception)
            {
                // общий фолбэк — тоже можно логировать
            }
        }
    }
}
