
import { detectDiscoveries } from "./rules.js";

// 🌍 Calculate one month of soil water changes
export function simulateMonth({
  soilWater,
  precipitation,
  evapotranspiration,
  drainage,
  soilCapacity
}) {
  const newSoilWater =
    soilWater +
    precipitation -
    evapotranspiration -
    drainage;

  const finalSoilWater = Math.max(
    0,
    Math.min(newSoilWater, soilCapacity)
  );

  return {
    soilWaterMm: finalSoilWater,
    soilMoistureIndex: finalSoilWater / soilCapacity
  };
}


// 🌧️ Calculate rainfall based on player adjustment
export function calculateRainfall(
  normalRainfall,
  rainfallChangePercent
) {
  const adjustedRainfall =
    normalRainfall * (1 + rainfallChangePercent / 100);

  return Math.max(0, adjustedRainfall);
}


// 🔥💧🌬️ Calculate drying demand from temperature,
// humidity, and wind
export function calculateEvapotranspiration(
  normalEvapotranspiration,
  temperatureDeltaC,
  relativeHumidityDeltaPctPoints = 0,
  windSpeedChangePct = 0
) {
  // Provisional gameplay approximation.
  // These coefficients are NOT scientifically calibrated.

  if (
    ![
      normalEvapotranspiration,
      temperatureDeltaC,
      relativeHumidityDeltaPctPoints,
      windSpeedChangePct
    ].every(Number.isFinite) ||
    normalEvapotranspiration < 0
  ) {
    throw new Error("Invalid drying-demand inputs.");
  }

  // Higher temperature increases drying demand.
  const temperatureFactor =
    1 + temperatureDeltaC * 0.05;

  // Higher humidity reduces drying demand.
  const humidityFactor =
    1 - relativeHumidityDeltaPctPoints * 0.005;

  // Higher wind increases drying demand.
  const windFactor =
    1 + windSpeedChangePct * 0.002;

  const combinedFactor = Math.max(
    0,
    temperatureFactor * humidityFactor * windFactor
  );

  return normalEvapotranspiration * combinedFactor;
}


// 🌱 Update vegetation health based on soil moisture
export function updateVegetationHealth(
  currentHealth,
  soilMoistureIndex
) {
  if (
    !Number.isFinite(currentHealth) ||
    !Number.isFinite(soilMoistureIndex) ||
    currentHealth < 0 ||
    currentHealth > 1 ||
    soilMoistureIndex < 0 ||
    soilMoistureIndex > 1
  ) {
    throw new Error(
      "Health and moisture must be between 0 and 1."
    );
  }

  let healthChange = 0;

  // Temporary gameplay response rules.
  if (soilMoistureIndex < 0.3) {
    healthChange = -0.08;
  } else if (soilMoistureIndex < 0.5) {
    healthChange = -0.03;
  } else if (soilMoistureIndex >= 0.6) {
    healthChange = 0.02;
  }

  const newHealth = currentHealth + healthChange;

  return Math.max(
    0,
    Math.min(1, newHealth)
  );
}


// 🌍 Simulate multiple months with soil and vegetation
export function simulateMultipleMonths({
  months,
  soilWater,
  normalRainfall,
  rainfallChangePercent,
  normalEvapotranspiration,
  temperatureDeltaC,
  relativeHumidityDeltaPctPoints = 0,
  windSpeedChangePct = 0,
  drainage,
  soilCapacity,
  startingVegetationHealth = 0.85
}) {
  if (
    !Number.isInteger(months) ||
    months < 1 ||
    months > 120
  ) {
    throw new Error(
      "Months must be between 1 and 120."
    );
  }

  let currentSoilWater = soilWater;
  let vegetationHealth = startingVegetationHealth;

  const history = [];

  for (let month = 1; month <= months; month++) {

    // Step 1: Calculate adjusted rainfall.
    const rainfall = calculateRainfall(
      normalRainfall,
      rainfallChangePercent
    );

    // Step 2: Calculate drying demand using
    // temperature, humidity, and wind.
    const dryingDemand = calculateEvapotranspiration(
      normalEvapotranspiration,
      temperatureDeltaC,
      relativeHumidityDeltaPctPoints,
      windSpeedChangePct
    );

    // Step 3: Calculate available water.
    const availableWater =
      currentSoilWater + rainfall;

    // Actual water loss cannot exceed available water.
    const actualWaterLoss = Math.min(
      dryingDemand,
      availableWater
    );

    // Drainage cannot exceed remaining water.
    const actualDrainage = Math.min(
      drainage,
      Math.max(0, availableWater - actualWaterLoss)
    );

    // Step 4: Update soil moisture.
    const result = simulateMonth({
      soilWater: currentSoilWater,
      precipitation: rainfall,
      evapotranspiration: actualWaterLoss,
      drainage: actualDrainage,
      soilCapacity
    });

    currentSoilWater = result.soilWaterMm;

    // Step 5: Update vegetation health.
    vegetationHealth = updateVegetationHealth(
      vegetationHealth,
      result.soilMoistureIndex
    );

    // Step 6: Save this month's results.
    history.push({
      month,
      soilWaterMm: result.soilWaterMm,
      soilMoistureIndex: result.soilMoistureIndex,
      vegetationHealthIndex: vegetationHealth
    });
  }

  return history;
}


// 🌍 Temporary baseline for testing.
// Person 3 will provide region-specific data later.

const DEMO_BASELINE = {
  normalRainfall: 100,
  normalEvapotranspiration: 50,
  normalRelativeHumidity: 60,
  drainage: 0,
  soilCapacity: 150,
  initialSoilWater: 100,
  initialVegetationHealth: 0.85
};



// 🔄 Create or reset an Earth environment
export function createInitialState(
  baseline = DEMO_BASELINE
) {
  return {
    elapsedMonths: 0,
    soilWaterMm: baseline.initialSoilWater,
    vegetationHealthIndex:
      baseline.initialVegetationHealth,
    history: []
  };
}


// 🚀 Main function called by the game's Run Experiment button
export function runExperiment(
  experiment,
  previousState = null,
  baseline = DEMO_BASELINE
) {
  const {
    regionId,
    durationMonths,
    controls
  } = experiment;

  if (!regionId || !controls) {
    throw new Error("Missing region or controls.");
  }

  if (
    !Number.isInteger(durationMonths) ||
    durationMonths < 1 ||
    durationMonths > 120
  ) {
    throw new Error("Duration must be 1–120 months.");
  }

  const temperature =
    controls.temperatureDeltaC;

  const rainfallChange =
    controls.precipitationChangePct;

  const humidityChange =
    controls.relativeHumidityDeltaPctPoints ?? 0;

  const windChange =
    controls.windSpeedChangePct ?? 0;

  if (
    ![
      temperature,
      rainfallChange,
      humidityChange,
      windChange
    ].every(Number.isFinite) ||
    rainfallChange < -100
  ) {
    throw new Error("Invalid climate controls.");
  }

  // Continue the previous Earth or start fresh.
  const state =
    previousState ?? createInitialState(baseline);

  // Simulate the selected number of months.
  const monthlyHistory = simulateMultipleMonths({
    months: durationMonths,
    soilWater: state.soilWaterMm,
    normalRainfall: baseline.normalRainfall,
    rainfallChangePercent: rainfallChange,
    normalEvapotranspiration:
      baseline.normalEvapotranspiration,
    temperatureDeltaC: temperature,
    relativeHumidityDeltaPctPoints: humidityChange,
    windSpeedChangePct: windChange,
    drainage: baseline.drainage,
    soilCapacity: baseline.soilCapacity,
    startingVegetationHealth:
      state.vegetationHealthIndex
  });

  // Combine previous and new history.
  const combinedHistory = [
    ...(state.history ?? []),
    ...monthlyHistory.map(month => ({
      ...month,
      month: state.elapsedMonths + month.month
    }))
  ];

  const finalMonth = combinedHistory.at(-1);

  // Current environment.
  const environment = {
    soilMoistureIndex:
      finalMonth.soilMoistureIndex,
    vegetationHealthIndex:
      finalMonth.vegetationHealthIndex,
    droughtStressIndex:
      1 - finalMonth.soilMoistureIndex
  };

  // Discover environmental phenomena.
  const discoveries =
    detectDiscoveries(combinedHistory);

  // Save everything needed to continue.
  const nextState = {
    elapsedMonths:
      state.elapsedMonths + durationMonths,
    soilWaterMm:
      finalMonth.soilWaterMm,
    vegetationHealthIndex:
      finalMonth.vegetationHealthIndex,
    history: combinedHistory
  };

  return {
    regionId,
    elapsedMonths: nextState.elapsedMonths,
    environment,
    discoveries,
    nextState
  };
}
