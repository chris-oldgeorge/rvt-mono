# Globalization analyzer suppressions (CA1304 / CA1311 / CA1862)

Some EF query-builder methods carry `[SuppressMessage]` for CA1304, CA1311 and CA1862. This documents why —
so the suppressions stay honest and nobody "fixes" them into a runtime bug.

## The pattern being suppressed

Case-insensitive text search, expressed inside an `IQueryable` predicate:

```csharp
return rows.Where(r => r.Name.ToLower().Contains(search));
```

The analyzer wants `StringComparison.OrdinalIgnoreCase` (CA1862) or an explicit culture (CA1304/CA1311). In an
EF predicate, neither works, and the current form is correct on both providers this app runs on.

## Why it can't be rewritten (verified 2026-07-15 via `ToQueryString`)

The app is tested on the **EF InMemory** provider (`SpaTestApplicationFactory`) and runs on **Npgsql**. Six-plus
tests exercise these search endpoints (`/api/companies?searchText=`, `/api/report-rules?searchText=`,
`/api/users?searchText=`, …). A replacement has to work on both.

| Candidate | Npgsql | InMemory | Verdict |
| --- | --- | --- | --- |
| `col.ToLower().Contains(x)` (current) | ✅ `lower(col) LIKE '%x%'` | ✅ client-side | works on both |
| `col.Contains(x, StringComparison.OrdinalIgnoreCase)` | ❌ throws (untranslatable) | ✅ | breaks production |
| `col.ToLowerInvariant().Contains(x)` | ❌ throws | ✅ | breaks production |
| `EF.Functions.ILike(col, "%x%")` | ✅ | ❌ throws (relational-only) | breaks tests |

`ToLower().Contains()` is the only form in the intersection. It is not a culture bug: on Npgsql the `ToLower`
never executes in .NET (it becomes SQL `lower()`), and on InMemory the tests run under `en-US`. The generated
SQL is identical to the `ILike` form, including unescaped `%`/`_` wildcard behaviour.

## What was NOT suppressed — it was fixed

- **CA1305 (formatting)** — every instance was given `CultureInfo.InvariantCulture`. Those are real: they run
  in .NET and format numbers/times.
- **In-memory culture folding** — `UserListApplicationService.ApplySearch` filters an in-memory dictionary, not
  a query, so it uses `StringComparison.OrdinalIgnoreCase`. That one *was* a latent Turkish-I bug.

The rules stay **enabled** everywhere else, so a genuine client-side `ToLower()` on user data still gets caught.
Only the EF query predicates are suppressed, method by method.

## If you touch a suppressed method

Keep the `ToLower().Contains()` form. If you must change the search semantics, re-run the `ToQueryString`
translation check against Npgsql before assuming any replacement works, and confirm the InMemory search tests
still pass.
