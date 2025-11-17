# Investment Conversation Service

## Overview

The `InvestmentConversationService` is a new service that combines calls to `ISignService.LookupInvestmentRequestsAsync` and `ISignService.LookupInvestmentRequestApprovals` to create a unified view of investment conversations. It stores these conversations in the database using a composite key of `WalletId` and `ProjectId`.

## Key Components

### 1. InvestmentConversation (Domain Model)

Located in: `Angor.Contexts.Funding\Shared\InvestmentConversation.cs`

This is the main document that stores investment request and approval data in the database.

**Key Properties:**
- `WalletId` - The wallet identifier
- `ProjectId` - The project identifier
- `RequestEventId` - The Nostr event ID of the investment request
- `InvestorNostrPubKey` - The investor's Nostr public key
- `ProjectNostrPubKey` - The project/founder's Nostr public key
- `RequestCreated` - When the request was created
- `EncryptedContent` - The encrypted message content
- `DecryptedContent` - The decrypted message (if available)
- **SignRecoveryRequest Properties:**
  - `ProjectIdentifier`
  - `InvestmentTransactionHex`
  - `UnfundedReleaseAddress`
  - `UnfundedReleaseKey`
- **Approval Properties:**
  - `ApprovalEventId` - The event ID of the approval (if approved)
  - `ApprovalCreated` - When the approval was created
- `Status` - Current status (Pending, Approved, Invested, Rejected)

### 2. IInvestmentConversationService (Interface)

Located in: `Angor.Contexts.Funding\Services\IInvestmentConversationService.cs`

**Main Methods:**

```csharp
// Get all conversations for a wallet and project
Task<Result<IEnumerable<InvestmentConversation>>> GetConversationsAsync(WalletId walletId, ProjectId projectId);

// Sync conversations from Nostr and store in DB
Task<Result<IEnumerable<InvestmentConversation>>> SyncConversationsFromNostrAsync(
    WalletId walletId, 
    ProjectId projectId,
    string projectNostrPubKey);

// Get conversations by status
Task<Result<IEnumerable<InvestmentConversation>>> GetConversationsByStatusAsync(
    WalletId walletId, 
    ProjectId projectId, 
    InvestmentRequestStatus status);

// Upsert operations
Task<Result> UpsertConversationAsync(InvestmentConversation conversation);
Task<Result> UpsertConversationsAsync(IEnumerable<InvestmentConversation> conversations);
```

### 3. InvestmentConversationService (Implementation)

Located in: `Angor.Contexts.Funding\Services\InvestmentConversationService.cs`

**How It Works:**

1. **Fetches Investment Requests**: Calls `ISignService.LookupInvestmentRequestsAsync` to get all investment requests from Nostr
2. **Fetches Approvals**: Calls `ISignService.LookupInvestmentRequestApprovals` to get all approval messages
3. **Combines Data**: Matches requests with their approvals using event IDs
4. **Decrypts Content**: Uses `INostrDecrypter` to decrypt and parse `SignRecoveryRequest` data
5. **Stores in DB**: Saves all conversations with a composite ID: `{walletId}_{projectId}_{requestEventId}`
6. **Updates Status**: Automatically updates status when approvals are found

## Usage Examples

### 1. Using MediatR Query Handler

The service comes with a ready-to-use MediatR handler:

```csharp
// Get all conversations for a project (with auto-sync)
var request = new GetInvestmentConversations.GetInvestmentConversationsRequest(
    walletId: myWalletId,
    projectId: myProjectId,
    filterByStatus: null,  // Optional: filter by status
    syncFromNostr: true    // Auto-sync from Nostr first
);

var result = await mediator.Send(request);

if (result.IsSuccess)
{
    foreach (var conversation in result.Value)
    {
        Console.WriteLine($"Request: {conversation.RequestEventId}");
        Console.WriteLine($"Status: {conversation.Status}");
        Console.WriteLine($"Investment Tx: {conversation.InvestmentTransactionHex}");
        Console.WriteLine($"Approval: {conversation.ApprovalEventId ?? "Not yet approved"}");
    }
}
```

### 2. Direct Service Usage

```csharp
// Inject the service
public class MyService
{
    private readonly IInvestmentConversationService _conversationService;
    
    public MyService(IInvestmentConversationService conversationService)
    {
        _conversationService = conversationService;
    }
    
    public async Task<List<InvestmentConversation>> GetPendingInvestments(
        WalletId walletId, 
        ProjectId projectId)
    {
        // Sync from Nostr
        await _conversationService.SyncConversationsFromNostrAsync(
            walletId, 
            projectId, 
            projectNostrPubKey);
        
        // Get pending investments
        var result = await _conversationService.GetConversationsByStatusAsync(
            walletId,
            projectId,
            InvestmentRequestStatus.Pending);
            
        return result.IsSuccess ? result.Value.ToList() : new List<InvestmentConversation>();
    }
}
```

### 3. Automatic Syncing in GetInvestments

The service is already integrated into the `GetInvestments` operation:

```csharp
// In GetInvestmentsHandler.GetInvestments()
// Sync investment conversations from Nostr to database in the background
_ = Task.Run(async () =>
{
    try
    {
        await conversationService.SyncConversationsFromNostrAsync(
            request.WalletId, 
            request.ProjectId, 
            nostrPubKey);
    }
    catch (Exception)
    {
        // Log errors but don't fail the main operation
    }
});
```

## Database Storage

### Collection Name
`InvestmentConversation` (auto-generated from class name)

### Composite Key
`{WalletId}_{ProjectId}_{RequestEventId}`

### Indexes
Consider adding indexes for efficient querying:
- `WalletId + ProjectId`
- `WalletId + ProjectId + Status`
- `ProjectId + InvestorNostrPubKey`

## Status Flow

```
┌──────────────────┐
│     Pending      │  Initial state when request is received
└────────┬─────────┘
         │
         ▼
┌──────────────────┐
│    Approved      │  After founder sends approval signatures
└────────┬─────────┘
         │
         ▼
┌──────────────────┐
│    Invested      │  When investment transaction is confirmed
└──────────────────┘

         OR

┌──────────────────┐
│    Rejected      │  If founder rejects (future feature)
└──────────────────┘
```

## Integration with Existing Code

### Service Registration

Already registered in `FundingContextServices.cs`:

```csharp
services.AddSingleton<IInvestmentConversationService, InvestmentConversationService>();
```

### Dependencies
- `IAngorDocumentDatabase` - For LiteDB storage
- `ISignService` - For Nostr communication
- `INostrDecrypter` - For decrypting messages
- `ISerializer` - For deserializing SignRecoveryRequest
- `ILogger` - For logging

## Benefits

1. **Unified View**: Combines requests and approvals in one place
2. **Persistent Storage**: All data stored in DB for offline access
3. **Automatic Sync**: Background syncing keeps data up-to-date
4. **Efficient Queries**: Query by status, wallet, project without hitting Nostr
5. **Audit Trail**: Full history of investment conversations with timestamps
6. **Performance**: Reduces redundant Nostr calls by caching in DB

## Future Enhancements

1. Add real-time notifications when new messages arrive
2. Implement message read/unread tracking
3. Add support for conversation threads
4. Enable message search and filtering
5. Add analytics on investment conversation patterns
6. Support for message attachments or metadata

## Testing

Example test structure:

```csharp
[Fact]
public async Task SyncConversationsFromNostr_Should_Store_Requests_And_Approvals()
{
    // Arrange
    var walletId = new WalletId("test-wallet");
    var projectId = new ProjectId("test-project");
    var projectNostrPubKey = "npub...";
    
    // Act
    var result = await _conversationService.SyncConversationsFromNostrAsync(
        walletId, 
        projectId, 
        projectNostrPubKey);
    
    // Assert
    Assert.True(result.IsSuccess);
    Assert.NotEmpty(result.Value);
    
    // Verify data stored in DB
    var conversations = await _conversationService.GetConversationsAsync(walletId, projectId);
    Assert.True(conversations.IsSuccess);
}
```

## Troubleshooting

### Common Issues

1. **No conversations synced**: 
   - Verify Nostr relays are connected
   - Check wallet and project keys are correct
   - Ensure ISignService is properly configured

2. **Decryption failures**:
   - Verify wallet has correct keys
   - Check INostrDecrypter implementation
   - Some messages may be from other investors

3. **Performance issues**:
   - Use background syncing to avoid blocking UI
   - Consider pagination for large result sets
   - Add database indexes

## Related Files

- `GetInvestmentConversations.cs` - MediatR query handler
- `ConversationMessage.cs` - Generic conversation message model (different from InvestmentConversation)
- `DirectMessage.cs` - Nostr direct message model
- `SignRecoveryRequest.cs` - Investment request data structure (in Angor.Shared)

