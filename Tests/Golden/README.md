# FracturedJson Conformance Suite

Exact-output snapshots of representative `Formatter` cases. This is **formatter-output** conformance for family version **5.0**, not a full behavioral spec. Parser trees, native `Serialize`, tokenizer tests, and error paths stay in each language’s own tests.

Official claim: **Implements FracturedJson 5.0.**

## Layout

- `manifest.json` — case list (schema keys are camelCase; option names are PascalCase, matching `FracturedJsonOptions`)
- `defaults-v5-constructor.json` — constructor defaults for the 5.0 option set (intersection of the official C# and JS packages). `JsonEolStyle` is `Lf` here; never `Default`.
- `<folder>/input.json` or `input.jsonc` — input
- `<folder>/<variant>.txt` — expected output (UTF-8, no BOM, LF)

Options for a case are **overrides** merged onto the defaults file, then `forced` from the manifest (always `JsonEolStyle: Lf`). Enum values are identifier strings (`Preserve`, `Decimal`, `Lf`), never integers.

`CommentPolicy` defaults to `TreatAsError`. JSONC cases must set `"CommentPolicy": "Preserve"`.

Do not put custom `StringLengthFunc` / East-Asian width hooks in these cases.

## What belongs here

Goldens lock a **full character grid** for a scenario a port should match exactly. They are not a replacement for unit tests.

**Add a golden** when all of these are true:

- The input is JSON or JSONC text (`Reformat` / `Minify` only).
- Options are expressible as `FracturedJsonOptions` property values (no custom `StringLengthFunc`, no language-specific `Serialize`).
- You care about the whole layout: spaces, alignment, line breaks, dummy commas, where comments sit.
- A silent one-space drift in a Formatter cleanup would be a real bug, not just a different-but-valid pretty-print.

Grain: one *kind of document* per folder, plus a few option variants of that same input (line length, comma placement, minify). Prefer something that looks like user data over a two-line micro-example and over a huge dump.

**Keep it as a unit test** when you are asserting a *rule* rather than a picture:

- “these columns line up”, “this comment stays on this line”, “output is still valid JSON”
- parser/tokenizer structure, thrown errors
- native `Serialize` (POCOs, `undefined`, `toJSON`)
- combinatorics (`UniversalJsonTests`) where you only need validity

Overlap is expected. A table unit test says the columns align; `nested-table` says exactly which spaces. If a golden fails you look at the grid; if a unit test fails you know which rule broke.

**New features:** write named C# unit tests first (collapse-brackets is the current example). When the feature exists in both official implementations — or you are about to port it — add one or two goldens with the option on. Do not try to golden every permutation, and do not delete the unit tests.

## Constructor vs Recommended

V1 cases use the **constructor** profile (`new FracturedJsonOptions()`), which is frozen for 5.x. `Recommended()` may drift on minors; when it does, add a separate defaults file and new cases rather than churning these.

Unreleased C# options (`AllowTableSegments`, collapse-bracket flags, …) are omitted from the v1 defaults file. They default to off, so they do not affect these goldens.

## C# regenerates; JS verifies

- C#: `dotnet test --filter TestCategory=Golden`
- Regeneration (C# only): `FRACTUREDJSON_UPDATE_GOLDENS=1` writes expected `.txt` files. It does **not** rewrite the manifest.
- JS: compare only. Never edit expected files in the JS copy.

## How to add a case

1. Add or reuse `Tests/Golden/<folder>/input.json` or `input.jsonc`.
2. Add a manifest entry with option overrides (and `CommentPolicy: Preserve` for JSONC).
3. Run C# tests with `FRACTUREDJSON_UPDATE_GOLDENS=1` to create `<variant>.txt`.
4. Inspect the new file. Commit if the grid is intended.
5. Sync into FracturedJsonJs (`scripts/sync-conformance.sh` from that repo, sibling checkout).
6. Run JS goldens.

## JS vendor copy

From a sibling clone of FracturedJsonJs:

```sh
./scripts/sync-conformance.sh
```

Or: `rsync -a --delete ../FracturedJson/Tests/Golden/ ./test/Golden/`
