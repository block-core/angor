using AngorApp.UI.Sections.FundedV2.Common.Model;
using AngorApp.UI.Sections.FundedV2.Fund.Model;
using AngorApp.UI.Sections.FundedV2.Investment.Model;
using AngorApp.UI.Sections.Shared.ProjectV2;
using FluentAssertions;
using Moq;

namespace AngorApp.Tests;

public class FundedPairingTests
{
    [Fact]
    public void InvestmentFunded_preserves_investment_specific_types()
    {
        var project = new Mock<IInvestmentProject>().Object;
        var investorData = new Mock<IInvestmentInvestorData>().Object;

        IInvestmentFunded funded = new InvestmentFunded(project, investorData);

        funded.Project.Should().BeSameAs(project);
        funded.InvestorData.Should().BeSameAs(investorData);

        IFunded asBase = funded;
        asBase.Project.Should().BeSameAs(project);
        asBase.InvestorData.Should().BeSameAs(investorData);
    }

    [Fact]
    public void FundFunded_preserves_fund_specific_types()
    {
        var project = new Mock<IFundProject>().Object;
        var investorData = new Mock<IFundInvestorData>().Object;

        IFundFunded funded = new FundFunded(project, investorData);

        funded.Project.Should().BeSameAs(project);
        funded.InvestorData.Should().BeSameAs(investorData);

        IFunded asBase = funded;
        asBase.Project.Should().BeSameAs(project);
        asBase.InvestorData.Should().BeSameAs(investorData);
    }
}
