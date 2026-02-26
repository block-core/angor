using Angor.Sdk.Funding.Founder;
using AngorApp.UI.Sections.FundedV2.Common.Model;
using AngorApp.UI.Sections.FundedV2.Fund.Manage;
using AngorApp.UI.Sections.FundedV2.Fund.Model;
using AngorApp.UI.Sections.FundedV2.Investment.Manage;
using AngorApp.UI.Sections.FundedV2.Investment.Model;

namespace AngorApp.UI.Sections.FundedV2.Host.Manage
{
    public class ManageCardsGalleryViewModelSample
    {
        public IReadOnlyList<ManageCardsGalleryCaseSample> Cases { get; } = CreateCases();

        private static IReadOnlyList<ManageCardsGalleryCaseSample> CreateCases()
        {
            InvestmentStatus[] statuses =
            [
                InvestmentStatus.Invested,
                InvestmentStatus.PendingFounderSignatures,
                InvestmentStatus.FounderSignaturesReceived,
                InvestmentStatus.Invalid
            ];

            List<ManageCardsGalleryCaseSample> cases = new(statuses.Length * 2);

            foreach (var status in statuses)
            {
                cases.Add(CreateCase(
                              "Investment",
                              status,
                              new InvestmentFunded(new InvestmentProjectSample(), new InvestmentInvestorDataSample(status))));
                cases.Add(CreateCase("Fund", status, new FundFunded(new FundProjectSample(), new FundInvestorDataSample(status))));
            }

            return cases;
        }

        private static ManageCardsGalleryCaseSample CreateCase(string projectType, InvestmentStatus status, IFunded funded)
        {
            return new ManageCardsGalleryCaseSample($"{projectType} | {status}", new ManageViewModelSample(funded));
        }
    }

    public class ManageCardsGalleryCaseSample(string title, ManageViewModelSample viewModel)
    {
        public string Title { get; } = title;
        public ManageViewModelSample ViewModel { get; } = viewModel;
    }
}
