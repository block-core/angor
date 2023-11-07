using System;
using System.Collections.Generic;
using Angor.Shared.Models;

/// <summary>
/// Contains all the requisite information for an investor to formulate an investment transaction.
/// This data is unique and tailored to each individual investor.
/// </summary>
public class InvestorContext
{
    public string InvestorKey { get; set; }

    public string InvestorSecretHash { get; set; }

    public ProjectInfo ProjectInfo { get; set; }

    public string TransactionHex { get; set; }


    // todo: does this info need to be in this class?
    // ==============================================

    public string ChangeAddress { get; set; }
}
