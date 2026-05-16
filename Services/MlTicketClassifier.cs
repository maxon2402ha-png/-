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
        public class TicketInput
    {
        [LoadColumn(0)] public string Title { get; set; } = string.Empty;
        [LoadColumn(1)] public string Description { get; set; } = string.Empty;
        [LoadColumn(2)] public string Category { get; set; } = string.Empty;
        [LoadColumn(3)] public string Priority { get; set; } = string.Empty;
    }

        public class CategoryPrediction
    {
        [ColumnName("PredictedLabel")] public string PredictedCategory { get; set; } = string.Empty;
    }

    public class PriorityPrediction
    {
        [ColumnName("PredictedLabel")] public string PredictedPriority { get; set; } = string.Empty;
    }

        public class MlTicketClassifier
    {
        private readonly MLContext _mlContext;
        private readonly string _categoryModelPath = "TicketCategoryModel.zip";
        private readonly string _priorityModelPath = "TicketPriorityModel.zip";

        public MlTicketClassifier()
        {
                        _mlContext = new MLContext(seed: 0);
        }

                                public void TrainModels(AppDbContext dbContext)
        {
            var tickets = dbContext.Tickets.AsNoTracking().ToList();

                        if (tickets.Count < 3) return;

                        var trainingData = tickets.Select(t => new TicketInput
            {
                Title = t.Title,
                Description = t.Description ?? "",
                Category = t.Category.ToString(),
                Priority = t.Priority.ToString()
            }).ToList();

            var dataView = _mlContext.Data.LoadFromEnumerable(trainingData);

                                                            var categoryPipeline = _mlContext.Transforms.Text.FeaturizeText("TitleFeaturized", nameof(TicketInput.Title))
                .Append(_mlContext.Transforms.Text.FeaturizeText("DescFeaturized", nameof(TicketInput.Description)))
                .Append(_mlContext.Transforms.Concatenate("Features", "TitleFeaturized", "DescFeaturized"))
                .Append(_mlContext.Transforms.Conversion.MapValueToKey("Label", nameof(TicketInput.Category)))
                .Append(_mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy("Label", "Features"))
                .Append(_mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

            var categoryModel = categoryPipeline.Fit(dataView);
            _mlContext.Model.Save(categoryModel, dataView.Schema, _categoryModelPath);

                        var priorityPipeline = _mlContext.Transforms.Text.FeaturizeText("TitleFeaturized", nameof(TicketInput.Title))
                .Append(_mlContext.Transforms.Text.FeaturizeText("DescFeaturized", nameof(TicketInput.Description)))
                .Append(_mlContext.Transforms.Concatenate("Features", "TitleFeaturized", "DescFeaturized"))
                .Append(_mlContext.Transforms.Conversion.MapValueToKey("Label", nameof(TicketInput.Priority)))
                .Append(_mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy("Label", "Features"))
                .Append(_mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

            var priorityModel = priorityPipeline.Fit(dataView);
            _mlContext.Model.Save(priorityModel, dataView.Schema, _priorityModelPath);
        }

                                public (TicketCategory Category, TicketPriority Priority) Predict(string title, string description)
        {
                        if (!File.Exists(_categoryModelPath) || !File.Exists(_priorityModelPath))
            {
                return (TicketCategory.Software, TicketPriority.Normal);
            }

            var input = new TicketInput { Title = title, Description = description };

                        ITransformer categoryModel = _mlContext.Model.Load(_categoryModelPath, out var _);
            var categoryEngine = _mlContext.Model.CreatePredictionEngine<TicketInput, CategoryPrediction>(categoryModel);
            var catPrediction = categoryEngine.Predict(input);

                        ITransformer priorityModel = _mlContext.Model.Load(_priorityModelPath, out var _);
            var priorityEngine = _mlContext.Model.CreatePredictionEngine<TicketInput, PriorityPrediction>(priorityModel);
            var prioPrediction = priorityEngine.Predict(input);

                        Enum.TryParse<TicketCategory>(catPrediction.PredictedCategory, out var category);
            Enum.TryParse<TicketPriority>(prioPrediction.PredictedPriority, out var priority);

            return (category, priority);
        }
    }
}