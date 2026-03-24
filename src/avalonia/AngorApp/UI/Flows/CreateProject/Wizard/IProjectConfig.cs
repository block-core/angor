using ReactiveUI.Validation.Abstractions;

namespace AngorApp.UI.Flows.CreateProject.Wizard
{

    public interface IProjectConfig : IProjectProfile
    {

        IAmountUI? TargetAmount { get; set; }
        int? PenaltyDays { get; set; }
        long? PenaltyThreshold { get; set; }
        DateTime? ExpiryDate { get; set; }


        Angor.Shared.Models.ProjectType ProjectType { get; set; }
    }
}

