using Microsoft.EntityFrameworkCore;
using Microsoft.ML;
using Microsoft.ML.Data;
using System;
using System.IO;
using System.Linq;
using КР_Ханников.Core;
using КР_Ханников.Data;

namespace КР_Ханников.Services
{
    // 1. Формат входных данных для обучения
    public class TicketInput
    {
        [LoadColumn(0)] public string Title { get; set; } = string.Empty;
        [LoadColumn(1)] public string Description { get; set; } = string.Empty;
        [LoadColumn(2)] public string Category { get; set; } = string.Empty;
        [LoadColumn(3)] public string Priority { get; set; } = string.Empty;
    }

    // 2. Форматы выходных данных (Предсказания)
    public class CategoryPrediction
    {
        [ColumnName("PredictedLabel")] public string PredictedCategory { get; set; } = string.Empty;
    }

    public class PriorityPrediction
    {
        [ColumnName("PredictedLabel")] public string PredictedPriority { get; set; } = string.Empty;
    }

    // 3. Основной класс сервиса
    public class MlTicketClassifier
    {
        private readonly MLContext _mlContext;
        private readonly string _categoryModelPath = "TicketCategoryModel.zip";
        private readonly string _priorityModelPath = "TicketPriorityModel.zip";

        public MlTicketClassifier()
        {
            // Фиксированный seed для предсказуемости результатов
            _mlContext = new MLContext(seed: 0);
        }

        /// <summary>
        /// Метод обучения модели на исторических данных из БД
        /// </summary>
        public void TrainModels(AppDbContext dbContext)
        {
            var tickets = dbContext.Tickets.AsNoTracking().ToList();

            // Если тикетов мало, обучаться нет смысла
            if (tickets.Count < 3) return;

            // Подготавливаем данные
            var trainingData = tickets.Select(t => new TicketInput
            {
                Title = t.Title,
                Description = t.Description ?? "",
                Category = t.Category.ToString(),
                Priority = t.Priority.ToString()
            }).ToList();

            var dataView = _mlContext.Data.LoadFromEnumerable(trainingData);

            // --- Пайплайн для предсказания Категории ---
            // 1. Превращаем текст Заголовка и Описания в числа (векторы)
            // 2. Объединяем их
            // 3. Применяем алгоритм многоклассовой классификации (SdcaMaximumEntropy)
            var categoryPipeline = _mlContext.Transforms.Text.FeaturizeText("TitleFeaturized", nameof(TicketInput.Title))
                .Append(_mlContext.Transforms.Text.FeaturizeText("DescFeaturized", nameof(TicketInput.Description)))
                .Append(_mlContext.Transforms.Concatenate("Features", "TitleFeaturized", "DescFeaturized"))
                .Append(_mlContext.Transforms.Conversion.MapValueToKey("Label", nameof(TicketInput.Category)))
                .Append(_mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy("Label", "Features"))
                .Append(_mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

            var categoryModel = categoryPipeline.Fit(dataView);
            _mlContext.Model.Save(categoryModel, dataView.Schema, _categoryModelPath);

            // --- Пайплайн для предсказания Приоритета ---
            var priorityPipeline = _mlContext.Transforms.Text.FeaturizeText("TitleFeaturized", nameof(TicketInput.Title))
                .Append(_mlContext.Transforms.Text.FeaturizeText("DescFeaturized", nameof(TicketInput.Description)))
                .Append(_mlContext.Transforms.Concatenate("Features", "TitleFeaturized", "DescFeaturized"))
                .Append(_mlContext.Transforms.Conversion.MapValueToKey("Label", nameof(TicketInput.Priority)))
                .Append(_mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy("Label", "Features"))
                .Append(_mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

            var priorityModel = priorityPipeline.Fit(dataView);
            _mlContext.Model.Save(priorityModel, dataView.Schema, _priorityModelPath);
        }

        /// <summary>
        /// Метод предсказания для новых тикетов
        /// </summary>
        public (TicketCategory Category, TicketPriority Priority) Predict(string title, string description)
        {
            // Если модели еще нет физически на диске, возвращаем стандартные значения
            if (!File.Exists(_categoryModelPath) || !File.Exists(_priorityModelPath))
            {
                return (TicketCategory.Software, TicketPriority.Normal);
            }

            var input = new TicketInput { Title = title, Description = description };

            // Предсказываем Категорию
            ITransformer categoryModel = _mlContext.Model.Load(_categoryModelPath, out var _);
            var categoryEngine = _mlContext.Model.CreatePredictionEngine<TicketInput, CategoryPrediction>(categoryModel);
            var catPrediction = categoryEngine.Predict(input);

            // Предсказываем Приоритет
            ITransformer priorityModel = _mlContext.Model.Load(_priorityModelPath, out var _);
            var priorityEngine = _mlContext.Model.CreatePredictionEngine<TicketInput, PriorityPrediction>(priorityModel);
            var prioPrediction = priorityEngine.Predict(input);

            // Парсим обратно в Enum
            Enum.TryParse<TicketCategory>(catPrediction.PredictedCategory, out var category);
            Enum.TryParse<TicketPriority>(prioPrediction.PredictedPriority, out var priority);

            return (category, priority);
        }
    }
}