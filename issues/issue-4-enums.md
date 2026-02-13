
**Title:** Refactor magic number NostrEventIdKeyType to Enum

**Body:**
Hey, found another small cleanup task.

In `NetworkConfiguration.cs`, `NostrEventIdKeyType` is defined as a `short` with a value of `1`. There's a TODO comment suggesting to use an enum.

I'll create a proper `NostrKeyType` enum to replace this magic number. It makes the code a bit more self-documenting and type-safe.

Will open a PR for this shortly.
