using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Angor.Client.Models;
using Angor.Client.Storage;

namespace Angor.Client.Services
{
    public class NotificationService : INotificationService
    {
        private const string NOTIFICATIONS_STORAGE_KEY = "notifications";
        private readonly IClientStorage _storage;
        private readonly List<NotificationItem> _notifications = new();
        private event Action _onNotificationsChanged;

        public NotificationService(IClientStorage storage)
        {
            _storage = storage;
            LoadNotifications();
        }

        public async Task AddNotificationAsync(string title, string message, string type, string projectId = null, Dictionary<string, string> additionalData = null)
        {
            var notification = new NotificationItem
            {
                Id = Guid.NewGuid().ToString(),
                Title = title,
                Message = message,
                Timestamp = DateTime.UtcNow,
                IsRead = false,
                Type = type,
                ProjectId = projectId,
                AdditionalData = additionalData ?? new Dictionary<string, string>()
            };

            _notifications.Add(notification);
            SaveNotifications();
            NotifyStateChanged();
        }

        public async Task AddInvestmentNotificationAsync(string title, string message, string projectId, Dictionary<string, string> additionalData = null)
        {
            await AddNotificationAsync(title, message, "investments", projectId, additionalData);
        }

        public async Task AddProjectNotificationAsync(string title, string message, string projectId, Dictionary<string, string> additionalData = null)
        {
            await AddNotificationAsync(title, message, "projects", projectId, additionalData);
        }

        public async Task AddMessageNotificationAsync(string title, string message, string projectId, Dictionary<string, string> additionalData = null)
        {
            await AddNotificationAsync(title, message, "messages", projectId, additionalData);
        }

        public async Task AddSystemNotificationAsync(string title, string message, Dictionary<string, string> additionalData = null)
        {
            await AddNotificationAsync(title, message, "system", null, additionalData);
        }

        public IEnumerable<NotificationItem> GetAllNotifications()
        {
            return _notifications.OrderByDescending(n => n.Timestamp).ToList();
        }

        public int GetUnreadNotificationsCount()
        {
            return _notifications.Count(n => !n.IsRead);
        }

        public int GetNotificationCountByType(string type)
        {
            return _notifications.Count(n => n.Type == type && !n.IsRead);
        }

        public async Task MarkNotificationAsReadAsync(string notificationId)
        {
            var notification = _notifications.FirstOrDefault(n => n.Id == notificationId);
            if (notification != null)
            {
                notification.IsRead = true;
                SaveNotifications();
                NotifyStateChanged();
            }
        }

        public async Task MarkAllNotificationsAsReadAsync()
        {
            foreach (var notification in _notifications.Where(n => !n.IsRead))
            {
                notification.IsRead = true;
            }
            SaveNotifications();
            NotifyStateChanged();
        }

        public async Task RemoveNotificationAsync(string notificationId)
        {
            var notification = _notifications.FirstOrDefault(n => n.Id == notificationId);
            if (notification != null)
            {
                _notifications.Remove(notification);
                SaveNotifications();
                NotifyStateChanged();
            }
        }

        public async Task ClearNotificationsAsync()
        {
            _notifications.Clear();
            SaveNotifications();
            NotifyStateChanged();
        }

        private void LoadNotifications()
        {
            try
            {
                var storedNotifications = _storage.GetNotifications();
                if (storedNotifications != null)
                {
                    _notifications.Clear();
                    _notifications.AddRange(storedNotifications);
                    NotifyStateChanged();
                }
            }
            catch (Exception)
            {
                // Ignore errors and start with an empty list
            }
        }

        private void SaveNotifications()
        {
            _storage.SetNotifications(_notifications);
        }

        public event Action OnNotificationsChanged
        {
            add => _onNotificationsChanged += value;
            remove => _onNotificationsChanged -= value;
        }

        private void NotifyStateChanged() => _onNotificationsChanged?.Invoke();
    }
}
