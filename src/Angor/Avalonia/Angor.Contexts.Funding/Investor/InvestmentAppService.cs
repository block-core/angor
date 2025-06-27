using Angor.Contexts.Funding.Founder;
using Angor.Contexts.Funding.Founder.Operations;
using Angor.Contexts.Funding.Investor.Dtos;
using Angor.Contexts.Funding.Investor.Operations;
using Angor.Contexts.Funding.Projects.Domain;
using CSharpFunctionalExtensions;
using MediatR;
using Investment = Angor.Contexts.Funding.Founder.Operations.Investment;

namespace Angor.Contexts.Funding.Investor;

public class InvestmentAppService(IInvestmentRepository investmentRepository, IMediator mediator) : IInvestmentAppService
{
    public Task<Result<CreateInvestment.Draft>> CreateInvestmentDraft(Guid sourceWalletId, ProjectId projectId, Amount amount, DomainFeerate feerate)
    {
        return mediator.Send(new CreateInvestment.CreateInvestmentTransactionRequest(sourceWalletId, projectId, amount, feerate));
    }

    public Task<Result<Guid>> Invest(Guid sourceWalletId, ProjectId projectId, CreateInvestment.Draft draft)
    {
        return mediator.Send(new RequestInvestment.RequestFounderSignaturesRequest(sourceWalletId, projectId, draft));
    }

    public Task<Result<IEnumerable<Investment>>> GetInvestments(Guid walletId, ProjectId projectId)
    {
        return mediator.Send(new GetInvestments.GetInvestmentsRequest(walletId, projectId));
    }

    public Task<Result> ApproveInvestment(Guid walletId, ProjectId projectId, Investment investment)
    {
        return mediator.Send(new ApproveInvestment.ApproveInvestmentRequest(walletId, projectId, investment));
    }

    public async Task<Result<IEnumerable<InvestedProjectDto>>> GetInvestorProjects(Guid idValue)
    {
        await Task.Delay(2000);

        var mockProjects = new List<InvestedProjectDto>
        {
            new InvestedProjectDto
            {
                Id = "1",
                Name = "DeFi Trading Platform",
                Description = "Advanced decentralized trading platform with automated market making",
                LogoUri = new Uri("https://images.unsplash.com/photo-1639762681485-074b7f938ba0?w=150&h=150&fit=crop"),
                FounderStatus = FounderStatus.Approved,
                Target = new Amount(50000000), // 0.5 BTC
                Raised = new Amount(23500000), // 0.235 BTC
                InRecovery = new Amount(0),
                InvestmentStatus = InvestmentStatus.FounderSignaturesReceived
            },
            new InvestedProjectDto
            {
                Id = "2",
                Name = "Bitcoin Lightning Wallet",
                Description = "Next-generation Lightning Network wallet with advanced privacy features",
                LogoUri = new Uri("https://images.unsplash.com/photo-1621761191319-c6fb62004040?w=150&h=150&fit=crop"),
                FounderStatus = FounderStatus.Approved,
                Target = new Amount(30000000), // 0.3 BTC
                Raised = new Amount(28750000), // 0.28750 BTC
                InRecovery = new Amount(1200000), // 0.012 BTC,
                InvestmentStatus = InvestmentStatus.PendingFounderSignatures
            },
            new InvestedProjectDto
            {
                Id = "3",
                Name = "NFT Marketplace",
                Description = "Decentralized marketplace for Bitcoin-based NFTs and digital collectibles",
                LogoUri = new Uri("https://images.unsplash.com/photo-1618005198919-d3d4b5a92ead?w=150&h=150&fit=crop"),
                FounderStatus = FounderStatus.Invalid,
                Target = new Amount(75000000), // 0.75 BTC
                Raised = new Amount(8900000), // 0.089 BTC
                InRecovery = new Amount(0),
                InvestmentStatus = InvestmentStatus.Invested
            },
            new InvestedProjectDto
            {
                Id = "4",
                Name = "Renewable Energy Mining",
                Description = "Sustainable Bitcoin mining operation powered by renewable energy sources",
                LogoUri = new Uri("https://images.unsplash.com/photo-1473341304170-971dccb5ac1e?w=150&h=150&fit=crop"),
                FounderStatus = FounderStatus.Approved,
                Target = new Amount(100000000), // 1.0 BTC
                Raised = new Amount(67800000), // 0.678 BTC
                InRecovery = new Amount(3400000), // 0.034 BTC
                InvestmentStatus = InvestmentStatus.PendingFounderSignatures
            },
            new InvestedProjectDto
            {
                Id = "5",
                Name = "Educational Platform",
                Description = "Interactive Bitcoin and blockchain education platform with certification",
                LogoUri = new Uri("https://images.unsplash.com/photo-1503676260728-1c00da094a0b?w=150&h=150&fit=crop"),
                FounderStatus = FounderStatus.Approved,
                Target = new Amount(25000000), // 0.25 BTC
                Raised = new Amount(25000000), // 0.25 BTC (fully funded)
                InRecovery = new Amount(0),
                InvestmentStatus = InvestmentStatus.FounderSignaturesReceived
            }
        };

        return Result.Success<IEnumerable<InvestedProjectDto>>(mockProjects);
    }

    public async Task<Result> ConfirmInvestment(int investmentId)
    {
        await Task.Delay(2000);
        return Result.Success();
    }
}