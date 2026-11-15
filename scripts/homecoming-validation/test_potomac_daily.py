"""Check daily water accounting, date aggregation and response against known injections."""

import unittest

import numpy as np

import potomac_daily as daily
import routing


class PotomacDailyTest(unittest.TestCase):
    def test_snow_waits_for_melt_and_the_soil_store_accounts_for_water(self):
        weather = [(10, -1, 0), (0, 0, 0), (0, 4, 0), (10, 4, 2)]
        out, evap, storage = daily.runoff(weather, (100, 1, 1, 0.5, 1), 1)
        np.testing.assert_allclose(out, [0, 0, 0, 515], rtol=0, atol=1e-10)
        self.assertAlmostEqual(evap, (2 * 19 / 70) * 1000)
        self.assertAlmostEqual(out.sum() + evap + storage, 20_000)

    def test_two_reaches_delay_and_spread_pulse_with_no_volume_loss(self):
        original = np.zeros(6)
        out, storage = daily.run_network(
            [np.array([80.0, 0, 0, 0, 0, 0]), original, original],
            [(0, 0), (1, 0.5), (2, 0.5)],
        )
        np.testing.assert_array_equal(out[1], [0, 40, 20, 10, 5, 2.5])
        np.testing.assert_array_equal(out[2], [0, 0, 0, 20, 20, 15])
        self.assertEqual(storage, [0, 2.5, 22.5])
        self.assertEqual(out[2].sum() + sum(storage), 80.0)

    def test_volume_still_in_transit_when_lag_exceeds_window(self):
        out, storage = daily.route(np.array([80.0]), (3, 0.25))
        np.testing.assert_array_equal(out, [0])
        self.assertEqual(storage, 80.0)

    def test_same_day_bypass_preserves_monthly_input_even_at_month_boundary(self):
        upstream_m3 = np.zeros(len(daily.DATES))
        upstream_m3[59] = routing.SECONDS_PER_DAY
        bypass, stored = daily.route(upstream_m3, daily.BYPASS)
        delayed, _ = daily.route(upstream_m3, (1, 0))
        self.assertEqual(daily.DATES[59].isoformat(), "2000-02-29")
        self.assertEqual(stored, 0)
        self.assertAlmostEqual(daily.monthly_mean(bypass)[1], 1 / 29)
        self.assertEqual(daily.monthly_mean(bypass)[2], 0)
        self.assertEqual(daily.monthly_mean(delayed)[1], 0)
        self.assertAlmostEqual(daily.monthly_mean(delayed)[2], 1 / 31)

    def test_case_pulse_cancels_unchanged_lateral_runoff(self):
        releases = [np.full(len(daily.DATES), 1000.0 * i) for i in (1, 2, 3)]
        effect = daily.pulse_check(releases, [(0, 0), (1, 0), (2, 0.5)])
        local, middle, outlet = [effect["stations"][site] for site in routing.STATIONS]
        self.assertEqual(local["firstResponseDay"], "2019-08-31")
        self.assertEqual(middle["firstResponseDay"], "2019-09-01")
        self.assertEqual(outlet["firstResponseDay"], "2019-09-03")
        self.assertAlmostEqual(outlet["peakIncreaseM3s"], 5.0)
        self.assertAlmostEqual(outlet["increasePastGaugeByYearEndM3"], daily.PULSE_M3)
        self.assertTrue(all(row["deltaM3s"] == 0 for row in outlet["months"][:8]))
        self.assertAlmostEqual(outlet["months"][8]["deltaM3s"], 10 / 30)
        self.assertAlmostEqual(effect["networkResidualM3"], 0.0, places=6)


if __name__ == "__main__":
    unittest.main()
