using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;

namespace КР_Ханников.Core
{
    /// <summary>
    /// История изменений тикета (создание, смена статуса, дедлайна и т.д.).
    /// Реализует INotifyPropertyChanged, чтобы при биндинге в WPF обновления
    /// подтягивались автоматически. Для EF Core это не мешает.
    /// </summary>
    public class TicketHistory : INotifyPropertyChanged
    {
        private int _id;
        private int _ticketId;
        private string _action = string.Empty;
        private string _details = string.Empty;
        private DateTime _timestamp;

        public event PropertyChangedEventHandler? PropertyChanged;

        [Key]
        public int Id
        {
            get => _id;
            set
            {
                if (_id == value) return;
                _id = value;
                OnPropertyChanged();
            }
        }

        public int TicketId
        {
            get => _ticketId;
            set
            {
                if (_ticketId == value) return;
                _ticketId = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Краткий тип события: "Статус", "Назначение", "Комментарий" и т.п.
        /// </summary>
        public string Action
        {
            get => _action;
            set
            {
                if (_action == value) return;
                _action = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Детальное описание, что произошло.
        /// </summary>
        public string Details
        {
            get => _details;
            set
            {
                if (_details == value) return;
                _details = value;
                OnPropertyChanged();
            }
        }

        public DateTime Timestamp
        {
            get => _timestamp;
            set
            {
                if (_timestamp == value) return;
                _timestamp = value;
                OnPropertyChanged();
            }
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = "")
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}