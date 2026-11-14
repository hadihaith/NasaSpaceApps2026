
export function detectDiscoveries(history) {
  const discoveries = [];

  if (!Array.isArray(history) || history.length < 3) {
    return discoveries;
  }

  // Check the latest 3 months for prolonged dryness.
  const recentMonths = history.slice(-3);

  const prolongedDryness = recentMonths.every(
    month => month.soilMoistureIndex < 0.3
  );

  if (prolongedDryness) {
    discoveries.push({
      id: "drought_stress",
      severity: "high"
    });
  }

  // Detect a substantial decline in vegetation health.
  const firstMonth = history[0];
  const lastMonth = history[history.length - 1];

  const vegetationDecline =
    firstMonth.vegetationHealthIndex -
    lastMonth.vegetationHealthIndex;

  if (vegetationDecline >= 0.3) {
    discoveries.push({
      id: "vegetation_decline",
      severity: vegetationDecline >= 0.6
        ? "high"
        : "moderate"
    });
  }

  return discoveries;
}

export function getNewDiscoveries(
  currentDiscoveries,
  previouslyUnlocked = []
) {
  const unlockedIds = new Set(previouslyUnlocked);

  return currentDiscoveries.filter(
    discovery => !unlockedIds.has(discovery.id)
  );
}
