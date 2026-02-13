
**Title:** refactor: replace magic number with NostrKeyType enum

**Body:**
Closes #4

*   Created `NostrKeyType` enum in `Shared/Models`.
*   Replaced `short` with `NostrKeyType` in `NetworkConfiguration`.
*   Updated usages in `Create.razor` and `CreateProject.cs`.

This removes the magic number `1` and addresses the TODO comment.
