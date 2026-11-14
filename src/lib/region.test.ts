import { describe, expect, it } from "vitest";
import { regionBBox, wrapLon } from "./region";

describe("regionBBox", () => {
  it("is null for a single point", () => {
    expect(regionBBox(10, 10, 0)).toBeNull();
  });

  it("never exceeds the requested width, even at awkward coordinates", () => {
    // 15.6 - 5 and 15.6 + 5 differ by 10.000000000000002 in floating point.
    const b = regionBBox(78.2, 15.6, 10)!;
    expect(b.lonMax - b.lonMin).toBe(10);
    expect(b.latMax - b.latMin).toBe(10);
  });

  it("slides inward at the poles and the antimeridian", () => {
    expect(regionBBox(89, 179, 10)).toEqual({ latMin: 80, latMax: 90, lonMin: 170, lonMax: 180 });
    expect(regionBBox(-89, -179, 5)).toEqual({ latMin: -90, latMax: -85, lonMin: -180, lonMax: -175 });
  });
});

describe("wrapLon", () => {
  it("wraps into [-180, 180)", () => {
    expect(wrapLon(190)).toBe(-170);
    expect(wrapLon(-190)).toBe(170);
    expect(wrapLon(45)).toBe(45);
  });
});
