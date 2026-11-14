import React from 'react';
import { EARTH_REGIONS, toCoordinates } from './regions';

export function RegionSelector({ regionId, onRegionSelect }) {
  return (
    <div className="earth-regions" role="group" aria-label="Simulation region">
      {EARTH_REGIONS.map((region) => (
        <button key={region.id} type="button" aria-pressed={regionId === region.id}
          onClick={() => onRegionSelect(region.id)}>
          <span className="earth-region-name">{region.name}</span>
          <span className="earth-region-coordinates">{toCoordinates(region)}</span>
          <span className="earth-region-light" aria-hidden="true" />
        </button>
      ))}
    </div>
  );
}
