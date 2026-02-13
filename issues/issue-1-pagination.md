
**Title:** Fix missing pagination in FetchAddressHistoryAsync

**Body:**
Hey, I noticed a TODO in `MempoolSpaceIndexerApi.cs` regarding the default paging limit for the mempool api.

Right now `FetchAddressHistoryAsync` only grabs the first page (50 txs) and doesn't loop to get the rest. If an address has more than 50 transactions, we're missing the older history.

I can fix this by adding a loop to fetch subsequent pages using the `after_txid` param until we get a set with fewer than 50 items.

Unassigning myself if anyone else wants to grab this, otherwise I'll push a PR shortly.
