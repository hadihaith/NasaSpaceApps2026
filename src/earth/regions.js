export const EARTH_REGIONS = [
  { id: 'phoenix', name: 'Central Arizona', latitude: 33.5, longitude: -112, radius: 7 },
  { id: 'sahel', name: 'Central Sahel', latitude: 14, longitude: 6, radius: 10 },
  { id: 'amazon', name: 'Amazon Basin', latitude: -7, longitude: -61, radius: 12 },
  { id: 'mediterranean', name: 'Eastern Mediterranean', latitude: 39.7, longitude: 22.6, radius: 8 },
  { id: 'south-asia', name: 'South Asia', latitude: 24, longitude: 79, radius: 10 },
  { id: 'australia', name: 'Eastern Australia', latitude: -27, longitude: 145, radius: 10 },
];

export const EARTH_TEXTURE_URL = '/assets/earth/blue-marble.webp';

export const ENVIRONMENT_FIELDS = [
  { key: 'soilMoistureIndex', label: 'Soil moisture', color: 'var(--earth-wet)' },
  { key: 'vegetationHealthIndex', label: 'Vegetation health', color: 'var(--earth-healthy)' },
  { key: 'droughtStressIndex', label: 'Drought stress', color: 'var(--earth-dry)' },
];

export function normalizeEnvironment(environment) {
  return ENVIRONMENT_FIELDS.map(({ key }) => {
    const value = environment?.[key];
    return Number.isFinite(value) ? Math.min(1, Math.max(0, value)) : 0.5;
  });
}

export function toCoordinates({ latitude, longitude }) {
  const format = (value, positive, negative) => `${Math.abs(value)}° ${value < 0 ? negative : positive}`;
  return `${format(latitude, 'N', 'S')} / ${format(longitude, 'E', 'W')}`;
}
