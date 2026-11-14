
import {
  simulateMonth,
  calculateRainfall,
  calculateEvapotranspiration,
  simulateMultipleMonths,
  updateVegetationHealth,
  runExperiment,
  createInitialState
} from "./src/simulation/engine.js";
import {
  detectDiscoveries,
  getNewDiscoveries
} from "./src/simulation/rules.js";
const result = simulateMonth({
  soilWater: 100,
  precipitation: 20,
  evapotranspiration: 50,
  drainage: 10,
  soilCapacity: 150
});

console.log("🌍 EARTH SIMULATION RESULT");
console.log("--------------------------");
console.log("Remaining soil water:", result.soilWaterMm, "mm");
console.log("Soil moisture index:", result.soilMoistureIndex);

console.log("\n🌧️ RAINFALL EXPERIMENT");
console.log("--------------------------");

const normalRainfall = 100;
const playerChange = -30;

const adjustedRainfall = calculateRainfall(
  normalRainfall,
  playerChange
);

console.log("Normal rainfall:", normalRainfall, "mm");
console.log("Player adjustment:", playerChange, "%");
console.log("Adjusted rainfall:", adjustedRainfall, "mm");

console.log("\n👽 ALIEN RAINFALL EXPERIMENT");
console.log("--------------------------");

const earthResult = simulateMonth({
  soilWater: 100,
  precipitation: adjustedRainfall,
  evapotranspiration: 50,
  drainage: 10,
  soilCapacity: 150
});

console.log("Rainfall adjustment:", playerChange, "%");
console.log("Adjusted rainfall:", adjustedRainfall, "mm");
console.log("Remaining soil water:", earthResult.soilWaterMm, "mm");
console.log("Soil moisture index:", earthResult.soilMoistureIndex);

console.log("\n🧪 COMPARING RAINFALL SCENARIOS");
console.log("--------------------------------");

const rainfallScenarios = [-80, -50, -30, 0, 30, 60];

for (const change of rainfallScenarios) {
  const rainfall = calculateRainfall(100, change);

  const result = simulateMonth({
    soilWater: 100,
    precipitation: rainfall,
    evapotranspiration: 50,
    drainage: 10,
    soilCapacity: 150
  });

  console.log(
    `Rainfall ${change > 0 ? "+" : ""}${change}%`,
    `→ Soil water: ${result.soilWaterMm} mm`,
    `→ Moisture: ${(result.soilMoistureIndex * 100).toFixed(1)}%`
  );
}

console.log("\n🌍 TEMPERATURE + RAINFALL EXPERIMENT");
console.log("-----------------------------------");

const climateScenarios = [
  { name: "Normal", temp: 0, rain: 0 },
  { name: "Hot and Dry", temp: 4, rain: -50 },
  { name: "Cool and Wet", temp: -2, rain: 50 },
  { name: "Hot and Wet", temp: 4, rain: 50 }
];

for (const scenario of climateScenarios) {
  const rainfall = calculateRainfall(
    100,
    scenario.rain
  );

  const evaporationDemand = calculateEvapotranspiration(
    50,
    scenario.temp
  );

  const result = simulateMonth({
    soilWater: 100,
    precipitation: rainfall,
    evapotranspiration: evaporationDemand,
    drainage: 10,
    soilCapacity: 150
  });

  console.log(`\n${scenario.name}`);
  console.log("Temperature change:", scenario.temp, "°C");
  console.log("Rainfall change:", scenario.rain, "%");
  console.log("Estimated drying demand:", evaporationDemand, "mm");
  console.log("Remaining soil water:", result.soilWaterMm, "mm");
  console.log(
    "Soil moisture:",
    (result.soilMoistureIndex * 100).toFixed(1) + "%"
  );
}

console.log("\n👽 12-MONTH DROUGHT EXPERIMENT");
console.log("--------------------------------");

const droughtHistory = simulateMultipleMonths({
  months: 12,
  soilWater: 100,
  normalRainfall: 100,
  rainfallChangePercent: -70,
  normalEvapotranspiration: 50,
  temperatureDeltaC: 4,
  drainage: 0,
  soilCapacity: 150
});

for (const month of droughtHistory) {
  console.log(
    `Month ${month.month}:`,
    `${month.soilWaterMm.toFixed(1)} mm`,
    `| Moisture: ${(month.soilMoistureIndex * 100).toFixed(1)}%`
  );
}

console.log("\n🌱 VEGETATION HEALTH EXPERIMENT");
console.log("--------------------------------");

const vegetationScenarios = [
  { name: "Healthy Soil", moisture: 0.7 },
  { name: "Dry Soil", moisture: 0.4 },
  { name: "Very Dry Soil", moisture: 0.1 }
];

for (const scenario of vegetationScenarios) {
  const startingHealth = 0.85;

  const newHealth = updateVegetationHealth(
    startingHealth,
    scenario.moisture
  );

  console.log(`\n${scenario.name}`);
  console.log("Soil moisture:", scenario.moisture);
  console.log("Starting vegetation health:", startingHealth);
  console.log("New vegetation health:", newHealth.toFixed(2));
}

console.log("\n🌍 SOIL + VEGETATION SIMULATION");
console.log("--------------------------------");

const ecosystemHistory = simulateMultipleMonths({
  months: 12,
  soilWater: 100,
  normalRainfall: 100,
  rainfallChangePercent: -70,
  normalEvapotranspiration: 50,
  temperatureDeltaC: 4,
  drainage: 0,
  soilCapacity: 150,
  startingVegetationHealth: 0.85
});

for (const month of ecosystemHistory) {
  console.log(
    `Month ${month.month}:`,
    `Soil: ${(month.soilMoistureIndex * 100).toFixed(1)}%`,
    `| Vegetation: ${(month.vegetationHealthIndex * 100).toFixed(1)}%`
  );
}

console.log("\n👽 EARTH DISCOVERY SYSTEM");
console.log("--------------------------");

const discoveries = detectDiscoveries(
  ecosystemHistory
);

if (discoveries.length > 0) {
  for (const discovery of discoveries) {
    console.log("🎉 NEW DISCOVERY!");
    console.log("Phenomenon:", discovery.id);
    console.log("Severity:", discovery.severity);
  }
} else {
  console.log("No discoveries detected.");
}

console.log("\n👽 DISCOVERY MEMORY TEST");
console.log("--------------------------");

// The player has already discovered drought stress.
const previouslyUnlocked = ["drought_stress"];

// Detect environmental phenomena.
const currentDiscoveries = detectDiscoveries(
  ecosystemHistory
);

// Only show discoveries that haven't been unlocked.
const newDiscoveries = getNewDiscoveries(
  currentDiscoveries,
  previouslyUnlocked
);

console.log("Previously unlocked:", previouslyUnlocked);

for (const discovery of newDiscoveries) {
  console.log("🎉 NEW DISCOVERY:", discovery.id);
}

console.log("\n🚀 COMPLETE ALIEN SIMULATOR TEST");
console.log("--------------------------------");

// Create a fresh Earth.
const initialState = createInitialState();

// Player chooses environmental adjustments.
const experiment = {
  regionId: "region_01",
  durationMonths: 12,
  controls: {
    temperatureDeltaC: 4,
    precipitationChangePct: -70,
    relativeHumidityDeltaPctPoints: -10,
    windSpeedChangePct: 15
  }
};

// Run the complete experiment.
const simulationResult = runExperiment(
  experiment,
  initialState
);

// Display the results.
console.log("Region:", simulationResult.regionId);
console.log("Months simulated:", simulationResult.elapsedMonths);

console.log(
  "Soil moisture:",
  (simulationResult.environment.soilMoistureIndex * 100)
    .toFixed(1) + "%"
);

console.log(
  "Vegetation health:",
  (simulationResult.environment.vegetationHealthIndex * 100)
    .toFixed(1) + "%"
);

console.log(
  "Discoveries:",
  simulationResult.discoveries.map(d => d.id)
);

console.log(
  "Saved simulation months:",
  simulationResult.nextState.elapsedMonths
);

console.log("\n👽 CONTINUE EXPERIMENT TEST");
console.log("------------------------------");

// Continue from the previous 12-month experiment.
const secondExperiment = {
  regionId: "region_01",
  durationMonths: 6,
  controls: {
    temperatureDeltaC: 0,
    precipitationChangePct: 80,
    relativeHumidityDeltaPctPoints: 0,
    windSpeedChangePct: 0
  }
};

// IMPORTANT: Reuse the previous simulation's nextState.
const continuedResult = runExperiment(
  secondExperiment,
  simulationResult.nextState
);

console.log(
  "Total months:",
  continuedResult.elapsedMonths
);

console.log(
  "Soil moisture:",
  (
    continuedResult.environment.soilMoistureIndex * 100
  ).toFixed(1) + "%"
);

console.log(
  "Vegetation health:",
  (
    continuedResult.environment.vegetationHealthIndex * 100
  ).toFixed(1) + "%"
);

console.log(
  "History entries:",
  continuedResult.nextState.history.length
);

console.log("\n🔄 RESET EARTH TEST");
console.log("------------------------------");

// Imagine these discoveries are saved by Person 1.
const unlockedDiscoveries = [
  "drought_stress",
  "vegetation_decline"
];

// Reset only the environmental simulation.
const resetState = createInitialState();

console.log("Elapsed months:", resetState.elapsedMonths);

console.log(
  "Soil water after reset:",
  resetState.soilWaterMm,
  "mm"
);

console.log(
  "Vegetation health after reset:",
  resetState.vegetationHealthIndex
);

console.log(
  "History entries after reset:",
  resetState.history.length
);

// Discoveries are deliberately kept separately.
console.log(
  "Previously unlocked discoveries:",
  unlockedDiscoveries
);

console.log("\n🌬️💧 HUMIDITY AND WIND CONTROL TEST");
console.log("--------------------------------");

// Test each control independently.
const atmosphereScenarios = [
  {
    name: "Normal Conditions",
    humidity: 0,
    wind: 0
  },
  {
    name: "Higher Humidity",
    humidity: 20,
    wind: 0
  },
  {
    name: "Lower Humidity",
    humidity: -20,
    wind: 0
  },
  {
    name: "Stronger Wind",
    humidity: 0,
    wind: 50
  }
];

for (const scenario of atmosphereScenarios) {
  const result = runExperiment({
    regionId: "region_01",
    durationMonths: 1,
    controls: {
      temperatureDeltaC: 0,
      precipitationChangePct: -50,
      relativeHumidityDeltaPctPoints: scenario.humidity,
      windSpeedChangePct: scenario.wind
    }
  });

  console.log(
    scenario.name + ":",
    (result.environment.soilMoistureIndex * 100)
      .toFixed(1) + "% soil moisture"
  );
}
