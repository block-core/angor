using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Projects.Infrastructure.Impl;
using Angor.Sdk.Funding.Services;
using Angor.Sdk.Funding.Shared;
using Angor.Shared.Protocol;
using CSharpFunctionalExtensions;
using MediatR;

namespace Angor.Sdk.Funding.Investor.Operations;

public static class CheckPenaltyThreshold
{
    public record CheckPenaltyThresholdRequest(ProjectId ProjectId, Amount Amount) : IRequest<Result<bool>>;

    public class CheckPenaltyThresholdHandler(
        IProjectService projectService,
        IInvestorTransactionActions investorTransactionActions) 
        : IRequestHandler<CheckPenaltyThresholdRequest, Result<bool>>
    {
        public async Task<Result<bool>> Handle(CheckPenaltyThresholdRequest request, CancellationToken cancellationToken)
        {
            try
            {
                // Get the project to access its info
                var projectResult = await projectService.GetAsync(request.ProjectId);
                if (projectResult.IsFailure)
                {
                    return Result.Failure<bool>(projectResult.Error);
                }

                var projectInfo = projectResult.Value.ToProjectInfo();
                
                // Use the centralized threshold check logic
                var isAboveThreshold = investorTransactionActions.IsInvestmentAbovePenaltyThreshold(
                    projectInfo, 
                    request.Amount.Sats);

                return Result.Success(isAboveThreshold);
            }
            catch (Exception ex)
            {
                return Result.Failure<bool>($"Error checking penalty threshold: {ex.Message}");
            }
        }
    }
}
