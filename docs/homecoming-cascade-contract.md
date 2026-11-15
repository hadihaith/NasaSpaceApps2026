# Homecoming · Arizona cascade contract

Tasks 2–4 share this contract. Derive the cascade from the campaign's existing same-start inherited and edited monthly projections. The campaign owns calculation, persistence and replay. The cascade describes a conditional local simulation. Historical inference comes from the separately reviewed archive.

## Integration and ownership

Paths in this table are relative to the repository root. Each owner also owns the Unity .meta files for files they add.

| Task | Write ownership | Integration |
| --- | --- | --- |
| 1 · Contract and fixtures | `docs/homecoming-cascade-contract.md` | Fixtures below use the shipped `Climate.json`. |
| 2 · Extraction | `unity/Homecoming/Assets/FieldSimulation/Runtime/FieldCascade.cs` (new), `unity/Homecoming/Assets/FieldSimulation/Editor/FieldRuntimeValidation.cs` | Implement the API and add fixture checks to `FieldRuntimeValidation.Run`. Read `FieldCampaign.cs`, `ClimateModel.cs`, `LocalWaterBalance.cs` and `FieldCalendar.cs` as inputs. |
| 3 · Playback | `unity/Homecoming/Assets/FieldSimulation/Runtime/FieldScenarioInterface.cs`, `unity/Homecoming/Assets/FieldSimulation/Runtime/FieldSimulation.cs`, `unity/Homecoming/Assets/FieldSimulation/Runtime/FieldInterface.cs` | Own draft readout, scenario lifecycle, playback and report entry. Those partial files contain in-progress changes. Preserve them. |
| 4 · Report | `unity/Homecoming/Assets/FieldSimulation/Runtime/FieldCascadeReportInterface.cs` (new partial) | Use existing archive lookup and finding helpers read-only. Own the report view, data binding and actions within this partial. Task 3 supplies the entry point and invokes the drawing hook. |
| 5+ · Mission, reset and integration | Coordinate shared edits with the owners above | Mission owns case/broadcast integration. Reset owns `FieldCampaign.cs` and its reset-specific UI. Coordinate any validation edits with task 2. |

For the report seam, task 4 supplies `OpenCascadeReport(CascadeResult result)`, `DrawCascadeReport()`, `CloseCascadeReport()` and `bool CascadeReportOpen { get; }` on the `FieldSimulation` partial. Task 3 passes the current result to the open method, dispatches drawing and Escape while open, and closes it before leaving or replacing the field view. Keep the report and its historical inspection inside the field view so returning preserves the pending run and playback cursor. Task 4 can build the exact historical detail with the shared `DrawHistoricalChart` and `DrawArchiveTable` helpers. It checks the active pending or committed run identity before state-changing actions. Advance goes through `CommitScenario()` to retain guided-case bookkeeping; refresh from the saved run only after success. Discard goes through `CancelPreview()` or `OpenWeatherLab()`. Historical filing follows the existing `CanFile` and `FileOrigin` rules. Existing case and broadcast files, archive, saves, scenes and climate assets stay outside the parallel write scopes.

## C# API

All types are in `Homecoming.Field`. The names and shapes below are the interface. Use detached results and arrays and treat them as read-only at call sites. Keep the results in memory. Existing campaign snapshots remain the persisted source of truth.

```csharp
public static class FieldCascade
{
    public const string RulesVersion = "homecoming-cascade-1";
    public static CascadeResult Analyze(ScenarioPreview preview, string siteId);
    public static CascadeResult Analyze(ScenarioRun run, string siteId);
}
```

| Type | Public members and semantics |
| --- | --- |
| `CascadeResult` | `string rulesVersion, runId, branchId, sourceRevisionId, committedRevisionId, editedSiteId, siteId, modelVersion, dataFingerprint, unavailableReason`; `CascadeProvenance provenance`; `CascadeStatus status`; `int startMonth, committedMonths`; `bool hasPhysicalLedger`; `ForcingEdit[] edits`; `CascadeFrame[] frames`; `CascadeSeries[] series`; `CascadeEffect[] effects`; `CascadeSeries Series(CascadeMetric metric)`. |
| `CascadeProvenance` | `Preview, Committed`. |
| `CascadeStatus` | `Unavailable, Quiet, Response`. |
| `CascadeFrame` | `int monthIndex`; `FieldMonth inherited, edited`. The pair includes resolved forcing, weather, end land state and each water ledger for the monthly log. |
| `CascadeSeries` | `CascadeMetric metric`; `CascadeKind kind`; `string label, valueUnit, deltaUnit, unavailableReason`; `double tolerance`; `bool available`; `int firstMeaningfulMonth` (`-1` if none); `CascadeSample[] samples`. `Series(metric)` returns the entry even when that metric is unavailable. |
| `CascadeSample` | `int monthIndex`; `double inherited, edited, delta`; `CascadeDirection direction`. |
| `CascadeEffect` | `CascadeMetric metric`; `CascadeTransition transition`; `CascadeSample sample`; `CascadeDirection priorMeaningfulDirection`; `string explanation`; `CascadeSampleKey[] inputs`. Join the metric to its series for label, units and category, and use the result's `siteId` for location. |
| `CascadeSampleKey` | `CascadeMetric metric`; `int monthIndex`. Refers to a raw sample in this result, including samples below display tolerance. |
| `CascadeKind` | `Direct, Physical, Educational`. |
| `CascadeDirection` | `LittleChange, Increase, Decrease`. |
| `CascadeTransition` | `Onset, ReturnToQuiet, Reversal, Resumed`. |

Use the run's pins as supplied, including on replay with a different currently installed pack. Copy edits in original order. A preview has an empty committed revision ID and `committedMonths == 0`. Call `Analyze(preview, siteId)` on the active `PendingPreview`. Its snapshot also works with the run overload. Preview covers the full stored horizon, currently 12 months. A committed result uses `Campaign.GetRun(runId)` or `RunForRevision(revisionId)`, with a nonempty committed revision ID and a positive `committedMonths`. Analyze exactly that prefix, including partial commits. Ignore the stored projections beyond the committed prefix. Analyze the saved run again after a successful advance rather than relabeling the preview. All month indices are absolute `FieldCalendar` indices. Playback offset is `monthIndex - startMonth`.

Use `run.Site(siteId)` for each comparison and require two same-start trials, paired frames with matching contiguous calendar indices and a valid prefix. A missing run, projection or pair, invalid width or date, or inconsistent commit metadata yields `Unavailable` with a reason and empty effects. For otherwise valid frames, retain weather and usable index series even if the physical ledger is absent. A series is available when all its covered pairs have valid finite inputs. Physical series require `water.available` and `land.hasStorage` for both sides throughout the window. `hasPhysicalLedger` is true when both whole-window ledgers support all physical series. Missing series have empty samples and a reason. Check snapshot fields and the presence of data explicitly. Unity's default numeric values can look like valid zeros. An illustrative run, including migrated v1 Arizona, uses the legacy wetness index and can show available educational effects. An available ledger can contain actual zero values. Arrays are empty when no entries apply.

A result is `Response` if any available Physical or Educational series has an onset, `Quiet` if at least one such series is available and all stay inside tolerance, and `Unavailable` if no modeled comparison is available or the pair is invalid. A direct forcing response alone can accompany `Quiet`. Show the qualification when some outputs are unavailable. A quiet result says the displayed outcomes stayed within tolerance over the covered months. It is neither a discovery nor a coverage gap. Pre-existing scenario-only `ClimateModel.Findings` may describe the run's absolute conditions and do not indicate an effect of the edit.

### Metrics and timing

This table defines `CascadeMetric` members and same-month display order. Calculations and thresholds use unrounded values. A delta is always edited minus inherited. Tolerances are inclusive around zero and are teaching display thresholds, in delta units. They carry neither statistical nor hazard meaning.

| Metric | Kind | Frame value | Value / delta unit | Tolerance |
| --- | --- | --- | --- | ---: |
| `Temperature` | Direct | `weather.temperature` | °C / °C | 0.01 |
| `Rain` | Direct | `weather.rain` | mm/month / mm/month | 0.1 |
| `Humidity` | Direct | `weather.humidity` | % / percentage points | 0.01 |
| `Wind` | Direct | `weather.wind` | m/s / m/s | 0.01 |
| `VaporPressureDeficit` | Physical | `water.vpdKpa` | kPa / kPa | 0.05 |
| `ReferenceEt` | Physical | `water.et0Mm` | mm/month / mm/month | 1 |
| `ActualEt` | Physical | `water.actualEtMm` | mm/month / mm/month | 1 |
| `Runoff` | Physical | `water.runoffMm` | mm/month / mm/month | 1 |
| `Drainage` | Physical | `water.drainageMm` | mm/month / mm/month | 1 |
| `RootZoneStorage` | Physical | `water.storageMm` | mm / mm | 1 |
| `RootZoneWetnessIndex` | Educational | `land.soil` when both sites' frame states have `hasStorage == false` | index (0–1) / index points | 0.01 |
| `DroughtStress` | Educational | `land.drought` | index (0–1) / index points | 0.02 |
| `Vegetation` | Educational | `land.vegetation` | index (0–1) / index points | 0.02 |
| `FuelDryness` | Educational | `land.fuel` | index (0–1) / index points | 0.02 |

Expose reference ET as modeled atmospheric drying demand, actual ET as modeled water loss, and runoff as local overflow. The storage value is an end-of-month depth equivalent. The physical stress index is the fractional gap between actual and reference ET (`1 - actualEt / et0`, 0 when ET0 is 0). The legacy stress index has a separate illustrative formula. Vegetation and fuel are teaching indices, with state carried from the previous month. Use their units explicitly. The fuel index does not estimate wildfire likelihood, ignition or burned area. Arizona uses a 150 mm uncalibrated bucket, normals spin-up and estimated radiation. It has no channel routing or cross-region propagation. See `LocalWaterBalance.Assumptions` for the model context.

For each available series, classify `delta > tolerance` as Increase and `delta < -tolerance` as Decrease. Values exactly at either tolerance are LittleChange. Emit `Onset` at the first meaningful sample. Emit `ReturnToQuiet` upon first re-entry into tolerance after an active sample and keep the last meaningful direction. Emit `Reversal` when an opposite meaningful sign appears, including across a quiet interval. Emit `Resumed` for the same sign after a quiet interval. `priorMeaningfulDirection` is LittleChange on onset and the last meaningful sign on the other transitions. Further months of the same classification stay in `samples`. A negative delta on a stress index means lower stress compared with inherited weather. A return to tolerance means the paired outcomes converge, even if both are still stressed. Sort emitted effects by absolute month then by the table order, with the table order also defining enum values.

Playback reveals effects with `sample.monthIndex <= startMonth + DisplayedMonth`. Rewind hides later effects, skip moves to the last visible month, and changing the landscape between inherited and edited views leaves the comparison and reveal cursor intact. The month scrubber already uses the paired frames. Reduced motion and the text path use the same event list. On the draft screen use `ClimateModel.Normal` and `ApplyForcing` at the source revision's next month with the inherited and draft control values. Show the applied weather difference and the dial edit separately. Rain is a multiplier on the normal, while humidity and wind may clamp. A dial change can therefore leave the actual weather unchanged.

### Actual dependencies

A node explanation describes the calculation and the paired change. Identify changed modeled inputs when they exist and allow multiple inputs or independent responses in one month. For `inputs`, use the available changed samples from the dependency map below, in month and metric order, with “changed” meaning a finite nonzero paired difference even below display tolerance. Include prior-month sample keys only within the result. Thresholds, min/max gates and competing terms can mute or cancel an input. The keys identify changed inputs to the calculation and support explanations of these gates. The monthly log can resolve all input keys. In the animation, draw an edge only when an input sample is directional. Anchor it to the most recent revealed effect for that metric at or before the input sample's month. A changed input below tolerance can be explained in text.

| Output | Direct calculation inputs and explanation |
| --- | --- |
| Weather | The resolved monthly schedules and same monthly normals, via `ApplyForcing`. Show the underlying dial where applicable. Weather nodes have no cascade sample inputs. |
| VPD | Current temperature and RH, with the fixed monthly daily temperature range and humidity floor used in the ET calculation. |
| Reference ET | Current temperature, RH, wind and VPD, plus the previous month's temperature (soil heat term). With rain alone, these quantities and reference ET are unchanged. |
| Actual ET | Current reference ET and rain, plus start storage from the previous month. Available water caps the loss and scales the fraction of reference ET. |
| Local overflow and drainage | Current rain and actual ET, plus start storage from the previous month. Overflow clips water remaining after actual ET to the fixed bucket capacity; drainage removes a fraction of retained water above the drainage threshold. They are outputs of that remainder and its respective gates. |
| End storage | Current rain, actual ET, runoff and drainage, plus start storage from the previous month. The signed ledger is `start + rain - actualEt - runoff - drainage`. Current end storage can drive the *next* month's actual ET. |
| Physical drought stress | Current actual ET and reference ET, using the gap formula above. End storage is a sibling outcome this month. |
| Illustrative wetness / stress | Wetness blends prior wetness toward a clamped target driven by rain and the illustrative heat/humidity/wind evaporation score. Stress uses current illustrative wetness and the evaporation score. Show control contributions from the frame forcing when its score changes even if the applied weather was clamped. |
| Vegetation | Prior vegetation and current drought stress, plus current storage (or illustrative wetness) through the wetness and relative-soil terms. Current temperature and the heat setting can contribute directly through hot/cold terms; include only when those terms differ. |
| Fuel dryness | Prior fuel, current drought stress and the *humidity dial* directly (dry-air term), even when applied RH was clamped. Rain or temperature effects on fuel are indirect through stress unless another documented input changes. |

Use prior-month `RootZoneStorage`, `RootZoneWetnessIndex`, `Temperature`, `Vegetation` and `FuelDryness` keys as applicable; both tracks start from the same land and preceding temperature. A dial term absent from the weather comparison is explained using `CascadeFrame.edited.forcing` versus `inherited.forcing`. There is no mandated order across months beyond computed sample dates and no required path to show a response. Never turn a local overflow, wetness or fuel value into a downstream flood, real waterlogging, or a wildfire event.

## Evidence and reporting

Use two separately labeled sections in the Earth Intelligence Report. Show the preview/committed provenance and its month window in the hypothetical section, plus exact edits, inherited and edited readings, signed changes, categories, explanations and a monthly log. Use the same result as playback. Statistical significance is inapplicable to this single hypothetical simulation. Use an existing reviewed historical record as context only where the region and relevant variable match. Link to its exact archive view. In particular, a root-zone wetness fraction can provide context for storage, with its own unit and source, and has no supported conversion to bucket millimeters. VPD, reference ET, vegetation and fuel do not inherit a temperature or rainfall review.

Use `Exported`, `MakeFinding`, `HasReviewedTest` and `CheckGuidedRecords` from the existing archive/investigation partials. Preserve dataset ID, fingerprint, raw checksum, selected finding ID, variable, unit, exact study period, mode, aggregation, month where applicable, footprint, product/version, source, sample count, completeness and test/review metadata. Scope an inference to the same pinned record and view, rather than matching on site or variable name. A different dataset, fingerprint, footprint, product/version, variable, units, period, aggregation, mode or month needs its own matched review. Missing or unmatched review means **Significance not established**, with a reason and without a p-value or interval. Reviewed `significant` and `not-significant` are distinct. Keep the descriptive Theil–Sen slope and the tested OLS slope and interval labeled. Use the recorded paired Sahel–Central Chile difference only for its matched historical contrast. The scenario's numerical-input fingerprint identifies a different object from an archive evidence fingerprint.

Historical cross-check for both fixtures is the pinned Central Arizona POWER/MERRA-2 T2M annual 1981–2025 area mean (15 cells, 32.5–34.5°N, 113–111°W, 45 complete years). The dataset ID is `region_01.merra2.air_temperature_2m.member3-v1-ff4bc680ad16`. The finding ID is `historical-v2:aecfe78848a3dbdd540f06b15b155fc5a761842e4af74f1f52f3064bee49b2c8:1981:2025:annual:-`. The archive has descriptive +0.50877 °C/decade, reviewed OLS +0.46768 °C/decade (95% CI 0.34259–0.59276), two-sided slope t-test with p ≈ 2.14409e−9 at α = 0.05, and review `phoenix-temperature-review-v1`. The Arizona POWER precipitation annual view 1981–2025 has a descriptive slope of −2.70932 mm/month per decade and significance not established. Source the displayed rows from the pack. See [evidence provenance](homecoming-evidence.md).

## Deterministic fixtures

Both fixtures use `unity/Homecoming/Assets/FieldSimulation/Data/Climate.json` (file SHA-256 `2e9eee764895e4efe1d70f138323b0beee6749befe0a64c416fbcee559714265`). The captured model version is `homecoming-local-1` and its runtime input fingerprint is `4c430ab9c8b30652f7935ba31cbcc53f7f10ec73d206f0063ef89705ca206f0a`. Use an in-memory campaign or an empty temporary save path. IDs assigned to branches and revisions vary. Assert their relationships. Compare these rounded samples with absolute tolerance 0.000002 and compare timing, units, directions and event order exactly.

### A · Arizona response and inherited follow-up

Use `FieldCampaign.Open(climate, selectedSiteId: "phoenix")`. Define `s0 = campaign.Current.nextMonth` (24300). Call `Preview("phoenix", new[] { new ForcingEdit(ForcingControl.Rain, 100, s0) }, "cascade-rain")` and advance all 12 months. Start the second preview with exactly `new ForcingEdit(ForcingControl.Heat, 4, firstRevision.nextMonth)`, command ID `"cascade-heat"`. The new start is 24312. Draft inputs at that start are 8.739 → 12.739 °C, with rain 76.124 mm/month and the +100% rain dial inherited in both tracks. Commit the follow-up for saved replay checks.

Both starts have two identical land snapshots for their respective comparison. Initial run start storage is 23.437350 mm. At December 2026 end, storage is 23.437350 → 46.874701 mm, delta +23.437350 mm. January 2027 starts from 46.874701 mm and the first run's saved preceding temperature in **both** follow-up tracks. December 2027 storage is 46.874701 → 38.769874 mm, delta −8.104827 mm. Commit/reload uses the previewed values, persists each edited schedule and advances the common date. Check that the second run's source revision is the first run's committed revision, on the same branch.

For the rain run, the full ordered transitions are below. The sign is the sample direction except for a quiet return.

| Date | Ordered transitions |
| --- | --- |
| 2026-01 | Rain +, ActualEt +, RootZoneStorage +, DroughtStress −, Vegetation +, FuelDryness − (all Onset) |
| 2026-04 | RootZoneStorage ReturnToQuiet |
| 2026-06 | DroughtStress ReturnToQuiet |
| 2026-07 | DroughtStress Resumed − |
| 2026-10 | RootZoneStorage Resumed + |

January precipitation is 38.062000 → 76.124001 mm/month, actual ET 27.024932 → 43.750686 mm/month, and end storage 34.474418 → 55.810665 mm. The drought stress index is 0.590004 → 0.336258. Reference ET and VPD match throughout this run.

For the heat follow-up, the full ordered transitions are below. Changes are computed from the frames; the dates are fixture assertions.

| Date | Ordered transitions |
| --- | --- |
| 2027-01 | Temperature +, VaporPressureDeficit +, ReferenceEt +, ActualEt +, RootZoneStorage − (all Onset) |
| 2027-02 | DroughtStress Onset + |
| 2027-03 | FuelDryness Onset + |
| 2027-04 | ActualEt Reversal −, RootZoneStorage ReturnToQuiet, Vegetation Onset − |
| 2027-05 | ActualEt ReturnToQuiet, DroughtStress ReturnToQuiet |
| 2027-07 | DroughtStress Resumed + |
| 2027-10 | ActualEt Reversal +, RootZoneStorage Resumed −, DroughtStress ReturnToQuiet, FuelDryness ReturnToQuiet |
| 2027-12 | DroughtStress Resumed +, Vegetation ReturnToQuiet, FuelDryness Resumed + |

| Follow-up sample | Inherited | Edited |
| --- | ---: | ---: |
| Jan VPD (kPa) | 0.601382 | 0.781431 |
| Jan reference ET (mm/month) | 65.915165 | 73.217012 |
| Jan actual ET (mm/month) | 54.049864 | 60.037316 |
| Jan end storage (mm) | 68.948837 | 62.961386 |
| Feb drought stress (index) | 0.033861 | 0.073777 |
| Mar fuel dryness (index) | 0.327332 | 0.367225 |
| Apr actual ET (mm/month) | 51.006592 | 35.693945 |
| Apr end storage (mm) | 0 | 0 |
| Apr vegetation (index) | 0.544563 | 0.517836 |
| Oct actual ET (mm/month) | 36.911295 | 39.094002 |

January temperature drives VPD and reference ET. January storage falls as actual ET rises. January stress stays at 0.180009 on both tracks because initial available water is the same and ET remains proportional to reference ET. February stress inputs include actual and reference ET. February actual ET inputs include January end storage. In April the heated track loses less water despite higher reference ET because both tracks exhaust the available water and the heated track began with less. The October reversal follows the quiet interval and retains Decrease as its prior meaningful direction. Within this fixture, Arizona's rain, RH, wind, overflow and drainage series in the second run are available and quiet, while illustrative root-zone wetness is unavailable on both physical runs.

With a separate campaign, replay the same actions but advance the heat preview by 3 months. Its committed result covers January–March and has exactly 7 transitions, with no vegetation onset or reversal. The following world revision starts in April and its saved land equals the edited March frame. The stored 12-month preview remains unchanged. On a second site's projection, the two tracks match and yield no local effects from the Arizona edit.

### B · Arizona quiet with a real applied edit

Start another fresh campaign at `s0` and preview `new ForcingEdit(ForcingControl.Rain, 5, s0 + 5, s0 + 6)` with command ID `"cascade-quiet"`. This bounded override exercises the campaign's existing date API. A June-only +5% rain edit raises precipitation from 4.175000 to 4.383750 mm/month, delta +0.208750. Both buckets start June empty and exhaust that month's supply. Actual ET is 4.175000 → 4.383750 mm/month, reference ET is 243.172033 mm/month in both, and end storage is exactly 0 mm in both. Drought stress shifts from 0.982831 to 0.981973, vegetation from 0.401861 to 0.401970 and fuel dryness from 0.886279 to 0.885973. Small index differences persist after the dial expires.

Expected result is `Quiet`, `hasPhysicalLedger == true`, with exactly two effects. June (24305) Rain Onset Increase, then July (24306) Rain ReturnToQuiet with Increase as its prior meaningful direction and 40.944000 mm/month in both tracks. Every available Physical and Educational series has `firstMeaningfulMonth == -1`. Every monthly storage delta is zero. December storage is 23.437350 mm in each track. The frame data support explaining how the edit was absorbed, even with no meaningful downstream response. Discard this preview and verify the head and journal are untouched. A null-edit preview is also Quiet and has zero effects. A missing projection instead yields Unavailable with a reason.

### Verification and build

1. From the repository root run `node scripts/prepare-unity-simulation.mjs --check`. This compares the generated pack with the pinned checked-in data.
2. In the project version from `unity/Homecoming/ProjectSettings/ProjectVersion.txt`, run **Homecoming → Field simulation → Validate model and campaign**. Batch equivalent below takes the full project path, or the path of an isolated project copy if the working project is open. Task 2 adds fixture and tie/missing-data coverage to this entry point. Check the paired dates, identical start, ledger closure on each track (absolute error < 1e−7 mm), transitions, order, prefix, copied results, and committed replay from a reloaded temporary save. Also exercise a legacy Arizona record with no physical ledgers and a direct change that clamps to equal weather.
3. Run **Homecoming → Run playback smoke check**. Batch equivalent is `-executeMethod Homecoming.Editor.StoryValidation.Smoke`, without `-quit` or `-nographics`, so the editor can enter Play Mode. Build using **Homecoming → Build macOS player** (batch `-executeMethod Homecoming.Editor.StoryValidation.BuildMacPreview`). Check the build success marker and launch from `Intro` through `Story` to `Simulation`.
4. Use the story-first QA build and `--investigation-preview /absolute/output/folder` from the [Unity guide](../unity/README.md). In addition to the existing harness, verify draft vs inherited, report preview identity, chronological reveal and rewind, pause/skip, reduced motion and text inspection. Check same-start readings and exact archive links. Inspect an archive from the report and return with the same pending run. Discard one preview, save both Arizona response runs and reopen for replay. Check the quiet fixture via the validation path unless a timed-edit UI is available.

```sh
UNITY=/Applications/Unity/Unity-6000.6.9f1/Unity.app/Contents/MacOS/Unity
PROJECT="$PWD/unity/Homecoming"
"$UNITY" -batchmode -nographics -quit -projectPath "$PROJECT" \
  -executeMethod Homecoming.Field.FieldRuntimeValidation.Run \
  -logFile /tmp/homecoming-field-validation.log
rg 'FIELD_RUNTIME_OK' /tmp/homecoming-field-validation.log
```

The fixture values above were captured from the checked-in campaign and water-balance code running in Unity 6000.6.9f1. The existing model and campaign validator passed in an isolated Unity project with the same runtime sources and pack. A threshold pass over the Unity-exported paired frames produced the expected transitions. The cascade extractor, playback, report and full player build are verified in their implementation tasks.
