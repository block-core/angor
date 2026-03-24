using System;

namespace Angor.Client.Services
{
    public interface IPasswordCacheService
    {
        string? Data { get; set; }
        void TryClear();
        void SetTimer(TimeSpan timeSpan);
        bool UnlockWhileActive { get; set; }
        void ResetLastSet();
    }

    public class PasswordCacheService : IPasswordCacheService
    {
        private Timer timer;

        private TimeSpan lastSet;

        private TimeSpan unlockDuration;

        public string? Data { get; set; }

        private TimeSpan oneMin = TimeSpan.FromMinutes(1);

        public bool UnlockWhileActive { get; set; }

        public PasswordCacheService()
        {
            this.timer = new Timer(Callback, null, oneMin, oneMin);
        }

        private void Callback(object? state)
        {
            if (DateTime.UtcNow.TimeOfDay - lastSet > unlockDuration)
            {
                Data = null;
            }
        }
        
        public void SetTimer(TimeSpan timeSpan)
        {
            unlockDuration = timeSpan;
            lastSet = DateTime.UtcNow.TimeOfDay;
        }

        public void TryClear()
        {
            // if the password is default of 1 min we clear it
            if (!UnlockWhileActive && unlockDuration.Ticks == oneMin.Ticks)
            {
                Data = null;
            }
        }

        public void ResetLastSet()
        {
            if (UnlockWhileActive)
            {
                lastSet = DateTime.UtcNow.TimeOfDay;
            }
        }
    }
}
