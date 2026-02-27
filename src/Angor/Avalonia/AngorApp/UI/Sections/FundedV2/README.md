# FundedV2 slices

`FundedV2` is organized by project-type slices plus host orchestration:

- `Common/`
  - Shared contracts and models used by all slices (`IFunded`, `IInvestorData`, `IFundedItem`, etc.).
- `Fund/`
  - Fund-specific models, samples and manage cards.
- `Investment/`
  - Investment-specific models, samples, project-list items and manage cards.
- `Host/`
  - Composition/orchestration layer (section view model, manage view model, project list and shared host views/cards).

## Practical rule

- If the code is project-type specific, place it in `Fund/` or `Investment/`.
- If the code only composes or routes between slices, place it in `Host/`.
- If the code is truly shared between both slices, place it in `Common/`.
