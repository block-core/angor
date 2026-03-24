using AngorApp.Model.Funded.Fund.Model;
using AngorApp.Model.Funded.Investment.Model;
using AngorApp.Model.Funded.Shared.Model;
using AngorApp.Model.ProjectsV2.FundProject;
using AngorApp.Model.ProjectsV2.InvestmentProject;
using FluentAssertions;

namespace AngorApp.Tests;

public class FundedPairingTests
{
    [Fact]
    public void InvestmentFunded_preserves_investment_specific_types()
    {
        IInvestmentFunded funded = new InvestmentFundedSample();

        funded.Project.Should().BeAssignableTo<IInvestmentProject>();
        funded.InvestorData.Should().BeAssignableTo<IInvestmentInvestorData>();

        IFunded asBase = funded;
        asBase.Project.Should().BeSameAs(funded.Project);
        asBase.InvestorData.Should().BeSameAs(funded.InvestorData);
    }

    [Fact]
    public void FundFunded_preserves_fund_specific_types()
    {
        IFundFunded funded = new FundFundedSample();

        funded.Project.Should().BeAssignableTo<IFundProject>();
        funded.InvestorData.Should().BeAssignableTo<IFundInvestorData>();

        IFunded asBase = funded;
        asBase.Project.Should().BeSameAs(funded.Project);
        asBase.InvestorData.Should().BeSameAs(funded.InvestorData);
    }
}
