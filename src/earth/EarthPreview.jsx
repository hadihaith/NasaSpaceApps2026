import React, { useState } from 'react';
import { EarthScene } from './EarthScene';
import { EARTH_REGIONS, ENVIRONMENT_FIELDS } from './regions';
import './earth-preview.css';

const presets = {
  dry: { soilMoistureIndex: 0.18, vegetationHealthIndex: 0.27, droughtStressIndex: 0.91 },
  healthy: { soilMoistureIndex: 0.52, vegetationHealthIndex: 0.84, droughtStressIndex: 0.08 },
  wet: { soilMoistureIndex: 0.97, vegetationHealthIndex: 0.68, droughtStressIndex: 0.04 },
};

export function EarthPreview() {
  const [regionId, setRegionId] = useState('sahel');
  const [environment, setEnvironment] = useState(presets.dry);
  const region = EARTH_REGIONS.find((item) => item.id === regionId);

  return (
    <main className="earth-preview">
      <header className="earth-preview-header">
        <div>
          <p className="earth-eyebrow">Operation: The Great Homecoming</p>
          <h1>Planetary Lab<span>.</span></h1>
        </div>
      </header>
      <div className="earth-preview-layout">
        <EarthScene regionId={regionId} environment={environment} onRegionSelect={setRegionId} />
        <aside className="earth-console" aria-labelledby="earth-controls-title">
          <div>
            <h2 id="earth-controls-title">Experiment controls</h2>
            <p className="earth-region-readout">Selected region <strong>{region.name}</strong></p>
          </div>
          <div className="earth-presets" role="group" aria-label="Example environmental states">
            {Object.entries(presets).map(([name, values]) => (
              <button key={name} type="button" aria-pressed={environment === values}
                className={`earth-preset earth-preset-${name}`} onClick={() => setEnvironment(values)}>{name}</button>
            ))}
          </div>
          <div className="earth-sliders">
            {ENVIRONMENT_FIELDS.map(({ key, label, color }) => (
              <div key={key} className="earth-slider" style={{ '--input-color': color, '--value': `${environment[key] * 100}%` }}>
                <div className="earth-slider-label">
                  <label htmlFor={`earth-preview-${key}`}>{label}</label><output aria-hidden="true">{environment[key].toFixed(2)}</output>
                </div>
                <input id={`earth-preview-${key}`} type="range" min="0" max="1" step="0.01" value={environment[key]}
                  onChange={(event) => {
                    const value = event.currentTarget.valueAsNumber;
                    setEnvironment((current) => ({ ...current, [key]: value }));
                  }} />
              </div>
            ))}
          </div>
          <div className="earth-console-note">
            <strong>Simulated regional overlay</strong>
            <p>Sliders provide mock values from 0 to 1. The color and patterns illustrate those values inside the highlighted area.</p>
          </div>
        </aside>
      </div>
    </main>
  );
}
