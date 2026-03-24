using System;

namespace Angor.Client.Models
{
    public class DirectMessage
    {
        public string Id { get; set; }
        public string Content { get; set; }
        public string SenderPubkey { get; set; }
        public string RecipientPubkey { get; set; }
        public DateTime Timestamp { get; set; }
        public bool IsFromCurrentUser { get; set; }
    }
}
