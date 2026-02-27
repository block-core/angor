using Angor.Shared.Models;
using Angor.Shared.Utilities;
using Blockcore.Consensus.ScriptInfo;
using Blockcore.Consensus.TransactionInfo;
using Blockcore.NBitcoin;

namespace Angor.Shared.Models;

public class FundingParameters
{
    public string InvestorKey { get; set; }
    public long TotalInvestmentAmount { get; set; }
    public DateTime? InvestmentStartDate { get; set; }
    public byte PatternId { get; set; }
    public int? StageCountOverride { get; set; }
    public Blockcore.NBitcoin.uint256? HashOfSecret { get; set; }
    public DateTime? ExpiryDateOverride { get; set; }

    public FundingParameters() { }

    /// <summary>
    /// Finds a DynamicStagePattern by PatternId from the project's patterns list.
    /// </summary>
    public DynamicStagePattern FindPattern(ProjectInfo projectInfo)
    {
        var pattern = projectInfo.DynamicStagePatterns?.FirstOrDefault(p => p.PatternId == PatternId);
        if (pattern == null)
            throw new InvalidOperationException($"No pattern found with PatternId {PatternId}. Available patterns: {string.Join(", ", projectInfo.DynamicStagePatterns?.Select(p => p.PatternId.ToString()) ?? [])}");
        return pattern;
    }

    public static FundingParameters CreateForInvest(
        ProjectInfo projectInfo,
        string investorKey,
        long totalInvestmentAmount,
        Blockcore.NBitcoin.uint256? HashOfSecret = null)
    {
        return new FundingParameters
        {
            InvestorKey = investorKey,
            TotalInvestmentAmount = totalInvestmentAmount,
            PatternId = 0,
            HashOfSecret = HashOfSecret,
            ExpiryDateOverride = PenaltyThresholdHelper.GetExpiryDateOverride(projectInfo, totalInvestmentAmount)
        };
    }

    public static FundingParameters CreateForFund(
        ProjectInfo projectInfo,
        string investorKey,
        long totalInvestmentAmount,
        byte patternId,
        DateTime investmentStartDate)
    {
        return new FundingParameters
        {
            InvestorKey = investorKey,
            TotalInvestmentAmount = totalInvestmentAmount,
            InvestmentStartDate = investmentStartDate,
            PatternId = patternId,
            ExpiryDateOverride = PenaltyThresholdHelper.GetExpiryDateOverride(projectInfo, totalInvestmentAmount)
        };
    }

    public static FundingParameters CreateForSubscribe(
           ProjectInfo projectInfo,
           string investorKey,
           long totalInvestmentAmount,
           byte patternId,
           DateTime investmentStartDate)
    {
        return new FundingParameters
        {
            InvestorKey = investorKey,
            TotalInvestmentAmount = totalInvestmentAmount,
            InvestmentStartDate = investmentStartDate,
            PatternId = patternId,
            ExpiryDateOverride = projectInfo.StartDate
        };
    }

    public static FundingParameters CreateFromTransaction(
       ProjectInfo projectInfo,
       Transaction investmentTransaction)
    {
        if (investmentTransaction == null)
            throw new ArgumentNullException(nameof(investmentTransaction));

        return CreateFromOpReturn(projectInfo,
            investmentTransaction.Outputs.First(_ => _.ScriptPubKey.IsUnspendable).ScriptPubKey,
            investmentTransaction.GetTotalInvestmentAmount());
    }

    public static FundingParameters CreateFromOpReturn(
        ProjectInfo projectInfo,
        Script opReturnScript,
        long totalInvestmentAmount)
    {
        if (opReturnScript == null)
            throw new ArgumentNullException(nameof(opReturnScript));

        if (projectInfo == null)
            throw new ArgumentNullException(nameof(projectInfo));

        if (!opReturnScript.IsUnspendable)
            throw new ArgumentException("Script is not an OP_RETURN script", nameof(opReturnScript));

        var ops = opReturnScript.ToOps();
        var investorKey = new PubKey(ops[1].PushData).ToHex();

        uint256? secretHash = null;
        DynamicStageInfo? dynamicInfo = null;

        if (ops.Count == 2)
        {
            // Invest project: investor key only
        }
        else if (ops.Count == 3)
        {
            if (ops[2].PushData?.Length == 32)
            {
                // Seeder with secret hash (Invest project)
                secretHash = new uint256(ops[2].PushData);
            }
            else if (ops[2].PushData?.Length == 4)
            {
                // Dynamic stage info (Fund/Subscribe project) - 4 bytes
                dynamicInfo = DynamicStageInfo.FromBytes(ops[2].PushData);
            }
        }
        else if (ops.Count == 4)
        {
            // Fund/Subscribe seeder: investor key + secret hash + dynamic info
            secretHash = new uint256(ops[2].PushData);
            dynamicInfo = DynamicStageInfo.FromBytes(ops[3].PushData);
        }

        DateTime? expiryDateOverride = null;

        if (projectInfo.ProjectType == ProjectType.Invest)
        {
            expiryDateOverride = PenaltyThresholdHelper.GetExpiryDateOverride(projectInfo, totalInvestmentAmount);
        }
        else if (projectInfo.ProjectType == ProjectType.Fund)
        {
            expiryDateOverride = PenaltyThresholdHelper.GetExpiryDateOverride(projectInfo, totalInvestmentAmount);
        }
        else if (projectInfo.ProjectType == ProjectType.Subscribe)
        {
            expiryDateOverride = projectInfo.StartDate;
        }

        return new FundingParameters
        {
            InvestorKey = investorKey,
            TotalInvestmentAmount = totalInvestmentAmount,
            InvestmentStartDate = dynamicInfo?.GetInvestmentStartDate(),
            PatternId = dynamicInfo?.PatternId ?? 0,
            StageCountOverride = dynamicInfo?.StageCount > 0 ? dynamicInfo.StageCount : (int?)null,
            HashOfSecret = secretHash,
            ExpiryDateOverride = expiryDateOverride
        };
    }

    public void Validate(ProjectInfo projectInfo, int? stageIndex = null)
    {
        if (projectInfo == null)
            throw new ArgumentNullException(nameof(projectInfo));

        if (string.IsNullOrWhiteSpace(InvestorKey))
            throw new ArgumentException("InvestorKey cannot be null or empty", nameof(InvestorKey));

        if (TotalInvestmentAmount < 0)
            throw new ArgumentException("TotalInvestmentAmount cannot be negative", nameof(TotalInvestmentAmount));

        if (stageIndex.HasValue && stageIndex.Value < 0)
            throw new ArgumentException("stageIndex cannot be negative", nameof(stageIndex));

        switch (projectInfo.ProjectType)
        {
            case ProjectType.Invest:
                ValidateInvestType(projectInfo, stageIndex);
                break;
            case ProjectType.Fund:
                ValidateFundType(projectInfo, stageIndex);
                break;
            case ProjectType.Subscribe:
                ValidateSubscribeType(projectInfo, stageIndex);
                break;
            default:
                throw new ArgumentException($"Unknown ProjectType: {projectInfo.ProjectType}");
        }
    }

    private void ValidateInvestType(ProjectInfo projectInfo, int? stageIndex)
    {
        if (projectInfo.Stages == null || !projectInfo.Stages.Any())
            throw new InvalidOperationException("Invest projects must have at least one stage");

        if (stageIndex.HasValue)
        {
            if (stageIndex.Value >= projectInfo.Stages.Count)
                throw new ArgumentOutOfRangeException(nameof(stageIndex), $"Stage index {stageIndex.Value} is out of range. Project has {projectInfo.Stages.Count} stages.");
        }
    }

    private void ValidateFundType(ProjectInfo projectInfo, int? stageIndex)
    {
        if (!InvestmentStartDate.HasValue)
            throw new ArgumentException("InvestmentStartDate is required for Fund/Subscribe projects", nameof(InvestmentStartDate));

        if (projectInfo.DynamicStagePatterns == null || !projectInfo.DynamicStagePatterns.Any())
            throw new InvalidOperationException("Fund/Subscribe projects must have at least one DynamicStagePattern");

        var pattern = FindPattern(projectInfo);

        // Validate pattern properties
        ValidatePattern(pattern);

        if (stageIndex.HasValue)
        {
            if (!StageCountOverride.HasValue || StageCountOverride.Value == 0)
            {
                if (stageIndex.Value >= pattern.StageCount)
                    throw new ArgumentOutOfRangeException(nameof(stageIndex), $"Stage index {stageIndex.Value} is out of range. Pattern has {pattern.StageCount} stages.");
            }
            else
            {
                if (stageIndex.Value >= StageCountOverride.Value)
                    throw new ArgumentOutOfRangeException(nameof(stageIndex), $"Stage index {stageIndex.Value} is out of range. Override stage count is {StageCountOverride.Value}.");
            }
        }
    }

    private void ValidateSubscribeType(ProjectInfo projectInfo, int? stageIndex)
    {
        if (!InvestmentStartDate.HasValue)
            throw new ArgumentException("InvestmentStartDate is required for Fund/Subscribe projects", nameof(InvestmentStartDate));

        if (projectInfo.DynamicStagePatterns == null || !projectInfo.DynamicStagePatterns.Any())
            throw new InvalidOperationException("Fund/Subscribe projects must have at least one DynamicStagePattern");

        var pattern = FindPattern(projectInfo);

        // Validate pattern properties
        ValidatePattern(pattern);

        // Subscribe patterns must have a fixed amount
        if (!pattern.HasFixedAmount)
            throw new InvalidOperationException("Subscribe patterns must have a fixed Amount specified.");

        // Validate that TotalInvestmentAmount matches the pattern's fixed Amount
        if (TotalInvestmentAmount != pattern.Amount!.Value)
            throw new InvalidOperationException($"Subscribe projects must use the pattern's fixed amount of {pattern.Amount.Value} satoshis. Received: {TotalInvestmentAmount} satoshis.");

        if (stageIndex.HasValue)
        {
            if (!StageCountOverride.HasValue || StageCountOverride.Value == 0)
            {
                if (stageIndex.Value >= pattern.StageCount)
                    throw new ArgumentOutOfRangeException(nameof(stageIndex), $"Stage index {stageIndex.Value} is out of range. Pattern has {pattern.StageCount} stages.");
            }
            else
            {
                if (stageIndex.Value >= StageCountOverride.Value)
                    throw new ArgumentOutOfRangeException(nameof(stageIndex), $"Stage index {stageIndex.Value} is out of range. Override stage count is {StageCountOverride.Value}.");
            }
        }
    }

    private static void ValidatePattern(DynamicStagePattern pattern)
    {
        if (pattern.StageCount <= 0)
            throw new InvalidOperationException("StageCount must be greater than 0.");

        // Validate PayoutDay based on PayoutDayType
        if (pattern.PayoutDayType == PayoutDayType.SpecificDayOfMonth)
        {
            if (pattern.PayoutDay < 1 || pattern.PayoutDay > 31)
                throw new InvalidOperationException("PayoutDay must be between 1 and 31 for SpecificDayOfMonth.");
        }
        else if (pattern.PayoutDayType == PayoutDayType.SpecificDayOfWeek)
        {
            if (pattern.PayoutDay < 0 || pattern.PayoutDay > 6)
                throw new InvalidOperationException("PayoutDay must be between 0 (Sunday) and 6 (Saturday) for SpecificDayOfWeek.");
        }
    }
}
