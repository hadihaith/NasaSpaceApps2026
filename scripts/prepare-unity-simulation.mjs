import { createHash } from "node:crypto";
import { readFile, writeFile, rename, rm } from "node:fs/promises";
import { fileURLToPath } from "node:url";
import { createServer } from "vite";

const root = fileURLToPath(new URL("../", import.meta.url));
const pathFromRoot = (path) => new URL(path, new URL("../", import.meta.url));
const read = async (path) => JSON.parse(await readFile(pathFromRoot(path), "utf8"));
const sha256 = (input) => createHash("sha256").update(input).digest("hex");
const bounds = (box) => box ? [box.lonMin, box.latMin, box.lonMax, box.latMax] : null;
const output = pathFromRoot("unity/Homecoming/Assets/FieldSimulation/Data/Climate.json");

const server = await createServer({
  root, configFile: false, server: { middlewareMode: true, ws: false }, appType: "custom",
});
try {
  const [
    { EARTH_REGIONS }, { CONTROLS, DISCOVERIES, REQUIRED_DISCOVERIES, toNormals }, { parseArchive },
    { HISTORICAL_SCHEMA_VERSION, analyzeHistoricalRecord, analyzeHistoricalComparison },
    { CHILE_RAIN_SOURCE, chileRainDataset, GUIDED_RAIN, PHOENIX_RAIN },
  ] = await Promise.all([
    "earth/regions.js", "homecoming/model.ts", "lib/earth/member3.ts",
    "lib/earth/historical.ts", "lib/earth/curated-history.ts",
  ].map((path) => server.ssrLoadModule("/src/" + path)));
  const [climate, archiveFile, index, reviews, chileRaw] = await Promise.all([
    read("public/homecoming/regional-climate.json"), read("public/data/earth-archive.json"),
    read("docs/member-3/dataset-index.json"), read("data/reviews/homecoming-trends.json"),
    readFile(pathFromRoot(CHILE_RAIN_SOURCE.path)),
  ]);
  const archive = parseArchive(archiveFile);
  const listed = new Set(index.datasets.filter(({ inArchive }) => inArchive).map(({ id }) => id));
  if (archive.length !== listed.size || archive.some(({ id }) => !listed.has(id))) {
    throw new Error("The public archive and dataset index disagree.");
  }
  const chile = chileRainDataset(JSON.parse(chileRaw), sha256(chileRaw),
    archive.find((d) => d.seriesId === "sahel.merra2.precipitation"));
  const datasets = [...archive.filter((dataset) => dataset.gameVariable), chile];
  const rawFiles = [...new Set(datasets.map(({ source }) => source.rawFile))];
  const rawSources = new Map(await Promise.all(rawFiles.map(async (path) => {
    const raw = await readFile(pathFromRoot(path));
    return [path, { sha: sha256(raw), json: path.endsWith(".json") ? JSON.parse(raw) : null }];
  })));
  const inputs = new Map(datasets.map((dataset) => {
    const raw = rawSources.get(dataset.source.rawFile);
    if (raw.sha !== dataset.rawSha256) throw new Error("Raw source checksum mismatch for " + dataset.id);
    if (dataset.spatial.type === "grid-point" &&
        JSON.stringify(raw.json.geometry.coordinates) !== JSON.stringify(dataset.spatial.returnedCoordinates)) {
      throw new Error("POWER returned coordinates disagree with the archive for " + dataset.id);
    }
    return [dataset.seriesId, dataset];
  }));
  const evidence = Array.from(inputs.values(), (dataset) => {
    const { start, end } = dataset.temporal;
    const window = { startYear: Number(start.slice(0, 4)), endYear: Number(end.slice(0, 4)) };
    const finding = analyzeHistoricalRecord(dataset, window, "annual", 0, reviews);
    const { spatial, provenance } = dataset;
    const cells = (spatial.gridCellCenters ?? []).map(([longitude, latitude]) => ({
      latitude, longitude, weight: Math.cos(latitude * Math.PI / 180),
    }));
    const point = spatial.returnedCoordinates
      ? { longitude: spatial.returnedCoordinates[0], latitude: spatial.returnedCoordinates[1],
        elevation: spatial.returnedCoordinates[2] } : null;
    return {
      id: dataset.id, region: dataset.gameRegionId, key: dataset.gameVariable,
      seriesId: dataset.seriesId, regionLabel: dataset.region,
      variable: dataset.variable, unit: dataset.units, provider: dataset.providerKind,
      product: provenance.product, version: provenance.version, url: provenance.sourceUrl,
      aggregation: dataset.annualInterpretation,
      years: finding.points.map(({ t, value }) => ({ year: t, value })),
      slope: finding.hasSlope ? finding.slopePerDecade / 10 : null, hasSlope: finding.hasSlope,
      sourceParameter: dataset.source.parameter, definition: dataset.definition,
      fingerprint: finding.fingerprint, rawSha256: dataset.rawSha256,
      footprint: { type: spatial.type, point, gridCells: spatial.gridCells ?? null, cells,
        bbox: bounds(spatial.bbox), dataBbox: bounds(spatial.dataBbox),
        nativeResolution: spatial.nativeResolution ?? null, note: spatial.footprintNote ?? spatial.method ?? null },
      provenance: {
        ...provenance,
        limitations: provenance.limitations.filter((note) =>
          note !== "No dataset-specific statistical review has been supplied. Significance is not established."),
        source: dataset.source,
      },
      coverageStart: start, coverageEnd: end, timeStandard: dataset.temporal.timeStandard ?? null, quality: dataset.quality,
      observations: dataset.observations.map(({ date, value }) => ({ date, value, hasValue: value !== null })),
      finding,
      monthFindings: Array.from({ length: 12 }, (_, month) =>
        analyzeHistoricalRecord(dataset, window, "month", month, reviews)),
      anomalyFinding: analyzeHistoricalRecord(dataset, window, "anomaly", 0, reviews),
    };
  });
  const comparisons = [
    analyzeHistoricalComparison(GUIDED_RAIN, inputs.get("sahel.merra2.precipitation"),
      inputs.get("central-chile.merra2.precipitation"), reviews),
    analyzeHistoricalComparison(PHOENIX_RAIN, inputs.get("region_01.merra2.precipitation"),
      inputs.get("region_01.imerg.precipitation")),
  ];
  const usedReviews = new Set([
    ...evidence.flatMap((record) => [record.finding, ...record.monthFindings, record.anomalyFinding]),
    ...comparisons.flatMap(({ left, right, difference }) => [left, right, difference]),
  ].filter(({ review }) => review).map(({ id }) => id));
  if (Object.keys(reviews).some((id) => !usedReviews.has(id))) {
    throw new Error("A historical review no longer matches the exact pinned dataset or analysis.");
  }
  const data = {
    schemaVersion: HISTORICAL_SCHEMA_VERSION,
    requiredDiscoveries: REQUIRED_DISCOVERIES,
    controls: CONTROLS,
    discoveries: DISCOVERIES,
    regions: EARTH_REGIONS.map((region) => ({ ...region, normals: toNormals(climate[region.id]) })),
    evidence,
    comparisons,
  };
  const serialized = JSON.stringify(data) + "\n";
  if (process.argv.includes("--check")) {
    if (serialized !== await readFile(output, "utf8")) throw new Error("Climate.json is stale. Rerun the exporter.");
  } else {
    const temporary = new URL(`Climate.json.${process.pid}.tmp`, output);
    try {
      await writeFile(temporary, serialized);
      await rename(temporary, output);
    } finally {
      await rm(temporary, { force: true });
    }
  }
  console.log(`${process.argv.includes("--check") ? "Verified" : "Exported"} ${data.regions.length} climate normals, ${evidence.length} evidence records and ${comparisons.length} comparisons.`);
} finally {
  await server.close();
}
