using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace TodoListApp.Models
{
    public class TodoItem : INotifyPropertyChanged
    {
        private Guid _id = Guid.NewGuid();
        private string _title = string.Empty;
        private bool _isCompleted;
        private DateTime _createdAt = DateTime.Now;
        private ObservableCollection<SubTaskItem> _subTasks = new();
        private bool _isExpanded;
        private bool _isDragging;

        public TodoItem()
        {
            _subTasks.CollectionChanged += SubTasks_CollectionChanged;
        }

        public Guid Id
        {
            get => _id;
            set
            {
                if (_id != value)
                {
                    _id = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Title
        {
            get => _title;
            set
            {
                if (_title != value)
                {
                    _title = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsCompleted
        {
            get => _isCompleted;
            set
            {
                if (_isCompleted != value)
                {
                    _isCompleted = value;
                    OnPropertyChanged();
                }
            }
        }

        public DateTime CreatedAt
        {
            get => _createdAt;
            set
            {
                if (_createdAt != value)
                {
                    _createdAt = value;
                    OnPropertyChanged();
                }
            }
        }

        public ObservableCollection<SubTaskItem> SubTasks
        {
            get => _subTasks;
            set
            {
                if (_subTasks != value)
                {
                    if (_subTasks != null)
                    {
                        _subTasks.CollectionChanged -= SubTasks_CollectionChanged;
                        foreach (var item in _subTasks)
                        {
                            item.PropertyChanged -= SubTask_PropertyChanged;
                        }
                    }

                    _subTasks = value ?? new ObservableCollection<SubTaskItem>();
                    _subTasks.CollectionChanged += SubTasks_CollectionChanged;
                    foreach (var item in _subTasks)
                    {
                        item.PropertyChanged += SubTask_PropertyChanged;
                    }

                    OnPropertyChanged();
                    UpdateSubtasksSummary();
                }
            }
        }

        [JsonIgnore]
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ExpandIcon));
                }
            }
        }

        [JsonIgnore]
        public string SubtasksSummary => $"Subtarefas {SubTasks.Count(s => s.IsCompleted)}/{SubTasks.Count}";

        [JsonIgnore]
        public string ExpandIcon => IsExpanded ? "▲" : "▼";

        [JsonIgnore]
        public bool AllSubtasksCompleted => SubTasks.Count > 0 && SubTasks.All(s => s.IsCompleted);

        [JsonIgnore]
        public bool IsDragging
        {
            get => _isDragging;
            set
            {
                if (_isDragging != value)
                {
                    _isDragging = value;
                    OnPropertyChanged();
                }
            }
        }

        private void SubTasks_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (SubTaskItem item in e.NewItems)
                {
                    item.PropertyChanged += SubTask_PropertyChanged;
                }
            }

            if (e.OldItems != null)
            {
                foreach (SubTaskItem item in e.OldItems)
                {
                    item.PropertyChanged -= SubTask_PropertyChanged;
                }
            }

            UpdateSubtasksSummary();
        }

        private void SubTask_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SubTaskItem.IsCompleted))
            {
                UpdateSubtasksSummary();
            }
        }

        public void UpdateSubtasksSummary()
        {
            OnPropertyChanged(nameof(SubtasksSummary));
            OnPropertyChanged(nameof(AllSubtasksCompleted));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
