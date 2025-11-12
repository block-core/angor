using Angor.Shared.Models;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.NBitcoin;

namespace Angor.Shared.Protocol.Scripts;

public class InvestmentScriptBuilder : IInvestmentScriptBuilder
{
    private readonly ISeederScriptTreeBuilder _seederScriptTreeBuilder;

    public InvestmentScriptBuilder(ISeederScriptTreeBuilder seederScriptTreeBuilder)
    {
        _seederScriptTreeBuilder = seederScriptTreeBuilder;
    }

    public Script GetInvestorPenaltyTransactionScript(string investorKey, int punishmentLockDays)
    {
        if (punishmentLockDays > 388)
        {
            // the actual number is 65535*512 seconds (388 days) 
            // https://en.bitcoin.it/wiki/Timelock
            throw new ArgumentOutOfRangeException(nameof(punishmentLockDays), $"Invalid CSV value {punishmentLockDays}");
        }

        var sequence = new Sequence(TimeSpan.FromDays(punishmentLockDays));

        return new(new List<Op>
        {
            Op.GetPushOp(new NBitcoin.PubKey(investorKey).ToBytes()),
            OpcodeType.OP_CHECKSIGVERIFY,
            Op.GetPushOp((uint)sequence),
            OpcodeType.OP_CHECKSEQUENCEVERIFY
        });
    }

    public ProjectScripts BuildProjectScriptsForStage(ProjectInfo projectInfo, string investorKey, int stageIndex,
      uint256? hashOfSecret, DateTime? expiryDateOverride = null)
    {
        return BuildProjectScriptsForStage(projectInfo, investorKey, stageIndex, hashOfSecret, expiryDateOverride, null);
    }

    /// <summary>
    /// Builds project scripts for a specific stage, supporting both Invest (fixed) and Fund/Subscribe (dynamic) projects.
    /// </summary>
    /// <param name="projectInfo">Project information</param>
    /// <param name="investorKey">Investor's public key</param>
    /// <param name="stageIndex">Index of the stage (0-based)</param>
    /// <param name="hashOfSecret">Optional secret hash for seeders</param>
    /// <param name="expiryDateOverride">Optional override for expiry date</param>
    /// <param name="investmentStartDate">Required for Fund/Subscribe projects - the date when investment was made</param>
    public ProjectScripts BuildProjectScriptsForStage(ProjectInfo projectInfo, string investorKey, int stageIndex,
        uint256? hashOfSecret, DateTime? expiryDateOverride, DateTime? investmentStartDate)
    {
        // Calculate stage release date based on project type
        DateTime stageReleaseDate;

        if (projectInfo.ProjectType == ProjectType.Invest)
        {
            // Fixed stages - use predefined dates
            if (stageIndex >= projectInfo.Stages.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(stageIndex), $"Stage index {stageIndex} is out of range. Project has {projectInfo.Stages.Count} stages.");
            }

            stageReleaseDate = projectInfo.Stages[stageIndex].ReleaseDate;
        }
        else // Fund or Subscribe
        {
            // Dynamic stages - calculate from investment start date and pattern
            if (!investmentStartDate.HasValue)
            {
                throw new ArgumentException("investmentStartDate is required for Fund/Subscribe projects", nameof(investmentStartDate));
            }

            if (!projectInfo.DynamicStagePatterns.Any())
            {
                throw new InvalidOperationException("Fund/Subscribe projects must have at least one DynamicStagePattern");
            }

            // Use the first pattern (or allow pattern selection in future)
            var pattern = projectInfo.DynamicStagePatterns[0];

            // Validate stage index
            if (stageIndex >= pattern.StageCount)
            {
                throw new ArgumentOutOfRangeException(nameof(stageIndex), $"Stage index {stageIndex} is out of range. Pattern has {pattern.StageCount} stages.");
            }

            // Calculate the stage release date
            stageReleaseDate = CalculateDynamicStageReleaseDate(
                investmentStartDate.Value, pattern, stageIndex);
        }

        // regular investor pre-co-sign with founder to gets funds with penalty
        var recoveryOps = new List<Op>
        {
            Op.GetPushOp(new NBitcoin.PubKey(projectInfo.FounderRecoveryKey).GetTaprootFullPubKey().ToBytes()),
            OpcodeType.OP_CHECKSIGVERIFY,
            Op.GetPushOp(new NBitcoin.PubKey(investorKey).GetTaprootFullPubKey().ToBytes()),
        };

        var secretHashOps = hashOfSecret == null
         ? new List<Op> { OpcodeType.OP_CHECKSIG }
            : new List<Op>
                  {
                    OpcodeType.OP_CHECKSIGVERIFY,
                    OpcodeType.OP_HASH256,
                    Op.GetPushOp(new uint256(hashOfSecret).ToBytes()),
                    OpcodeType.OP_EQUAL
                  };

        recoveryOps.AddRange(secretHashOps);

        var seeders = hashOfSecret == null && projectInfo.ProjectSeeders.SecretHashes.Any()
        ? _seederScriptTreeBuilder.BuildSeederScriptTree(investorKey,
                   projectInfo.ProjectSeeders.Threshold,
         projectInfo.ProjectSeeders.SecretHashes).ToList()
       : new List<Script>();

        // Use the override expiry date if provided, otherwise use the project's expiry date
        var effectiveExpiryDate = expiryDateOverride ?? projectInfo.ExpiryDate;

        return new()
        {
            Founder = GetFounderSpendScript(projectInfo.FounderKey, stageReleaseDate),
            Recover = new Script(recoveryOps),
            EndOfProject = GetEndOfProjectInvestorSpendScript(investorKey, effectiveExpiryDate),
            Seeders = seeders
        };
    }

    /// <summary>
    /// Calculates the release date for a dynamic stage based on the pattern and investment start date.
    /// </summary>
    private static DateTime CalculateDynamicStageReleaseDate(DateTime investmentStartDate, DynamicStagePattern pattern, int stageIndex)
    {
        if (pattern.PayoutDayType == PayoutDayType.FromStartDate)
        {
            // Simple: add fixed intervals from start date
            var duration = DynamicStageHelper.GetFrequencyDuration(pattern.Frequency);
            return investmentStartDate.Add(duration * (stageIndex + 1)); // +1 because first stage is after first interval
        }
        else if (pattern.PayoutDayType == PayoutDayType.SpecificDayOfMonth)
        {
            // Payout on specific day of month (e.g., 1st, 15th)
            return CalculateNextMonthlyPayoutDate(investmentStartDate, pattern.Frequency, pattern.PayoutDay, stageIndex);
        }
        else // SpecificDayOfWeek
        {
            // Payout on specific day of week (e.g., Monday)
            return CalculateNextWeeklyPayoutDate(investmentStartDate, pattern.Frequency, pattern.PayoutDay, stageIndex);
        }
    }

    private static DateTime CalculateNextMonthlyPayoutDate(DateTime startDate, StageFrequency frequency, int dayOfMonth, int stageIndex)
    {
        // Determine how many months to add based on frequency
        int monthsToAdd = frequency switch
        {
            StageFrequency.Monthly => stageIndex,
            StageFrequency.BiMonthly => stageIndex * 2,
            StageFrequency.Quarterly => stageIndex * 3,
            _ => throw new ArgumentException($"Frequency {frequency} does not support SpecificDayOfMonth")
        };

        // Start from the first occurrence of the target day
        var targetDate = new DateTime(startDate.Year, startDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        // Adjust to the target day of month (handle months with fewer days)
        var daysInMonth = DateTime.DaysInMonth(targetDate.Year, targetDate.Month);
        var actualDay = Math.Min(dayOfMonth, daysInMonth);
        targetDate = new DateTime(targetDate.Year, targetDate.Month, actualDay, 0, 0, 0, DateTimeKind.Utc);

        // If the target date is before the start date, move to next month
        if (targetDate < startDate)
        {
            targetDate = targetDate.AddMonths(1);
            daysInMonth = DateTime.DaysInMonth(targetDate.Year, targetDate.Month);
            actualDay = Math.Min(dayOfMonth, daysInMonth);
            targetDate = new DateTime(targetDate.Year, targetDate.Month, actualDay, 0, 0, 0, DateTimeKind.Utc);
        }

        // Add the appropriate number of months for this stage
        targetDate = targetDate.AddMonths(monthsToAdd);

        // Adjust for months with fewer days (e.g., Feb 31 -> Feb 28/29)
        daysInMonth = DateTime.DaysInMonth(targetDate.Year, targetDate.Month);
        actualDay = Math.Min(dayOfMonth, daysInMonth);

        return new DateTime(targetDate.Year, targetDate.Month, actualDay, 0, 0, 0, DateTimeKind.Utc);
    }

    private static DateTime CalculateNextWeeklyPayoutDate(DateTime startDate, StageFrequency frequency, int dayOfWeek, int stageIndex)
    {
        if (dayOfWeek < 0 || dayOfWeek > 6)
            throw new ArgumentOutOfRangeException(nameof(dayOfWeek), "DayOfWeek must be 0-6 (Sunday-Saturday)");

        // Determine how many weeks to add based on frequency
        int weeksToAdd = frequency switch
        {
            StageFrequency.Weekly => stageIndex,
            StageFrequency.Biweekly => stageIndex * 2,
            _ => throw new ArgumentException($"Frequency {frequency} does not support SpecificDayOfWeek")
        };

        // Find the first occurrence of the target day of week on or after start date
        var targetDayOfWeek = (DayOfWeek)dayOfWeek;
        var currentDate = startDate.Date;

        while (currentDate.DayOfWeek != targetDayOfWeek)
        {
            currentDate = currentDate.AddDays(1);
        }

        // Add the appropriate number of weeks for this stage
        return currentDate.AddDays(weeksToAdd * 7);
    }

    private static Script GetFounderSpendScript(string founderKey, DateTime stageReleaseDate)
    {
        long locktimeFounder = Utils.DateTimeToUnixTime(stageReleaseDate);

        // founder gets funds after stage started
        return new Script(new List<Op>
        {
            Op.GetPushOp(new NBitcoin.PubKey(founderKey).GetTaprootFullPubKey().ToBytes()),
            OpcodeType.OP_CHECKSIGVERIFY,
            Op.GetPushOp(locktimeFounder),
            OpcodeType.OP_CHECKLOCKTIMEVERIFY
      });
    }

    private static Script GetEndOfProjectInvestorSpendScript(string investorKey, DateTime projectExpieryDate)
    {
        long locktimeExpiery = Utils.DateTimeToUnixTime(projectExpieryDate);

        // project ended and investor can collect remaining funds
        return new Script(new List<Op>
     {
            Op.GetPushOp(new NBitcoin.PubKey(investorKey).GetTaprootFullPubKey().ToBytes()),
            OpcodeType.OP_CHECKSIGVERIFY,
            Op.GetPushOp(locktimeExpiery),
            OpcodeType.OP_CHECKLOCKTIMEVERIFY
     });
    }
}