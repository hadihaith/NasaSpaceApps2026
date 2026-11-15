# Homecoming field mission

## 1. Make the current mission readable

Match the film's navy panels, cream type and amber primary actions. In the guided lab, present the current instruction and its single relevant dial. Lead playback with the soil-water comparison and let players open the cascade for more detail. Saving a guided test goes back to the case so the next action is visible. Verify both guided turns, saved-run replay and the full free lab in the standalone player.

## 2. Follow the evidence between actual locations

Use the Sahel and central Chile rainfall records to make the first story investigation a two-stop trip. Show the data pin, variable, product and date range before each reading. Bring the player to a shared time series, the two local trend rates and the reviewed test of their difference. Give the player one decision at each stop and an evidence-backed claim to send to Earth. Include the same sequence in the judge walkthrough.

Expand the archive location picker from data footprints. Point products get a pin and area products get their actual coverage outline. A weather map click can offer the closest supported simulation, including its distance. Keep the selected location and simulation site visible together through the report. Next, reuse the existing NASA POWER time-series endpoint for connected custom historical requests. Keep the pinned case available offline. Show a verified test only for the exact reviewed scope and distinguish descriptive results from reviewed results.

## 3. Follow a modeled effect downstream

Start with one named, connected river route and one intervention. Use the existing Potomac research to pick the upstream and downstream gauges and source catchments. Improve local runoff estimation, fit with the training window, and compare withheld discharge with calendar-month climatology and a same-month inflow baseline. Verify volume conservation and the direction, timing and magnitude of a known injected-flow change. The current Potomac screen missed the climatology benchmark at all three gauges.

Once the route meets the validation criteria, have the player choose an upstream intervention, see the local response, then follow its downstream contribution to the next gauge. Give the two sites a shared month cursor and show the same-start comparison at each. Report the gauge record, test status, modeled contribution, time resolution and limits. Use the historical comparison route for the release demo until the physical route qualifies.

## How players read results

Lead every result screen with a sentence that answers the player's question. For historical records, give the statistical significance verdict first and identify what the test covers. Declare significance only for the reviewed variable, location or matched difference, and date range. Say “The test did not find a statistically significant trend” for a reviewed result above the threshold and “Statistical significance has not been established for this record” when a reviewed test is unavailable. The two local rainfall slopes are descriptive. The reviewed verdict applies to their difference.

Put the direction and rate in the next line. Keep the time series easy to open and read, with axes, units, date range and a short explanation of the pattern. Place the actual test statistic when available, p-value, confidence interval, method and source in smaller supporting text or under **See the evidence**. Preserve those fields for the challenge walkthrough and report. Use the same hierarchy everywhere the player sees a result.

For a scenario, lead with the modeled consequence of the player's edit against the same-start baseline. Use a plain-language comparison such as “Your temperature change left the model soil drier at the end of the year.” Use “little modeled change” when the difference stays within the documented tolerance. Put exact values and the cascade underneath. Keep historical significance on the historical record.

## Chat assignments

The current build already includes the initial palette pass, a single dial per guided test, the Central Chile archive entry, and a closest-site choice on the free weather map. Use those changes as the starting point.

| Chat | Sol effort | Owns | Handoff |
| --- | --- | --- | --- |
| A · Guided lab usability | High | Guided lab and scenario playback presentation, including the lab-only methods in `FieldInterface.cs`. | A first-time player can change one control, read the paired result, and advance from rain to temperature. Verify the existing free lab and saved replay too. |
| B · Two-stop story investigation | High | `FieldBroadcast.cs`, historical flight presentation in `FieldComparison.cs`, and the broadcast entry in the story. | From the intro, visit the exact Sahel and central Chile points, compare the matched records, and send a claim supported by the reviewed evidence. |
| C · Place selection and NASA data | Extra High | Archive and site picker in `FieldArchiveInterface.cs`, evidence lookup in `FieldArchiveData.cs`, and a dedicated Unity client for the existing time-series endpoint. | Request a specific place and variable, display the returned point or area and provenance, and retain separate requested and simulated locations when the player uses a proxy. Keep the offline paths playable. |
| D · Downstream routing | Extra High | Routing evaluation, source provenance and test fixtures under `scripts/homecoming-validation/` and the routing documentation. | A reproducible pass or fail decision on named upstream and downstream gauges. A passing model delivers the response contract and fixtures for the Unity flight. |
| E · Integration and release playtest | Extra High | Shared navigation and cross-task integration after the feature owners finish, plus the judge walkthrough and standalone build. | A tested macOS preview that meets the four historical challenge objectives. Integrate the downstream flight after a passing routing handoff. |

A, B, C and D can begin in parallel. B uses the existing pinned records so it can work independently of the connected client. C owns the site picker used by the free lab, while A owns the guided weather controls and playback. E can run a baseline playtest in an isolated save while those chats work. Final story and archive integration follows A, B and C. D delivers a separate scientific decision. The validated route and its Unity integration can follow the historical release if it needs further work.

The release playthrough must cover story entry, variable and location selection, a long time series with units and dates, a numerical trend rate, and a significance result whose scope the player can identify. Also check the connected archive and its error states, the offline case, both Arizona turns, old saves, mouse and keyboard, and a clean macOS build.
