using Angor.Contexts.Funding.Investor.CreateInvestment;
using Angor.Contexts.Funding.Projects.Domain;
using AngorApp.Sections.Browse.Details.Invest.Draft;

namespace AngorApp;

public class DraftViewModelDesign : IDraftViewModel
{
    public long SatsToInvest { get; } = 1000;
    public InvestmentDraft Draft { get; } = new InvestmentDraft(new InvestmentTransaction("key", TransactionJson(), "id", new Amount(1234)));
    public long Feerate { get; set; } = 321;

    private static string TransactionJson()
    {
      return """
             {
               "txid": "3c1818e3e5b7e46df97c3b2789bbb713f8a436f321488ae883885b0d3927d637",
               "hash": "3c1818e3e5b7e46df97c3b2789bbb713f8a436f321488ae883885b0d3927d637",
               "version": 1,
               "size": 275,
               "vsize": 275,
               "weight": 1100,
               "locktime": 0,
               "vin": [
                 {
                   "txid": "0437cd7f8525ceed2324359c2d0ba26006d92d856a9c20fa0241106ee5a597c9",
                   "vout": 0,
                   "scriptSig": {
                     "hex": "47304402204e45e16932b8af514961a1d3a1a25fdf3f4f7732e9d624c6c61548ab5fb8cd410220181522ec8eca07de4860a4acdd12909d831cc56cbbac4622082221a8768d1d0901"
                   },
                   "sequence": 4294967295
                 }
               ],
               "vout": [
                 {
                   "value": 10,
                   "n": 0,
                   "scriptPubKey": {
                     "hex": "4104ae1a62fe09c5f51b13905f07f06b99a2f7159b2225f374cd378d71302fa28414e7aab37397f554a7df5f142c21c1b7303b8a0626f1baded5c72a704f7e6cd84cac",
                     "type": "pubkey"
                   }
                 },
                 {
                   "value": 40,
                   "n": 1,
                   "scriptPubKey": {
                     "hex": "410411db93e1dcdb8a016b49840f8c53bc1eb68a382e97b1482ecad7b148a6909a5cb2e0eaddfb84ccf9744464f82e160bfa9b8b64f9d4c03f999b8643f656b412a3ac",
                     "type": "pubkey"
                   }
                 }
               ]
             }
             """;
    }

    public IObservable<bool> IsValid { get; }
    public IObservable<bool> IsBusy { get; }
    public bool AutoAdvance { get; }
}