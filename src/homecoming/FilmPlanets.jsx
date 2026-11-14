'use client';

import { useMemo, useRef } from 'react';
import { useFrame, useLoader } from '@react-three/fiber';
import { AdditiveBlending, BackSide, Color, MathUtils, SRGBColorSpace, TextureLoader } from 'three';

const MOON_MAPS = ['/homecoming/moon-albedo.jpg', '/homecoming/moon-height.jpg'];
const KU_MAPS = ['ku-day', 'ku-night', 'ku-water', 'ku-clouds'].map((map) => `/homecoming/planets/${map}.webp`);

class PlanetTextureLoader extends TextureLoader {
  load(url, onLoad, onProgress, onError) {
    return super.load(url, (texture) => {
      if (url === MOON_MAPS[0] || url === KU_MAPS[0] || url === KU_MAPS[1]) {
        texture.colorSpace = SRGBColorSpace;
      }
      texture.anisotropy = 4;
      onLoad(texture);
    }, onProgress, onError);
  }
}

const surfaceVertex = /* glsl */ `
  varying vec2 mapUv;
  varying vec3 viewNormal;
  varying vec3 viewPoint;
  void main() {
    mapUv = uv;
    viewNormal = normalize(normalMatrix * normal);
    vec4 point = modelViewMatrix * vec4(position, 1.0);
    viewPoint = point.xyz;
    gl_Position = projectionMatrix * point;
  }
`;

const relief = /* glsl */ `
  vec3 reliefNormal(vec3 n, vec3 point, float elevation, float strength) {
    vec3 x = dFdx(point);
    vec3 y = dFdy(point);
    vec3 gradientX = cross(y, n);
    vec3 gradientY = cross(n, x);
    float determinant = dot(x, gradientX);
    vec3 gradient = sign(determinant) * (dFdx(elevation) * gradientX + dFdy(elevation) * gradientY);
    return normalize(abs(determinant) * n - strength * gradient);
  }
`;

const moonFragment = /* glsl */ `
  uniform sampler2D albedoMap;
  uniform sampler2D heightMap;
  varying vec2 mapUv;
  varying vec3 viewNormal;
  varying vec3 viewPoint;
  ${relief}
  void main() {
    vec3 normal = normalize(viewNormal);
    vec3 sun = normalize(vec3(-0.86, 0.44, 0.42));
    float elevation = texture2D(heightMap, mapUv).r;
    vec3 bumps = reliefNormal(normal, viewPoint, elevation, 0.047);
    float terminator = smoothstep(-0.065, 0.18, dot(normal, sun));
    float direct = min(max(dot(bumps, sun), 0.0), 1.0);
    vec3 albedo = texture2D(albedoMap, mapUv).rgb;
    vec3 light = vec3(1.26, 1.22, 1.14);
    vec3 shadow = vec3(0.075, 0.10, 0.18);
    gl_FragColor = vec4(albedo * (shadow + light * direct * terminator), 1.0);
    #include <tonemapping_fragment>
    #include <colorspace_fragment>
  }
`;

// The KU Exchange globe's relief, water mask, ocean glint and city-light halo are
// evaluated directly in the film canvas so the planet shares its camera and depth.
const earthFragment = /* glsl */ `
  uniform sampler2D dayMap;
  uniform sampler2D nightMap;
  uniform sampler2D waterMap;
  uniform float phase;
  uniform float hologram;
  varying vec2 mapUv;
  varying vec3 viewNormal;
  varying vec3 viewPoint;
  ${relief}
  float cityLuminance(vec3 sampleColor) {
    return dot(sampleColor, vec3(0.299, 0.587, 0.114)) - max(sampleColor.b - sampleColor.r, 0.0) * 0.9;
  }
  void main() {
    vec3 normal = normalize(viewNormal);
    vec3 view = normalize(-viewPoint);
    vec3 sun = normalize(mix(vec3(-0.90, 0.30, 0.56), vec3(-0.2, 0.44, 0.94), hologram));
    float dayAmount = smoothstep(-0.14, 0.28, dot(normal, sun));
    vec3 albedo = texture2D(dayMap, mapUv).rgb;
    float water = texture2D(waterMap, mapUv).r;
    float elevation = dot(albedo, vec3(0.299, 0.587, 0.114));
    vec3 terrain = reliefNormal(normal, viewPoint, elevation, 0.025);
    float light = max(dot(mix(terrain, normal, water), sun), 0.0);
    vec3 ocean = albedo * 0.87 + vec3(0.004, 0.018, 0.038);
    vec3 day = mix(albedo, ocean, water) * (0.45 + light * 1.55);
    vec3 nightSource = texture2D(nightMap, mapUv).rgb;
    float cities = smoothstep(0.001, 0.12, cityLuminance(nightSource));
    float halo = smoothstep(0.0002, 0.035, cityLuminance(texture2D(nightMap, mapUv, 3.5).rgb));
    vec3 night = vec3(0.003, 0.008, 0.019) + nightSource * 0.16;
    night += vec3(0.84, 0.39, 0.09) * (cities * 0.8 + halo * 0.18);
    float glint = pow(max(dot(normal, normalize(sun + view)), 0.0), 100.0) * water * 0.24;
    float atmosphere = pow(1.0 - max(dot(normal, view), 0.0), 3.55);
    vec3 color = mix(night, day, dayAmount) + glint * vec3(1.0, 0.95, 0.78);
    color += vec3(0.045, 0.21, 0.48) * atmosphere * (0.22 + dayAmount * 0.60);

    vec2 grid = abs(fract(mapUv * vec2(24.0, 12.0) + 0.5) - 0.5);
    vec2 lines = 1.0 - smoothstep(vec2(0.008), vec2(0.022) + fwidth(mapUv) * vec2(24.0, 12.0), grid);
    float beam = exp(-abs(mapUv.y - mod(phase * 0.38, 1.4) + 0.2) * 33.0);
    vec3 phosphor = vec3(0.04, 0.72, 1.0);
    vec3 projected = vec3(0.008, 0.053, 0.14) + phosphor *
      (0.06 + elevation * 0.9 + max(lines.x, lines.y) * 0.19 + atmosphere * 0.28 + beam * 0.36);
    gl_FragColor = vec4(mix(color, projected, hologram), 1.0);
    #include <tonemapping_fragment>
    #include <colorspace_fragment>
  }
`;

const cloudFragment = /* glsl */ `
  uniform sampler2D cloudMap;
  uniform vec2 offset;
  uniform float density;
  varying vec2 mapUv;
  varying vec3 viewNormal;
  varying vec3 viewPoint;
  void main() {
    vec3 normal = normalize(viewNormal);
    vec3 sun = normalize(vec3(-0.90, 0.30, 0.56));
    float light = smoothstep(-0.19, 0.43, dot(normal, sun));
    float angle = dot(normal, normalize(-viewPoint));
    float edge = smoothstep(0.00, 0.18, angle);
    vec2 cloudUv = mapUv * vec2(4.0, 2.0) + offset;
    float cloud = texture2D(cloudMap, fract(cloudUv)).r;
    float breakup = 0.55 + 0.45 * texture2D(cloudMap, fract(cloudUv * vec2(0.7, 1.5) + 0.19)).r;
    gl_FragColor = vec4(mix(vec3(0.08, 0.14, 0.26), vec3(0.85, 0.92, 1.0), light), cloud * breakup * density * edge * (0.22 + light * 0.78));
    #include <tonemapping_fragment>
    #include <colorspace_fragment>
  }
`;

const atmosphereFragment = /* glsl */ `
  uniform vec3 color;
  uniform float hologram;
  varying vec3 viewNormal;
  varying vec3 viewPoint;
  void main() {
    vec3 normal = normalize(viewNormal);
    vec3 view = normalize(-viewPoint);
    float angle = abs(dot(normal, view));
    float edge = pow(max(1.0 - angle, 0.0), 5.0) * smoothstep(0.0, 0.16, angle);
    float day = clamp(dot(normal, normalize(vec3(-0.90, 0.30, 0.56))) + 0.35, 0.0, 1.0);
    gl_FragColor = vec4(color, edge * mix(0.12 + day * 0.32, 0.65, hologram));
    #include <colorspace_fragment>
  }
`;

const projectorFragment = /* glsl */ `
  uniform float phase;
  varying vec2 mapUv;
  void main() {
    vec2 point = mapUv - vec2(0.5, 0.0);
    float width = mix(0.10, 0.5, mapUv.y);
    float sides = 1.0 - smoothstep(width * 0.50, width, abs(point.x));
    float ray = 0.65 + 0.35 * sin(point.x / width * 42.0 + phase * 2.7);
    float edge = 1.0 - smoothstep(0.52, 1.0, mapUv.y);
    gl_FragColor = vec4(0.06, 0.72, 1.0, sides * ray * edge * 0.23);
    #include <colorspace_fragment>
  }
`;

const MOON_POSITION = [0, 1.2, 0];
const LAB_POSITION = [0, 1.6, -1];

function shotSeconds(time) {
  return typeof time === 'number' ? time : time?.current ?? 0;
}

export function FilmMoon({ time, position = MOON_POSITION, radius = 1.15 }) {
  const [albedoMap, heightMap] = useLoader(PlanetTextureLoader, MOON_MAPS);
  const globe = useRef(null);
  const uniforms = useMemo(() => ({ albedoMap: { value: albedoMap }, heightMap: { value: heightMap } }), [albedoMap, heightMap]);

  useFrame(() => {
    globe.current.rotation.y = 3.95 + shotSeconds(time) * 0.13;
  });

  return (
    <mesh ref={globe} position={position} rotation={[-0.12, 3.95, 0.17]}>
      <sphereGeometry args={[radius, 96, 64]} />
      <shaderMaterial uniforms={uniforms} vertexShader={surfaceVertex} fragmentShader={moonFragment} />
    </mesh>
  );
}

function KuClouds({ cloudMap, radius, time }) {
  const cloudShell = useRef(null);
  const layers = useMemo(() => [
    { radius: radius * 1.007, spin: 0.015, density: 0.62, offset: [0.06, 0.17] },
    { radius: radius * 1.019, spin: 0.035, density: 0.30, offset: [0.43, 0.28] },
    { radius: radius * 1.038, spin: 0.025, density: 0.15, offset: [0.75, 0.62] },
  ].map((layer) => ({
    ...layer,
    uniforms: { cloudMap: { value: cloudMap }, density: { value: layer.density }, offset: { value: layer.offset } },
  })), [cloudMap, radius]);

  useFrame(() => {
    const seconds = shotSeconds(time);
    cloudShell.current.children.forEach((layer, index) => {
      layer.rotation.y = seconds * layers[index].spin;
    });
  });

  return (
    <group ref={cloudShell}>
      {layers.map((layer, index) => (
        <mesh key={layer.radius} renderOrder={index + 1}>
          <sphereGeometry args={[layer.radius, 56, 40]} />
          <shaderMaterial uniforms={layer.uniforms} vertexShader={surfaceVertex} fragmentShader={cloudFragment} transparent depthWrite={false} />
        </mesh>
      ))}
    </group>
  );
}

export function KuExchangeEarth({ time, position = MOON_POSITION, radius = 1.2, hologram = false, interactive = false }) {
  const [dayMap, nightMap, waterMap, cloudMap] = useLoader(PlanetTextureLoader, KU_MAPS);
  const earth = useRef(null);
  const surfaceMaterial = useRef(null);
  const hologramAmount = useMemo(() => ({ value: Number(hologram) }), [hologram]);
  const surface = useMemo(() => ({
    dayMap: { value: dayMap },
    nightMap: { value: nightMap },
    waterMap: { value: waterMap },
    phase: { value: 0 },
    hologram: hologramAmount,
  }), [dayMap, nightMap, waterMap, hologramAmount]);
  const atmosphere = useMemo(() => ({
    color: { value: new Color(hologram ? '#60ebff' : '#99cfff') },
    hologram: hologramAmount,
  }), [hologram, hologramAmount]);

  useFrame(() => {
    const seconds = shotSeconds(time);
    if (!interactive) earth.current.rotation.y = 3.98 + seconds * (hologram ? 0.32 : 0.115);
    surfaceMaterial.current.uniforms.phase.value = seconds;
  });

  return (
    <group position={position}>
      <group ref={earth} rotation={interactive ? [0, 0, 0] : [0, 3.98, 0.37]}>
        <mesh>
          <sphereGeometry args={[radius, 96, 64]} />
          <shaderMaterial ref={surfaceMaterial} uniforms={surface} vertexShader={surfaceVertex} fragmentShader={earthFragment} />
        </mesh>
        {!hologram && <KuClouds cloudMap={cloudMap} radius={radius} time={time} />}
      </group>
      <mesh renderOrder={4}>
        <sphereGeometry args={[radius * (hologram ? 1.08 : 1.065), 64, 40]} />
        <shaderMaterial uniforms={atmosphere} vertexShader={surfaceVertex} fragmentShader={atmosphereFragment}
          side={BackSide} blending={AdditiveBlending} transparent depthWrite={false} />
      </mesh>
    </group>
  );
}

function LabProjection({ time }) {
  const globe = useRef(null);
  const position = useMemo(() => [LAB_POSITION[0], 0, LAB_POSITION[2]], []);
  useFrame(() => {
    const seconds = shotSeconds(time);
    const rise = MathUtils.smootherstep(seconds, .62, 1.85);
    globe.current.visible = seconds >= .63;
    globe.current.scale.setScalar(.09 + rise * .91);
    globe.current.position.y = MathUtils.lerp(.68, LAB_POSITION[1], rise);
  });

  return <>
    <Projector time={time} />
    <group ref={globe} position={position} visible={false}>
      <KuExchangeEarth time={time} position={[0, 0, 0]} radius={0.78} hologram />
    </group>
  </>;
}

function Projector({ time }) {
  const rings = useRef(null);
  const beamMaterial = useRef(null);
  const beam = useMemo(() => ({ phase: { value: 0 } }), []);

  useFrame(() => {
    const seconds = shotSeconds(time);
    beamMaterial.current.uniforms.phase.value = seconds;
    rings.current.children.forEach((ring, index) => {
      const pulse = (seconds * 0.63 + index / 3) % 1;
      ring.scale.setScalar(0.65 + pulse * 0.93);
      ring.material.opacity = Math.sin(Math.PI * pulse) * .6 * MathUtils.smoothstep(seconds, .34, .75);
    });
  });

  return (
    <group position={[0, 0.38, -1]}>
      <group ref={rings}>
        {[0, 1, 2].map((ring) => (
          <mesh key={ring} rotation={[-Math.PI / 2, 0, 0]}>
            <ringGeometry args={[0.49, 0.506, 72]} />
            <meshBasicMaterial color="#70efff" transparent opacity={0} depthWrite={false} blending={AdditiveBlending} />
          </mesh>
        ))}
      </group>
      <mesh position={[0, 0.49, 0]}>
        <planeGeometry args={[1.86, 1]} />
        <shaderMaterial ref={beamMaterial} uniforms={beam} vertexShader={surfaceVertex} fragmentShader={projectorFragment}
          transparent depthWrite={false} blending={AdditiveBlending} />
      </mesh>
    </group>
  );
}

export function FilmPlanets({ shot, time }) {
  if (shot === 'signal') return <FilmMoon time={time} />;
  if (shot === 'earth') return <KuExchangeEarth time={time} />;
  if (shot === 'lab') return <LabProjection time={time} />;
  return null;
}
