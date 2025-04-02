using Angor.Projects.Domain;
using Angor.Projects.Infrastructure.Interfaces;
using CSharpFunctionalExtensions;

namespace Angor.Projects.Infrastructure.Impl;

public class InvestmentService : IInvestmentService
{
    public Task<Result<string>> CreateInvestmentTransaction(string bitcoinAddress, string investorPubKey, long satoshiAmount, ModelFeeRate feeRate)
    {
        throw new NotImplementedException();
    }
}