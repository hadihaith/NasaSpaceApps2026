# Member 3 → Member 4 handoff

Member 4 is ready to receive data without changing provider integrations or adding live gameplay APIs. `/member-4` imports JSON locally; no uploaded file is sent to a server. The working synthetic example is `docs/member-4/example.synthetic.json`.

## JSON contract

```json
{
  "schemaVersion": 1,
  "id": "stable-dataset-region-variable-id",
  "variable": "Name of environmental variable",
  "units": "Product's actual units",
  "region": "Region description and bounds or point coordinates",
  "kind": "reanalysis",
  "provenance": {
    "product": "Exact product identifier",
    "version": "Exact dataset version",
    "sourceUrl": "https://official-provider.example/product",
    "retrievedAt": "YYYY-MM-DD",
    "license": "Verified terms and attribution, or UNKNOWN",
    "redistribution": "unknown",
    "processing": ["Quality mask, unit conversion and spatial aggregation details"],
    "limitations": ["Coverage, uncertainty and known product limitations"]
  },
  "observations": [
    {"date": "2000-01", "value": 1.2},
    {"date": "2000-02", "value": null},
    {"date": "2000-03", "value": 1.4}
  ]
}
```

The example above documents structure, not a valid scientific dataset or real provider URL. Valid kinds: observation, reanalysis, synthetic, simulation. Keep one variable, consistent units and one region per document. Dates must be consecutive and unique; represent every missing month explicitly with null. Never send fill values such as −999 as observations. Convert them after consulting the product specification. Do not infer missing values from legitimate zeroes.

The module accepts up to 2,400 months; the import UI limits files to 2 MB. These are project limits, not NASA/NOAA quotas. Larger data require a different preprocessing or performance plan.

## Integration

```ts
import { analyzeDataset, type ScientificReview } from "@/lib/member4/dataset";

const descriptive = analyzeDataset(member3Json, "annual");
// descriptive.points, trend, warnings, missingMonths, startYear, endYear
// Unreviewed imported data intentionally show "Significance not established".

// After Member 4 documents a dataset-specific assessment:
const review: ScientificReview = {
  reviewer: "Named Member 4 reviewer",
  rationale: "Document the evidence supporting model assumptions here",
  datasetId: descriptive.dataset.id,
  productVersion: descriptive.dataset.provenance.version,
  mode: "annual",
  startYear: descriptive.startYear,
  endYear: descriptive.endYear,
  assumptionsReviewed: true
};
const assessed = analyzeDataset(member3Json, "annual", 0, undefined, review);
```

The shown review is a programming example, not a completed scientific review. Do not set the flag merely to unlock a p-value. Imported files cannot grant themselves approval: the adapter ignores unrelated fields. Keep IDs/versions immutable when values or processing change, and repeat the review when a dataset, analysis period, aggregation or model changes. A separate integration can supply stored reviews; the standalone import UI intentionally provides no approval checkbox.

## Required data handoff and review

Member 3 owns product choice, acquisition, QA, spatial aggregation, provenance and redistribution verification. Please include region geometry, grid resolution, selected quality flags, fill-value handling, monthly statistic definition, time convention, processing script/version and product citations. Distinguish observations from model/reanalysis output.

Member 4 then reviews record length, missingness, seasonal structure, residual dependence at relevant lags, linearity, outliers, variance stability, distributional assumptions, product discontinuities and test-selection effects. Choose one defensible analysis before interpreting a p-value. Known source changes or inadequately modeled dependence can require descriptive results only.

`TrendChart.tsx` renders time series and robust lines; `DetectiveDashboard.tsx` contains the standalone controls and interpretation. `trends.js` from the earlier plan corresponds to the existing TypeScript files under `src/lib/stats/`; no duplicate JavaScript implementation is needed.

## Scope of this delivery

Statistics, time processing, independent tests, a standalone analysis dashboard, local import contract and Member 4 documentation. Existing provider APIs, acquisition code, maps, deployment and presentation are outside the change. The small guard in `game.ts` prevents unresolved scientific results from awarding scores or badges; it is not a game redesign. Existing Findings text is updated only to communicate the revised analysis accurately.
