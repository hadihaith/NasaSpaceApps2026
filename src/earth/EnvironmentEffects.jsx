import React, { useMemo, useRef } from 'react';
import { useFrame } from '@react-three/fiber';
import { AdditiveBlending, BackSide, MathUtils, Vector3 } from 'three';

const radians = MathUtils.degToRad;

// Matches the UV convention of Three.js SphereGeometry for a world map centered on Greenwich.
export function regionDirection({ latitude, longitude }) {
  const lat = radians(latitude);
  const lon = radians(longitude);
  return new Vector3(Math.cos(lat) * Math.cos(lon), Math.sin(lat), -Math.cos(lat) * Math.sin(lon));
}

const vertexShader = /* glsl */ `
  varying vec2 mapUv;
  varying vec3 surface;
  void main() {
    mapUv = uv;
    surface = position;
    gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
  }
`;

const globeFragment = /* glsl */ `
  uniform sampler2D basemap;
  uniform vec3 regionCenter;
  uniform float regionEdge;
  uniform float regionCore;
  uniform float hasRegion;
  uniform vec3 environment;
  uniform float time;
  uniform bool overlay;
  varying vec2 mapUv;
  varying vec3 surface;

  void main() {
    vec3 direction = normalize(surface);
    vec3 map = texture2D(basemap, mapUv).rgb;
    vec3 view = normalize(cameraPosition - surface);
    float rim = pow(1.0 - max(0.0, dot(view, direction)), 3.1);
    float light = 0.49 + 0.58 * max(0.0, dot(direction, normalize(view + vec3(0.0, 0.72, 0.0))));
    vec3 color = map * light + vec3(0.006, 0.023, 0.044);

    vec2 grid = abs(fract(mapUv * vec2(24.0, 12.0) + 0.5) - 0.5);
    vec2 line = 1.0 - smoothstep(vec2(0.005), vec2(0.012) + fwidth(mapUv * vec2(24.0, 12.0)), grid);
    color += vec3(0.028, 0.105, 0.145) * max(line.x, line.y) * 0.34;
    color += rim * vec3(0.055, 0.55, 0.92) * 0.33;

    // Angular footprint of the selected region. Every state-dependent pixel lives inside it.
    float angle = dot(direction, regionCenter);
    float area = smoothstep(regionEdge, regionCore, angle) * hasRegion;
    float dry = clamp(0.72 * environment.z + 0.65 * max(0.0, 0.52 - environment.x), 0.0, 1.0);
    float wet = smoothstep(0.42, 1.0, environment.x);
    float healthy = environment.y * (1.0 - 0.82 * dry);
    vec3 condition = vec3(0.095, 0.32, 0.145);
    condition = mix(condition, vec3(0.19, 0.77, 0.24), healthy);
    condition = mix(condition, vec3(0.012, 0.55, 0.91), wet);
    condition = mix(condition, vec3(1.0, 0.24, 0.025), dry);

    // Animated telemetry strokes represent the supplied indices on the hologram.
    float sweep = pow(0.5 + 0.5 * sin(430.0 * angle - 1.75 * time), 6.0);
    float stipple = pow(0.5 + 0.5 * sin(285.0 * direction.x) * sin(285.0 * direction.y), 8.0);
    float textureLight = dot(map, vec3(0.2126, 0.7152, 0.0722));
    color = mix(color, condition * (0.52 + textureLight), area * (0.36 + 0.27 * max(dry, wet)));
    color += area * condition * (sweep * (0.11 + 0.18 * wet) + stipple * 0.34 * healthy);
    float border = smoothstep(regionEdge, regionEdge + 0.0018, angle)
      * (1.0 - smoothstep(regionEdge + 0.0018, regionEdge + 0.004, angle)) * hasRegion;
    color += (condition + vec3(0.16)) * border * 0.72;

    if (overlay) {
      vec3 highlighted = condition * (0.65 + 0.36 * sweep + 0.26 * stipple * healthy);
      gl_FragColor = vec4(highlighted + border * 0.22, clamp(area * 0.64 + border * 0.45, 0.0, 0.92));
    } else {
      gl_FragColor = vec4(color, 1.0);
    }
    #include <colorspace_fragment>
  }
`;

const atmosphereFragment = /* glsl */ `
  varying vec3 surface;
  void main() {
    vec3 view = normalize(cameraPosition - surface);
    float band = exp(-pow((dot(normalize(surface), view) + 0.28) / 0.21, 2.0));
    gl_FragColor = vec4(0.055, 0.55, 0.93, band * 0.26);
    #include <colorspace_fragment>
  }
`;

export function Atmosphere() {
  return (
    <mesh>
      <sphereGeometry args={[1.055, 48, 32]} />
      <shaderMaterial vertexShader={vertexShader} fragmentShader={atmosphereFragment}
        side={BackSide} transparent depthWrite={false} blending={AdditiveBlending} />
    </mesh>
  );
}

export function EnvironmentEffects({ basemap, region, values, reducedMotion, overlay = false }) {
  const material = useRef(null);
  const [moisture, vegetation, drought] = values;
  const target = useMemo(() => new Vector3(moisture, vegetation, drought), [moisture, vegetation, drought]);
  const uniforms = useMemo(() => ({
    basemap: { value: basemap },
    regionCenter: { value: new Vector3(1, 0, 0) },
    regionEdge: { value: 0.98 },
    regionCore: { value: 0.99 },
    hasRegion: { value: 0 },
    environment: { value: new Vector3(0.5, 0.5, 0.5) },
    time: { value: 0 },
    overlay: { value: overlay },
  }), [basemap, overlay]);
  const direction = useMemo(() => region ? regionDirection(region) : null, [region]);

  useFrame((_, delta) => {
    if (!material.current) return;
    const uniforms = material.current.uniforms;
    if (direction) {
      // Move the footprint immediately so the new state never spills onto the old region.
      uniforms.regionCenter.value.copy(direction);
      uniforms.regionEdge.value = Math.cos(radians(region.radius));
      uniforms.regionCore.value = Math.cos(radians(region.radius * 0.48));
    }
    uniforms.hasRegion.value = direction ? 1 : 0;
    const step = Math.min(delta, 0.05);
    uniforms.environment.value.lerp(target, reducedMotion ? 1 : 1 - Math.exp(-6 * step));
    uniforms.time.value += reducedMotion ? 0 : step;
  });

  return (
    <mesh onClick={(event) => event.stopPropagation()}>
      <sphereGeometry args={[overlay ? 1.006 : 1, 64, 40]} />
      <shaderMaterial ref={material} vertexShader={vertexShader} fragmentShader={globeFragment} uniforms={uniforms} transparent={overlay} depthWrite={!overlay} />
    </mesh>
  );
}
