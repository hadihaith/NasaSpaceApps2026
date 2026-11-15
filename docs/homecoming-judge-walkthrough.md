# The Great Homecoming · Judge walkthrough

## First contact

1. Open the macOS app. Watch the opening and briefings, enter a recruit name, then choose **Investigate the rain alert**.
2. **Cut the feed → Use precipitation → Use the Chile point** (36°S, 72°W). Read the dated chart and select **See the trend**. The local estimate fell by 18.84 mm/month per decade. **Choose a second point → Use the Sahel point** (14°N, 6°E). The local estimate there rose by 2.13 mm/month per decade.
3. **Compare records**. The chart has 35 annual values per site, 1991–2025, in mm/month. **Show the matched difference** plots the Sahel value minus the Chile value for each year. That difference rose by **22.84 mm/month per decade** in the reviewed fit. The trend in the difference was statistically significant. Significance for each local trend has not been established.
4. **See the evidence** shows the two-sided OLS slope test, `t(33) = 7.170`, `p = 3.23 × 10⁻⁸`, and the 95% slope interval from +16.36 to +29.33 mm/month per decade. The source is NASA POWER MERRA-2 precipitation (PRECTOTCORR) at the two archived points. Choose the claim about the difference and **Send to Earth**.

## Choose your own evidence

From Earth’s reply, open **Back to field station → Earth archive**. Pick **Water → Rain and snowfall → Choose a place → Browse offline records → Central Sahel**. Read the dated chart, then choose **Test the trend**. The local estimate is descriptive. **Compare another place → Central Chile** keeps the measurement, source, point coverage and annual time view fixed. Read the same-scale overlay and the separately plotted A-minus-B series. Both records and the exact reviewed comparison work offline.

For a connected example, choose **Heat → Air temperature** and enter latitude `-36` and longitude `-72` for a point request. The chart shows the returned POWER footprint and its dated values in °C. **See the evidence** shows the request and source. Choose an offline record if the request stalls or fails. Connected records are exploratory. Matched charts and a descriptive difference rate are available after both records load. Exploratory tests require a server with the comparison route.

## Change the weather

From Earth’s reply, choose **Take the Weather and soil case**. Open and save Central Arizona’s rainfall finding, then predict the soil response and open the weather lab.

1. Set precipitation to **+100%** and **Preview 12 months**. At year-end the model held **46.87 mm** of root-zone water with the edit and **23.44 mm** with saved weather. **Save test and return to case**.
2. Open the follow-up and set temperature to **+4 °C**. The saved rain and starting ground carry forward. The warmer track ended with **38.77 mm**, versus **46.87 mm** in the matched reference. Use the timeline to see months with little modeled change and the cascade to inspect when a difference returned.
3. Save the test. Record the missing historical match, choose **The edited run ended lower**, and read the mission report. **Inspect follow-up** opens the saved result. Its monthly log lets you jump back into the saved playback. **Discoveries** earned during experimentation unlock the final hearing.

Use **Tab** or **Shift+Tab** to move focus and **Return** to activate. Arrow keys adjust a focused dial. **Space** skips a descent or pauses playback.

The release uses the historical flight. In the Potomac research replay, monthly discharge error at Hancock was 35.99 m³/s versus 35.50 m³/s for the same-month inflow baseline. This missed the predeclared routing gate. [Routing validation](homecoming-routing-forecast.md)

## First-time playtest notes

- **Earth archive** now offers offline records from the place picker as well as from request recovery. With the connection deliberately stalled and then closed, the standalone player reached the pinned chart, verdict, and reviewed two-point comparison by keyboard.
- **Weather and soil** now returns to the prediction after saving the rainfall finding. Saving a run from the intelligence report returns to the case and reveals the next test. Both handoffs were checked in rendered QA builds.
- **Field station → Weather lab** showed all four controls while a guided soil case was in progress. The standalone harness also passed both Arizona advances and the animated hearing. Saved-replay and prior-campaign checks passed.

Keyboard focus and activation worked in the rendered macOS player. Desktop input automation reported mouse presses at a stationary game cursor, so physical mouse clicks and drags remain unverified.
