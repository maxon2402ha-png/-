using Microsoft.ML;
using Microsoft.ML.Data;
using System;
using System.Diagnostics;
using System.IO;
using КР_Ханников.Services;

namespace КР_Ханников.Core
{
    /// <summary>
    /// Интеллектуальный классификатор.
    /// Пытается использовать ML.NET модель. Если модели нет, использует RuleBasedTicketClassifier.
    /// </summary>
    public class TicketClassifier : ITicketClassifier
    {
        private const string CategoryModelFileName = "ticket_category_model.zip";
        private const string PriorityModelFileName = "ticket_priority_model.zip";

        private readonly ITicketClassifier _fallback; // Резервный алгоритм (правила)
        private readonly MLContext _mlContext;

        private PredictionEngine<TextInput, CategoryPrediction>? _categoryEngine;
        private PredictionEngine<TextInput, PriorityPrediction>? _priorityEngine;

        public bool IsModelLoaded => _categoryEngine != null && _priorityEngine != null;

        public TicketClassifier(ITicketClassifier? fallback = null)
        {
            // Если fallback не передан, создаем классификатор по правилам
            _fallback = fallback ?? new RuleBasedTicketClassifier();
            _mlContext = new MLContext();

            TryLoadModels();
        }

        public (TicketCategory category, TicketPriority priority) Classify(string title, string description)
        {
            // Гарантируем, что строки не null (исправление CS8604)
            var safeTitle = title ?? string.Empty;
            var safeDescription = description ?? string.Empty;
            var text = $"{safeTitle} {safeDescription}".Trim();

            // Если текст пустой или модели ML не загрузились — используем правила
            if (string.IsNullOrWhiteSpace(text) || !IsModelLoaded)
            {
                Debug.WriteLine("[Classifier] Используются правила (Rule-Based)");
                return _fallback.Classify(safeTitle, safeDescription);
            }

            try
            {
                Debug.WriteLine("[Classifier] Используется ML.NET");
                var input = new TextInput { Text = text };

                // Предсказание категории
                var catPred = _categoryEngine!.Predict(input);
                var category = Enum.TryParse<TicketCategory>(catPred.CategoryLabel, out var catResult)
                    ? catResult
                    : TicketCategory.General;

                // Предсказание приоритета
                var prioPred = _priorityEngine!.Predict(input);
                var priority = Enum.TryParse<TicketPriority>(prioPred.PriorityLabel, out var prioResult)
                    ? prioResult
                    : TicketPriority.Normal;

                return (category, priority);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TicketClassifier] Ошибка ML: {ex.Message}. Переход на правила.");
                return _fallback.Classify(safeTitle, safeDescription);
            }
        }

        private void TryLoadModels()
        {
            try
            {
                var baseDir = AppContext.BaseDirectory;
                var modelsDir = Path.Combine(baseDir, "MLModels");

                var categoryModelPath = Path.Combine(modelsDir, CategoryModelFileName);
                var priorityModelPath = Path.Combine(modelsDir, PriorityModelFileName);

                if (!File.Exists(categoryModelPath) || !File.Exists(priorityModelPath))
                    return; // Модели еще не обучены

                using var catStream = File.OpenRead(categoryModelPath);
                var categoryModel = _mlContext.Model.Load(catStream, out _);

                using var prioStream = File.OpenRead(priorityModelPath);
                var priorityModel = _mlContext.Model.Load(prioStream, out _);

                _categoryEngine = _mlContext.Model.CreatePredictionEngine<TextInput, CategoryPrediction>(categoryModel);
                _priorityEngine = _mlContext.Model.CreatePredictionEngine<TextInput, PriorityPrediction>(priorityModel);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TicketClassifier] Не удалось загрузить ML модели: {ex.Message}");
            }
        }

        // Внутренние классы данных для ML
        private sealed class TextInput { public string Text { get; set; } = string.Empty; }
        private sealed class CategoryPrediction { [ColumnName("PredictedLabel")] public string CategoryLabel { get; set; } = string.Empty; }
        private sealed class PriorityPrediction { [ColumnName("PredictedLabel")] public string PriorityLabel { get; set; } = string.Empty; }
    }
}