using System.Linq;
using Angor.Contexts.Funding.Investor.Dtos;
using Angor.Contexts.Wallet.Domain;

namespace AngorApp.Sections.Portfolio.Manage;

public sealed record RecoveryState
{
    private readonly InvestorProjectRecoveryDto dto;

    public RecoveryState(WalletId WalletId, InvestorProjectRecoveryDto dto)
    {
        this.dto = dto;
        this.WalletId = WalletId;
        
        Project = new InvestedProject(dto);
        Stages = dto.Items
            .Select(IInvestorProjectStage (x) => new InvestorProjectStage(
                stage: x.StageIndex + 1,
                amount: new AmountUI(x.Amount),
                isSpent: x.IsSpent,
                status: x.Status))
            .ToList();
    }

    public List<IInvestorProjectStage> Stages { get;  }

    public InvestedProject Project { get; }

    public bool CanRecover => dto.CanRecover;
    public bool CanRelease => dto.CanRelease;
    public bool CanClaim => Stages.Any(stage => !stage.IsSpent);
    public WalletId WalletId { get; }
}