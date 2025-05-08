using System;
using System.Collections.Generic;

namespace Angor.Client.Models
{
    public class NotificationItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public bool IsRead { get; set; } = false;
        public string Type { get; set; } // investments, projects, messages, system
        public string ProjectId { get; set; }
        public string RelatedId { get; set; }
        public Dictionary<string, string> AdditionalData { get; set; } = new Dictionary<string, string>();
    }
}
