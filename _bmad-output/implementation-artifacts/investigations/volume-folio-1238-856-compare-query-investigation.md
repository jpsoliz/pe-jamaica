# Volume/Folio 1238/856 Compare Query Investigation

## Hand-off Brief

Volume/Folio `1238/856` fails in the Compare app because the live query reaches Innola endpoints that return `401 Unauthorized`, including the BA Unit fallback route. Volume/Folio `1248/856` shows a related but distinct logic gap: owner search returns party-shaped raw rows only, no mapped property record, and the code does not fall back to BA Unit because fallback is currently limited to zero raw rows. Name search working does not contradict this; name search uses the owner route and can return useful owner/property rows in cases where Volume/Folio does not.

## Evidence

- Confirmed: `100000814/working/compare_legal_query_trace.json` has repeated `volume=1238;folio=856` failures with message `Innola owner search could not be completed. Try again.` and diagnostics showing `401 Unauthorized` at `/api/v4/rest/portal/searches`.
- Confirmed: the same trace later has `volume=1238;folio=856` failures with message `Innola BA Unit search could not be completed. Try again.` and diagnostics showing `401 Unauthorized` at `/api/v4/rest/search/`.
- Confirmed: the trace has `volume=1248;folio=856` returning two raw rows, but none mapped as property evidence. The first raw row is `type=party_type_individual`.
- Confirmed: `CompareCadasterQueryServices.cs` only falls back from owner-search Volume/Folio to BA Unit when the owner-search result is successful, `no_record_returned`, and `RawRecordCount == 0`.

## Conclusion

The issue is not simply that the first record has no owner. The stronger cause is that Volume/Folio is routed through owner search first, and fallback behavior is too narrow:

- `1238/856`: fallback is attempted after zero owner rows, but the BA Unit route returns Unauthorized in the current app session.
- `1248/856`: owner search returns party-only rows, so the current fallback rule does not attempt BA Unit search even though no property evidence was mapped.

## Fix Direction

1. Extend Volume/Folio fallback to also run BA Unit search when owner search returns no mapped property records but only party matches/raw rows.
2. Review BA Unit search authentication for `/api/v4/rest/search/`; the trace shows the retry path still ends with `401 Unauthorized`.
3. Add regression tests for both cases: zero owner rows fallback and party-only owner rows fallback.
