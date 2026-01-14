using Angor.Sdk.Common;
using Angor.Sdk.Funding.Founder;
using Angor.Sdk.Funding.Founder.Dtos;
using Angor.Sdk.Funding.Founder.Operations;
using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Projects.Dtos;
using Angor.Sdk.Funding.Projects.Operations;
using static Angor.Sdk.Funding.Founder.Operations.CreateProjectConstants.CreateProject;
using AngorApp.UI.TransactionDrafts.DraftTypes.Base;
using CSharpFunctionalExtensions;
using Serilog;

using AngorApp.UI.TransactionDrafts;
using AngorApp.UI.TransactionDrafts.DraftTypes;
using Zafiro.Avalonia.Dialogs;

namespace AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject
{
    public class ProjectDeploymentOrchestrator(
        IProjectAppService projectAppService,
        IFounderAppService founderAppService,
        UIServices uiServices,
        ILogger logger) : IProjectDeploymentOrchestrator
    {
        private string? projectInfoEventId;

        public async Task<Result<string>> Deploy(WalletId walletId, CreateProjectDto dto, ProjectSeedDto projectSeed)
        {
            string? transactionId = null;

            var transactionDraftPreviewerViewModel = new TransactionDraftPreviewerViewModel(
                async feerate =>
                {

                    var result = await CreateProjectTransactionDraft(walletId, feerate, dto, projectSeed);
                    return result.Map(response =>
                    {
                        transactionId = response.TransactionDraft.TransactionId;
                        ITransactionDraftViewModel viewModel = new TransactionDraftViewModel(response.TransactionDraft, uiServices);
                        return viewModel;
                    });
                },
                model =>
                {

                    return SubmitProjectTransaction(new PublishFounderTransaction.PublishFounderTransactionRequest(model.Model))
                        .Tap(txId =>
                        {
                            transactionId = txId;
                            uiServices.NotificationService.Show("Project created successfully!", "Success");
                        })
                        .Map(_ => Guid.Empty);
                },
                uiServices);

            var dialogRes = await uiServices.Dialog.ShowAndGetResult(transactionDraftPreviewerViewModel, "Review Project Creation", s => s.CommitDraft.Enhance("Create Project"));

            return dialogRes.HasValue
                ? Result.Success(transactionId ?? "Unknown")
                : Result.Failure<string>("Project creation was canceled");
        }

        private async Task<Result<CreateProjectResponse>> CreateProjectTransactionDraft(WalletId walletId, long feerate, CreateProjectDto dto, ProjectSeedDto projectSeed)
        {
            if (projectInfoEventId == null)
            {
                var prerequisites = await EnsurePrerequisites(walletId, projectSeed, dto);
                if (prerequisites.IsFailure)
                {
                    return Result.Failure<CreateProjectResponse>(prerequisites.Error);
                }
                projectInfoEventId = prerequisites.Value;
            }

            logger.Information("Creating blockchain transaction for project {ProjectName}", dto.ProjectName);
            return await projectAppService.CreateProject(walletId, feerate, dto, projectInfoEventId, projectSeed);
        }

        private async Task<Result<string>> SubmitProjectTransaction(PublishFounderTransaction.PublishFounderTransactionRequest request)
        {
            return await founderAppService.SubmitTransactionFromDraft(request)
                .Tap(response => logger.Information("Project created successfully: {TransactionId}", response.TransactionId))
                .Map(response => response.TransactionId);
        }

        private async Task<Result<string>> EnsurePrerequisites(WalletId walletId, ProjectSeedDto projectSeed, CreateProjectDto dto)
        {
            logger.Information("Creating profile for project {ProjectName}", dto.ProjectName);
            var profileResult = await projectAppService.CreateProjectProfile(walletId, projectSeed, dto);

            if (profileResult.IsFailure)
            {
                logger.Error("Failed to create Nostr profile: {Error}", profileResult.Error);
                return Result.Failure<string>(profileResult.Error);
            }

            logger.Information("Creating project info for project {ProjectName}", dto.ProjectName);
            var projectInfoResult = await projectAppService.CreateProjectInfo(walletId, dto, projectSeed);

            if (projectInfoResult.IsFailure)
            {
                logger.Error("Failed to create project info: {Error}", projectInfoResult.Error);
                return Result.Failure<string>(projectInfoResult.Error);
            }

            return Result.Success(projectInfoResult.Value.EventId);
        }
    }
}
