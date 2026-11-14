import { createHash } from "node:crypto";
import { afterEach, describe, expect, it, vi } from "vitest";
import { VARIABLES_BY_ID } from "../catalog";
import { getMonthlySeries, PowerError } from "../power";
import { connectedHistory } from "./connected-history";

function pointBody() {
  return {
    geometry: { coordinates: [-179.98, 12.34, 30] },
    header: { fill_value: -999, api: { version: "v2.test" }, sources: ["MERRA2"], time_standard: "LST" },
    parameters: { PRECTOTCORR: { units: "mm/day" } },
    properties: { parameter: { PRECTOTCORR: {
      "199901": 1, "199902": 1, "199903": -999, "199913": 99, "200001": 2, "200002": 1,
    } } },
  };
}

describe("connected historical adapter", () => {
  afterEach(() => vi.unstubAllGlobals());

  it("reuses a source fetch, preserves its footprint and nulls, and converts precipitation with leap years", async () => {
    const payload = JSON.stringify(pointBody());
    const fetch = vi.fn().mockResolvedValue(new Response(payload));
    vi.stubGlobal("fetch", fetch);
    const args = ["PRECTOTCORR", 12.34, -179.98, 0, 1999, 2000] as const;
    const [record, same] = await Promise.all([getMonthlySeries(...args), getMonthlySeries(...args)]);
    expect(record).toBe(same);
    expect(record.monthly[1]).toBe(1);
    expect(fetch).toHaveBeenCalledTimes(1);
    const evidence = connectedHistory(record, VARIABLES_BY_ID.PRECTOTCORR, "month", 1);
    expect(evidence.footprint.point).toMatchObject({ latitude: 12.34, longitude: -179.98 });
    expect(evidence.dataset.rawSha256).toBe(createHash("sha256").update(payload).digest("hex"));
    expect(evidence.dataset.units).toBe("mm/month");
    expect(evidence.finding.points).toEqual([{ date: "1999-02", t: 1999, value: 28 }, { date: "2000-02", t: 2000, value: 29 }]);
    expect(evidence.observations[2]).toEqual({ date: "1999-03", value: null, hasValue: false });
    expect(evidence.finding.status).toBe("not-established");
    expect(evidence.finding.test).toBeNull();
  });

  it("retries a transient failure and drops malformed coverage from the cache", async () => {
    const fetch = vi.fn()
      .mockResolvedValueOnce(new Response("Unavailable", { status: 503 }))
      .mockResolvedValueOnce(Response.json({ features: [] }))
      .mockResolvedValueOnce(Response.json(pointBody()));
    vi.stubGlobal("fetch", fetch);
    const load = () => getMonthlySeries("PRECTOTCORR", 12.35, -179.98, 0, 1999, 2000);
    await expect(load()).rejects.toBeInstanceOf(PowerError);
    await expect(load()).resolves.toMatchObject({ cells: 1 });
    expect(fetch).toHaveBeenCalledTimes(3);
  });
});
