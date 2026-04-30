using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.JSInterop;

namespace mes_opc_ui.Services
{
    /// <summary>
    /// Types d'événements pour l'activité récente.
    /// </summary>
    public enum ActivityEventType
    {
        Add,     // Ajout
        Modify,  // Modification
        Delete,  // Suppression
        Error,   // Erreur
        Success  // Succès
    }

    /// <summary>
    /// Entrée dans le journal d'activité système.
    /// </summary>
    public class ActivityLogEntry
    {
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Category { get; set; } = "";
        public DateTime Timestamp { get; set; }
        public string ColorCategory { get; set; } = "blue"; // blue, yellow, red, green
        public ActivityEventType EventType { get; set; } = ActivityEventType.Add;
    }

    /// <summary>
    /// Service singleton gérant le journal d'activité en mémoire.
    /// Les événements sont insérés par les services AdminApi lors des opérations CRUD.
    /// </summary>
    public class ActivityLogService
    {
        private readonly List<ActivityLogEntry> _logs = new();
        private const int MaxEntries = 100;
        private readonly Microsoft.JSInterop.IJSRuntime _js;
        private const string StorageKey = "mes_recent_activity";

        public event Action? OnActivityLogged;

        public ActivityLogService(Microsoft.JSInterop.IJSRuntime js)
        {
            _js = js;
        }

        /// <summary>
        /// Réhydrate le service depuis une source externe (ex: localStorage).
        /// N'émet pas d'événement OnActivityLogged.
        /// </summary>
        public void LoadEntries(IEnumerable<ActivityLogEntry> entries)
        {
            _logs.Clear();
            foreach (var e in entries.Take(MaxEntries))
                _logs.Add(e);
        }

        /// <summary>
        /// Journalise une action utilisateur/système.
        /// </summary>
        public void Log(
            string title,
            string category,
            string colorCategory = "blue",
            ActivityEventType eventType = ActivityEventType.Add,
            string description = "")
        {
            var entry = new ActivityLogEntry
            {
                Title = title,
                Description = description,
                Category = category,
                Timestamp = DateTime.Now,
                ColorCategory = colorCategory,
                EventType = eventType
            };

            _logs.Insert(0, entry);

            if (_logs.Count > MaxEntries)
                _logs.RemoveAt(_logs.Count - 1);

            // Persist to localStorage
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(_logs.Take(15).ToList());
                _ = _js.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
            }
            catch { }

            OnActivityLogged?.Invoke();
        }

        // Shortcuts pour les types courants
        public void LogAdd(string title, string category, string description = "")
            => Log(title, category, "blue", ActivityEventType.Add, description);

        public void LogModify(string title, string category, string description = "")
            => Log(title, category, "yellow", ActivityEventType.Modify, description);

        public void LogDelete(string title, string category, string description = "")
            => Log(title, category, "red", ActivityEventType.Delete, description);

        public void LogError(string title, string category, string description = "")
            => Log(title, category, "red", ActivityEventType.Error, description);

        public void LogSuccess(string title, string category, string description = "")
            => Log(title, category, "green", ActivityEventType.Success, description);

        /// <summary>
        /// Retourne les N dernières activités, avec filtrage optionnel.
        /// </summary>
        public IReadOnlyList<ActivityLogEntry> GetRecentActivities(int count = 10, string? category = null, ActivityEventType? eventType = null)
        {
            var query = _logs.AsQueryable();

            if (!string.IsNullOrEmpty(category) && category != "all")
                query = query.Where(l => l.Category.Equals(category, StringComparison.OrdinalIgnoreCase));

            if (eventType.HasValue)
                query = query.Where(l => l.EventType == eventType.Value);

            return query.Take(count).ToList();
        }

        /// <summary>
        /// Retourne toutes les activités avec filtrage et pagination.
        /// </summary>
        public (IReadOnlyList<ActivityLogEntry> Items, int Total) GetPaged(
            int page = 1,
            int pageSize = 7,
            string? category = null,
            ActivityEventType? eventType = null)
        {
            var query = _logs.AsQueryable();

            if (!string.IsNullOrEmpty(category) && category != "all")
                query = query.Where(l => l.Category.Equals(category, StringComparison.OrdinalIgnoreCase));

            if (eventType.HasValue)
                query = query.Where(l => l.EventType == eventType.Value);

            var total = query.Count();
            var items = query.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return (items, total);
        }

        public int TotalCount => _logs.Count;
    }
}
