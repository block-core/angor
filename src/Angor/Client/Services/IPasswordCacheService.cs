using System;

namespace Angor.Client.Services
{
    public interface IPasswordCacheService
    {
        string? Data { get; set; }
        void TryClear();
        void SetTimer(TimeSpan timeSpan);
    }

    public class PasswordCacheService : IPasswordCacheService
    {
        private Timer timer;

        private TimeSpan lastSet;
        public string? Data { get; set; }

        private TimeSpan oneMin = TimeSpan.FromMinutes(1);

        public PasswordCacheService()
        {
            this.timer = new Timer(Callback, null, oneMin, oneMin);
        }

        private void Callback(object? state)
        {
            Data = null;
        }
        
        public void SetTimer(TimeSpan timeSpan)
        {
            lastSet = timeSpan;
            this.timer.Change(timeSpan, timeSpan);
        }

        public void TryClear()
        {
            // if the password is default of 1 min we clear it
            if (lastSet.Ticks == oneMin.Ticks)
            {
                Data = null;
            }
        }
    }
}
