using System;
using System.Collections.Generic;

/// <summary>
/// Contains all the requisite information for a founder.
/// </summary>
public class FounderContext
{
    public ProjectInfo ProjectInfo { get; set; }
    public ProjectSeeders ProjectSeeders { get; set; }
    public string ChangeAddress { get; set; }
    public List<string> InvestmentTrasnactionsHex { get; set; } = new ();
}
