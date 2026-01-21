## 📄 `SKILLS.md`

# Skills and Working Rules for this Repository

This document defines the mandatory skills, conventions, and behavioral rules for any automated agent working in this repository (including AI assistants).
These rules must be applied before writing, modifying, or refactoring any code.

---

## Core Technical Skills

The agent working on this repository is expected to have strong expertise in:

- C# and modern .NET
- Avalonia UI
- MVVM architecture
- Clean Code and Clean Architecture
- Functional programming patterns in C#
- Reactive programming with DynamicData
- Cross-platform application design

---

## Architectural Principles

- MVVM is mandatory for all UI-related code.
- Business logic must remain independent from UI concerns.
- Prefer composition over inheritance.
- Prefer immutability where practical.
- Public APIs must be designed carefully and kept stable.
- Dependencies flow inwards only.

---

## Coding Standards

- Do NOT use the suffix `Async` in method names, even when returning `Task`.
- Do NOT use `_` prefix for private fields.
- Avoid static state unless explicitly justified.
- Favor small, expressive methods.
- Keep cyclomatic complexity low.
- Prefer explicitness over cleverness.

---

## Error Handling

- Use `Result` and `Maybe` from **CSharpFunctionalExtensions** for flow control and error handling.
- Exceptions are reserved for truly exceptional situations.
- Never leak exceptions across architectural boundaries.

---

## Avalonia UI Rules

- Always use **Avalonia**, never `System.Drawing`.
- Prefer bindings over code-behind.
- Avoid `VisualStates` unless absolutely required.
- Prefer explicit `DataTemplate`s and typed `DataContext`s when possible.
- ViewModels must not reference Avalonia types.

---

## Zafiro-Specific Guidelines

This repository follows **Zafiro** conventions. The agent must:

- Prefer existing Zafiro abstractions, helpers and extension methods over re-implementing logic.
- Search for existing Zafiro helpers before introducing new types or services.
- Prefer Zafiro's validation helpers (e.g. `ValidationRule` and validation extensions) over ad-hoc reactive logic.
- Keep UI logic MVVM-pure.
- Extend Zafiro following its established naming, layering, and reactive patterns.
- If uncertain, locate a similar feature already implemented in Zafiro and mirror that approach.

---

## DynamicData & Reactive Rules (Mandatory)

### Required Approach

- Prefer **DynamicData** operators and extension methods over plain `System.Reactive` operators when working with observable collections.
- Build pipelines from existing sources and keep them as a single, readable chain.
- Prefer:
  `Connect()`, `Filter(...)`, `FilterOnObservable(...)`, `Transform(...)`, `Sort(...)`, `Bind(...)`, `DisposeMany(...)`, `IsEmpty()` and related DynamicData operators.
- Prefer `DisposeWith(...)` for lifecycle management.
- Subscriptions must be minimal, centralized, and for side-effects only.

### Forbidden / Strong Anti-Patterns

- Do NOT create new `SourceList` / `SourceCache` on the fly to solve local problems.
- Do NOT place business logic inside `Subscribe(...)`.
- Do NOT use `System.Reactive` operators when an equivalent DynamicData operator exists.
- Do NOT scatter subscriptions across the ViewModel.

### Canonical Validation Pattern

When validating dynamic collections, the preferred and mandatory pattern is:

```csharp
this.ValidationRule(
        StagesSource
            .Connect()
            .FilterOnObservable(stage => stage.IsValid)
            .IsEmpty(),
        b => !b,
        _ => "Stages are not valid")
    .DisposeWith(Disposables);
```

This pattern must not be replaced by manual Rx, ad-hoc caches, or Subscribe-based logic.

To filter nulls in Reactive pipelines, use WhereNotNull().

```csharp
this.WhenAnyValue(x => x.DurationPreset).WhereNotNull()
```

---

## Procedure Before Writing Reactive Code

Before writing any reactive or DynamicData code, the agent must:

1. Search the codebase for similar pipelines and follow the same style.
2. Search for existing extension methods and helpers (especially in Zafiro).
3. Only if no suitable helper exists, propose a new reusable extension method.
4. Never inline complex reactive logic inside a ViewModel.

---

## Comments and Documentation

* All code comments must be written in **English**.
* Public APIs must be clearly documented.
* README files are preferably written in English and may include a sponsorship section.

---

## Quality Expectations

* All code must be production-grade.
* Existing behavior must be preserved unless explicitly requested.
* Refactors must improve clarity, maintainability, or correctness.
* Never introduce breaking changes silently.

---

## Behavioral Rules

* Respect existing project conventions at all times.
* Never simplify code at the expense of correctness.
* If uncertain about architectural changes, ask before proceeding.
* The goal is long-term maintainability and correctness, not short-term convenience.
