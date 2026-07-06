# Investigation: TR100000400 Parcel-006 Sequence Gap

## Hand-off Brief

1. **What happened.** The Points Validation Tool reports parcel-006 is missing sequence values 16 and 17 even though point identifiers P16 and P17 are visible.
2. **Where the case stands.** Confirmed: the persisted review artifact has point identifiers P16/P17, but their saved sequence values are 19/20, leaving no rows with sequence 16 or 17.
3. **What's needed next.** Repair the active parcel sequence for TR100000400 and consider adding an explicit renumber action or automatic active-parcel sequence normalization after inserts.

## Case Info

| Field | Value |
| --- | --- |
| Ticket | TR100000400 |
| Date opened | 2026-07-06 |
| Status | Concluded |
| System | Parcel Workflow Add-In, Points Validation Tool |
| Evidence sources | Case artifact `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100000400\working\extraction_review_data.json`; validator source |

## Problem Statement

User reported that parcel-006 says sequence values 16 and 17 are missing, but those values appear to be present in the form.

## Confirmed Findings

### Finding 1: Point labels P16/P17 exist, but sequence values 16/17 do not

**Evidence:** `C:\Users\js91482\Documents\SidwellCo\ParcelWorkflowCases\100000400\working\extraction_review_data.json` rows around parcel-006 show `110900205_P15` has `review_sequence_in_group` 18, `110900205_P16` has 19, and `110900205_P17` has 20.

**Detail:** The visible point identifier is not the same thing as the parcel sequence value used by validation and polygon construction.

### Finding 2: The validator checks sequence values, not point identifier suffixes

**Evidence:** `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ParcelScopedReviewValidationService.cs:398` gathers `SequenceInGroup`; `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ParcelScopedReviewValidationService.cs:404` computes missing sequence values; `src/ParcelWorkflowAddIn/ParcelWorkflowAddIn/Workflow/Review/ParcelScopedReviewValidationService.cs:501` emits the displayed warning.

**Detail:** With saved sequence values `1..15,18..23`, the validator correctly reports missing `16,17`.

## Conclusion

**Confidence:** High

The message is technically correct. The confusing part is that the UI shows point identifiers P16 and P17, but the `Seq` column for those rows is 19 and 20. The current case artifact needs sequence repair for parcel-006.

## Recommended Next Steps

### Fix direction

Renumber parcel-006 rows in visual/path order so sequences become contiguous. For the current screenshot, the expected tail is P15 -> 16, P16 -> 17, P17 -> 18, P18 -> 19, P19 -> 20, P20 -> 21.

Add a code-level guard: after inserting or deleting a manual point, normalize the active parcel sequence or expose a clear "Renumber active parcel" action.
