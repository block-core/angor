using Angor.Sdk.Funding.Projects.Application.Dtos;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Shared.Models;
using Stage = Angor.Shared.Models.Stage;

namespace Angor.Sdk.Funding.Projects.Infrastructure.Impl;

public static class ProjectExtensions
{
    public static ProjectDto ToDto(this Project project)
    {
        return new ProjectDto
        {
            Id = project.Id,
            Banner = project.Banner,
            Avatar = project.Picture,
            Name = project.Name,
            ShortDescription = project.ShortDescription,
            FundingStartDate = project.StartingDate,
            PenaltyDuration = project.PenaltyDuration,
            PenaltyThreshold = project.PenaltyThreshold,
            NostrNpubKeyHex = project.NostrPubKey,
            FundingEndDate = project.EndDate,
            InformationUri = project.InformationUri,
            TargetAmount = project.TargetAmount,
            Stages = project.Stages.Select(stage => new StageDto
            {
                Index = stage.Index,
                RatioOfTotal = stage.RatioOfTotal,
                ReleaseDate = stage.ReleaseDate,
            }).ToList(),
            
            // New fields for Fund/Subscribe support
            Version = project.Version,
            ProjectType = project.ProjectType,
            DynamicStagePatterns = project.DynamicStagePatterns ?? new List<DynamicStagePattern>()
        };
    }
  
    public static ProjectInfo ToProjectInfo(this Project project)
    {
        return new ProjectInfo
        {
            // Version and ProjectType
            Version = project.Version,
            ProjectType = project.ProjectType,
   
            // Core project identification
            FounderKey = project.FounderKey,
            FounderRecoveryKey = project.FounderRecoveryKey,
            ProjectIdentifier = project.Id.Value,
            NostrPubKey = project.NostrPubKey,
         
            // Dates
            StartDate = project.StartingDate,
            EndDate = project.EndDate,
            ExpiryDate = project.ExpiryDate,
      
            // Penalties
            PenaltyDays = project.PenaltyDuration.Days,
            PenaltyThreshold = project.PenaltyThreshold,
          
            // Funding details
            TargetAmount = project.TargetAmount,
   
            // Stages (for Invest projects)
            Stages = project.Stages.Select(stage => new Stage
            {
                ReleaseDate = stage.ReleaseDate,
                AmountToRelease = stage.RatioOfTotal,
            }).ToList(),
      
            // Dynamic patterns (for Fund/Subscribe projects)
            DynamicStagePatterns = project.DynamicStagePatterns ?? new List<DynamicStagePattern>()
        };
    }
}