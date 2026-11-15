import { describe, expect, it } from "vitest";
import { POST } from "./route";

function request(body: unknown) {
  return new Request("http://localhost/api/timeseries/compare", { method: "POST", body: JSON.stringify(body) });
}

describe("archive comparison", () => {
  it("uses common dates for both slopes and the exploratory A-minus-B fit", async () => {
    const left = Array.from({ length: 21 }, (_, i) => ({ date: String(1999+i), value: 3*i + Math.sin(i) }));
    const right = Array.from({ length: 20 }, (_, i) => ({ date: String(2000+i), value: i + Math.cos(i) }));
    const response = await POST(request({ mode: "annual", leftId: "a", rightId: "b", left, right }));
    expect(response.status).toBe(200);
    const result = await response.json();
    expect(result).toMatchObject({ schemaVersion: 1, n: 20, firstDate: "2000", lastDate: "2019" });
    expect(result.left.ratePerDecade).toBeCloseTo(30, 0);
    expect(result.right.ratePerDecade).toBeCloseTo(10, 0);
    expect(result.difference.ratePerDecade).toBeCloseTo(20, 0);
    expect(result.difference.outcome).toBe("both");
    expect(result.difference.review).toBeUndefined();
  });

  it("validates the calendar month and reports short overlaps without a test", async () => {
    const body = { mode: "month", month: 5, leftId: "a", rightId: "b",
      left: [{ date: "2003-05", value: 10 }, { date: "2004-05", value: 12 }],
      right: [{ date: "2004-05", value: 5 }] };
    expect((await POST(request({ ...body, month: 6 }))).status).toBe(400);
    expect((await POST(request({ ...body, left: [...body.left, body.left[1]] }))).status).toBe(400);
    expect(await (await POST(request(body))).json()).toMatchObject({ n: 1, firstDate: "2004-05", difference: null });
  });
});
