using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Angor.Client.Models;

namespace Angor.Client.Services
{
    public interface INotificationService
    {
        Task AddNotificationAsync(string title, string message, string type, string projectId = null, Dictionary<string, string> additionalData = null);
        Task AddInvestmentNotificationAsync(string title, string message, string projectId, Dictionary<string, string> additionalData = null);
        Task AddProjectNotificationAsync(string title, string message, string projectId, Dictionary<string, string> additionalData = null);
        Task AddMessageNotificationAsync(string title, string message, string projectId, Dictionary<string, string> additionalData = null);
        Task AddSystemNotificationAsync(string title, string message, Dictionary<string, string> additionalData = null);
        
        IEnumerable<NotificationItem> GetAllNotifications();
        int GetUnreadNotificationsCount();
        int GetNotificationCountByType(string type);
        
        Task MarkNotificationAsReadAsync(string notificationId);
        Task MarkAllNotificationsAsReadAsync();
        Task RemoveNotificationAsync(string notificationId);
        Task ClearNotificationsAsync();
        
        event Action OnNotificationsChanged;
    }
}
