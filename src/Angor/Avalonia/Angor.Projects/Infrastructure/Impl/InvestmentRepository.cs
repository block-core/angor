using Angor.Projects.Application.Dtos;
using Angor.Projects.Domain;
using Angor.Projects.Infrastructure.Interfaces;
using Angor.Shared.Services;
using CSharpFunctionalExtensions;

namespace Angor.Projects.Infrastructure.Impl;

public class InvestmentRepository(IIndexerService indexerService) : IInvestmentRepository
{
    public async Task<Result> Save(Investment investment)
    {
        return Result.Success();
    }

    public async Task<Result<IEnumerable<Investment>>> Get(ProjectId projectId)
    {
        return Result.Success<IEnumerable<Investment>>(new List<Investment>());
    }

    public async Task<Result<IList<InvestmentDto>>> GetByProject(ProjectId projectId)
    {
        var investments = await indexerService.GetInvestmentsAsync(projectId.Value);
            
        var investmentDtos = investments.Select(inv => new InvestmentDto
        {
            ProjectId = projectId,
            InvestorKey = inv.InvestorPublicKey,
            Amount = inv.TotalAmount,
            TransactionId = inv.TransactionId
        }).ToList();
       
        return investmentDtos;
    }
}