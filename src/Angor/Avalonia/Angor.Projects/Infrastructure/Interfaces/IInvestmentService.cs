using Angor.Projects.Domain;
using CSharpFunctionalExtensions;

namespace Angor.Projects.Infrastructure.Interfaces;

public interface IInvestmentService
{
    Task<Result<string>> CreateInvestmentTransaction(
        string bitcoinAddress, 
        string investorPubKey, 
        long satoshiAmount, 
        ModelFeeRate feeRate);
}