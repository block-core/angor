using Angor.Contexts.Funding.Founder.Operations;
using Angor.Contexts.Funding.Investor.Dtos;
using Angor.Contexts.Funding.Investor.Operations;
using Angor.Contexts.Funding.Projects.Domain;
using Angor.Shared.Services;
using CSharpFunctionalExtensions;
using MediatR;
using Investment = Angor.Contexts.Funding.Founder.Operations.Investment;

namespace Angor.Contexts.Funding.Investor;

public class InvestmentAppService(IInvestmentRepository investmentRepository, IMediator mediator) : IInvestmentAppService
{
    public Task<Result<CreateInvestment.Draft>> CreateInvestmentDraft(Guid sourceWalletId, ProjectId projectId, Amount amount)
    {
        return mediator.Send(new CreateInvestment.CreateInvestmentTransactionRequest(sourceWalletId, projectId, amount));
    }

    public Task<Result> Invest(Guid sourceWalletId, ProjectId projectId, CreateInvestment.Draft draft)
    {
        return mediator.Send(new RequestInvestmentSignatures.RequestFounderSignaturesRequest(sourceWalletId, projectId, draft));
    }

    public Task<Result<IEnumerable<Investment>>> GetInvestments(Guid walletId, ProjectId projectId)
    {
        return mediator.Send(new GetInvestments.GetInvestmentsRequest(walletId, projectId));
    }

    public Task<Result> ApproveInvestment(Guid walletId, ProjectId projectId, Investment investment)
    {
        return mediator.Send(new ApproveInvestment.ApproveInvestmentRequest(walletId, projectId, investment));
    }

    public Task<Result<IEnumerable<InvestedProjectDto>>> GetInvestorProjects(Guid walletId)
    {
        return mediator.Send(new Investments.InvestmentsPortfolioRequest(walletId));
    }

    public async Task<Result> ConfirmInvestment(int investmentId)
    {
        await Task.Delay(2000);
        return Result.Success();
    }
}