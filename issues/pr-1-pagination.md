
**Title:** fix: handle pagination in mempool api history

**Body:**
Closes #1 

Implemented the loop to fetch all pages of transactions for an address. 
Previously it would stop after the first 50 results (header limit).

Tested locally with an address having >50 txs and verified it pulls the full history now.

Let me know if the while loop logic looks good.
