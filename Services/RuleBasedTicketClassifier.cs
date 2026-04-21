using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using КР_Ханников.Core;

namespace КР_Ханников.Services
{
    public interface ITicketClassifier
    {
        (TicketCategory category, TicketPriority priority) Classify(string title, string description);
    }

    /// <summary>
    /// Классификатор на основе жестких правил (ключевых слов).
    /// Выполняет роль "экспертной системы" и запасного варианта, если ML модель еще не обучена.
    /// </summary>
    public class RuleBasedTicketClassifier : ITicketClassifier
    {
        // Словарь критериев: Категория -> Список ключевых слов
        private static readonly (TicketCategory category, string[] keywords)[] CategoryKeywords =
        {
            (TicketCategory.Billing,  new[] { "счёт", "счет", "оплата", "платеж", "платёж", "инвойс", "bill", "invoice", "цена", "стоимость", "бухгалтерия", "акт", "договор" }),
            (TicketCategory.Account,  new[] { "аккаунт", "учётная", "учетная", "запись", "пароль", "логин", "доступ", "вход", "авторизация", "регистрация", "сброс", "блокировка", "права" }),
            (TicketCategory.Hardware, new[] { "принтер", "компьютер", "монитор", "клавиатура", "мышь", "диск", "ssd", "hdd", "ноутбук", "оборудование", "железо", "экран", "картридж", "сканер", "сервер" }),
            (TicketCategory.Software, new[] { "программа", "приложение", "установка", "лицензия", "обновление", "ошибка", "баг", "crash", "exception", "windows", "office", "excel", "word", "outlook", "зависает", "не запускается" }),
            (TicketCategory.Network,  new[] { "интернет", "сеть", "сетевой", "wi-fi", "wifi", "vpn", "подключение", "маршрутизатор", "роутер", "ping", "пинг", "dns", "днс", "сайт", "портал", "не грузит" }),
            (TicketCategory.Security, new[] { "вирус", "антивирус", "безопасность", "утечка", "фишинг", "phishing", "взлом", "malware", "spyware", "подозрительно", "спам", "атака" }),
            (TicketCategory.Other,    new[] { "прочее", "другое", "консультация", "вопрос", "подскажите" }),
        };

        // Словарь критериев: Приоритет -> Список ключевых слов
        private static readonly string[] CriticalWords = { "не работает", "недоступно", "простой", "падение", "утечка", "срочно", "критично", "краш", "down", "emergency", "ошибка 500", "бизнес встал", "пожар", "взлом" };
        private static readonly string[] HighWords = { "частично не работает", "нельзя работать", "очень медленно", "задержка", "ошибка", "деградация", "degradation", "важно", "быстрее" };
        private static readonly string[] LowWords = { "вопрос", "как", "инструкция", "улучшить", "предложение", "идея", "фича", "feature", "запрос на улучшение", "не горит", "косметика" };

        public (TicketCategory category, TicketPriority priority) Classify(string title, string description)
        {
            // 1. Нормализация текста (убираем лишние пробелы, приводим к нижнему регистру)
            var raw = $"{title} {description}";
            var text = (raw ?? string.Empty).ToLowerInvariant();
            string norm = Regex.Replace(text, @"\s+", " ");

            // 2. Определение Категории (подсчет вхождений ключевых слов)
            var catScores = new Dictionary<TicketCategory, int>();

            foreach (var (category, keys) in CategoryKeywords)
            {
                int score = keys.Sum(k => CountOccurrences(norm, k));
                if (score > 0)
                    catScores[category] = score;
            }

            // Выбираем категорию с максимальным числом совпадений, либо General
            var categoryResult = catScores.Count > 0
                ? catScores.OrderByDescending(kv => kv.Value).First().Key
                : TicketCategory.General;

            // 3. Определение Приоритета (по наличию "триггерных" слов)
            TicketPriority priorityResult = TicketPriority.Normal;

            if (ContainsAny(norm, CriticalWords))
                priorityResult = TicketPriority.Critical;
            else if (ContainsAny(norm, HighWords))
                priorityResult = TicketPriority.High;
            else if (ContainsAny(norm, LowWords))
                priorityResult = TicketPriority.Low;

            return (categoryResult, priorityResult);
        }

        private static bool ContainsAny(string text, IEnumerable<string> words)
            => words.Any(w => text.Contains(w, StringComparison.OrdinalIgnoreCase));

        private static int CountOccurrences(string text, string value)
        {
            if (string.IsNullOrEmpty(value)) return 0;
            int count = 0;
            int start = 0;
            while ((start = text.IndexOf(value, start, StringComparison.OrdinalIgnoreCase)) != -1)
            {
                count++;
                start += value.Length;
            }
            return count;
        }
    }
}