using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.ML;
using Microsoft.ML.Data;
using КР_Ханников.Core;
using КР_Ханников.Data;

namespace КР_Ханников.Core
{
    public static class TicketModelTrainer
    {
        private const string CategoryModelFileName = "ticket_category_model.zip";
        private const string PriorityModelFileName = "ticket_priority_model.zip";

        public static void TrainAndSaveModels(AppDbContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            var modelsDir = Path.Combine(AppContext.BaseDirectory, "MLModels");
            Directory.CreateDirectory(modelsDir);

            // 1. Выгрузка данных
            var rawTickets = context.Tickets
                .AsNoTracking()
                .Where(t => !string.IsNullOrWhiteSpace(t.Title) && !string.IsNullOrWhiteSpace(t.Description))
                .ToList();

            // Нужно хотя бы немного данных для обучения. 
            // Если данных мало, ML не будет точным, но технически будет работать.
            if (rawTickets.Count < 5)
            {
                Debug.WriteLine("[Trainer] Слишком мало данных для обучения.");
                return;
            }

            var data = rawTickets.Select(t => new TicketTextData
            {
                Text = $"{t.Title} {t.Description}",
                CategoryLabel = t.Category.ToString(),
                PriorityLabel = t.Priority.ToString()
            }).ToList();

            var ml = new MLContext(seed: 1);
            var dataView = ml.Data.LoadFromEnumerable(data);

            // 2. Пайплайн обучения (Text -> Featurize -> MapToKey -> SDCA -> MapKeyToValue)
            var pipelineCat = ml.Transforms.Text.FeaturizeText("Features", nameof(TicketTextData.Text))
                .Append(ml.Transforms.Conversion.MapValueToKey("Label", nameof(TicketTextData.CategoryLabel)))
                .Append(ml.MulticlassClassification.Trainers.SdcaMaximumEntropy("Label", "Features"))
                .Append(ml.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

            var pipelinePrio = ml.Transforms.Text.FeaturizeText("Features", nameof(TicketTextData.Text))
                .Append(ml.Transforms.Conversion.MapValueToKey("Label", nameof(TicketTextData.PriorityLabel)))
                .Append(ml.MulticlassClassification.Trainers.SdcaMaximumEntropy("Label", "Features"))
                .Append(ml.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

            // 3. Обучение и сохранение
            Debug.WriteLine("Обучение модели категорий...");
            var modelCat = pipelineCat.Fit(dataView);
            ml.Model.Save(modelCat, dataView.Schema, Path.Combine(modelsDir, CategoryModelFileName));

            Debug.WriteLine("Обучение модели приоритетов...");
            var modelPrio = pipelinePrio.Fit(dataView);
            ml.Model.Save(modelPrio, dataView.Schema, Path.Combine(modelsDir, PriorityModelFileName));
        }

        private sealed class TicketTextData
        {
            public string Text { get; set; } = string.Empty;
            public string CategoryLabel { get; set; } = string.Empty;
            public string PriorityLabel { get; set; } = string.Empty;
        }
    }
}