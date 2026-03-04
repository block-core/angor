namespace AngorApp.Model.ProjectsV2
{
    public static class FundingStateObservables
    {
        public static IObservable<bool> IsFundingNotInvestedYet(IObservable<bool> canInvest, IObservable<IAmountUI> fundingRaised)
        {
            return canInvest.CombineLatest(fundingRaised, (investable, raisedAmount) => investable && raisedAmount.Sats == 0);
        }

        public static IObservable<bool> IsFundingOpen(IObservable<bool> canInvest, IObservable<IAmountUI> fundingRaised)
        {
            return canInvest.CombineLatest(fundingRaised, (investable, raisedAmount) => investable && raisedAmount.Sats > 0);
        }

        public static IObservable<bool> CanInvest(IObservable<IAmountUI> fundingRaised, IAmountUI fundingTarget, DateTimeOffset fundingStart, DateTimeOffset fundingEnd)
        {
            return fundingRaised.Select(raisedAmount =>
            {
                var now = DateTimeOffset.UtcNow;
                var withinFundingWindow = now >= fundingStart && now <= fundingEnd;
                var hasReachedTarget = raisedAmount.Sats >= fundingTarget.Sats;
                return withinFundingWindow && !hasReachedTarget;
            });
        }

        public static IObservable<bool> IsFundingSuccessful(IObservable<IAmountUI> fundingRaised, IAmountUI fundingTarget)
        {
            return fundingRaised.Select(raisedAmount => raisedAmount.Sats >= fundingTarget.Sats);
        }

        public static IObservable<bool> IsFundingFailed(IObservable<IAmountUI> fundingRaised, IAmountUI fundingTarget, DateTimeOffset fundingEnd)
        {
            return fundingRaised.Select(raisedAmount =>
            {
                var fundingClosed = DateTimeOffset.UtcNow > fundingEnd;
                var hasReachedTarget = raisedAmount.Sats >= fundingTarget.Sats;
                return fundingClosed && !hasReachedTarget;
            });
        }

    }
}
