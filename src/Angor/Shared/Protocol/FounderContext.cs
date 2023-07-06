using System;
using System.Collections.Generic;

/// <summary>
/// Contains all the requisite information for a founder.
/// </summary>
public class FounderContext
{
    public ProjectInvestmentInfo ProjectInvestmentInfo { get; set; }

    public string ChangeAddress { get; set; }

    public List<string> InvestmentTrasnactionsHex { get; set; } = new ();
}
