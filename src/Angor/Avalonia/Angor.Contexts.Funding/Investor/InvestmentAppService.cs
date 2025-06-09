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
    public Task<Result<CreateInvestment.Draft>> CreateInvestmentDraft(Guid sourceWalletId, ProjectId projectId, Amount amount)
    {
        return mediator.Send(new CreateInvestment.CreateInvestmentTransactionRequest(sourceWalletId, projectId, amount));
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
        return Result.Success<IEnumerable<InvestedProjectDto>>([new InvestedProjectDto()
        {
            Id = "1",
            Name = "Project 1",
            LogoUri = new Uri("https://test.angor.io/assets/img/no-image.jpg"),
            FounderStatus = FounderStatus.Approved,
            Target = new Amount(12000000),
            Raised = new Amount(14000),
        }]);
    }

    public async Task<Result> ConfirmInvestment(int investmentId)
    {
        await Task.Delay(2000);
        return Result.Success();
    }
}