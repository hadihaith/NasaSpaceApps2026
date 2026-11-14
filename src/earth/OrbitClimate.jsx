import { useMemo, useRef } from 'react';
import { useFrame, useLoader } from '@react-three/fiber';
import { AdditiveBlending, BufferAttribute, BufferGeometry, MathUtils, TextureLoader, Vector3, Vector4 } from 'three';
import { regionDirection } from './EnvironmentEffects';

const vertex = /* glsl */ `
  varying vec3 point;
  varying vec2 mapUv;
  void main() {
    point = position;
    mapUv = uv;
    gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
  }
`;

const patch = /* glsl */ `
  uniform vec3 center;
  uniform vec3 east;
  uniform vec3 north;
  uniform float radius;
  uniform float time;
  uniform vec4 weather;
  uniform vec3 terrain;
  uniform float pulse;
  varying vec3 point;
  varying vec2 mapUv;

  float hash(vec2 p) {
    p = fract(p * vec2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return fract(p.x * p.y);
  }
  float noise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    return mix(mix(hash(i), hash(i + vec2(1, 0)), f.x),
               mix(hash(i + vec2(0, 1)), hash(i + 1.0), f.x), f.y);
  }
  vec2 localPoint(vec3 direction) {
    return vec2(dot(direction, east), dot(direction, north)) / radius;
  }
  float shapeMask(vec3 direction, vec2 local, float span) {
    float distanceFromCenter = length(local) + (noise(local * 6.0) - 0.5) * 0.17;
    return (1.0 - smoothstep(span * 0.42, span, distanceFromCenter)) * step(0.55, dot(direction, center));
  }
`;

const surface = /* glsl */ `
  ${patch}
  uniform sampler2D ocean;

  float cracks(vec2 p) {
    vec2 cell = floor(p);
    vec2 offset = fract(p);
    float first = 10.0;
    float second = 10.0;
    for (int y = -1; y <= 1; y++) {
      for (int x = -1; x <= 1; x++) {
        vec2 neighbor = vec2(float(x), float(y));
        vec2 jitter = vec2(hash(cell + neighbor), hash(cell + neighbor + 13.7));
        vec2 diff = neighbor + jitter - offset;
        float d = dot(diff, diff);
        second = min(second, max(first, d));
        first = min(first, d);
      }
    }
    return 1.0 - smoothstep(0.013, 0.095, second - first);
  }

  void main() {
    vec3 direction = normalize(point);
    vec2 local = localPoint(direction);
    float area = shapeMask(direction, local, 1.08);
    if (area < 0.001) discard;
    float land = 1.0 - smoothstep(0.20, 0.72, texture2D(ocean, mapUv).r);
    float dry = terrain.z;
    float heat = max(0.0, weather.x);
    float chill = max(0.0, -weather.x);
    float wet = smoothstep(0.48, 0.96, terrain.x);
    float healthy = smoothstep(0.57, 0.89, terrain.y) * (1.0 - dry);
    float mottled = noise(local * 26.0) * 0.45 + noise(local * 8.0) * 0.55;
    float parched = smoothstep(0.18, 0.88, dry) * land;
    float fissures = parched * cracks(local * 15.0 + noise(local * 22.0) * 0.32);
    vec3 color = mix(vec3(0.015, 0.29, 0.132), vec3(0.19, 0.58, 0.025), mottled);
    color = mix(color, mix(vec3(0.10, 0.026, 0.006), vec3(0.7, 0.275, 0.047), mottled), parched);
    color = mix(color, vec3(0.028, 0.01, 0.005), fissures * 0.93);
    float standingWater = wet * smoothstep(0.51, 0.78, mottled) * (1.0 - parched);
    color = mix(color, vec3(0.006, 0.31, 0.48), standingWater * 0.83);
    color = mix(color, vec3(0.26, 0.62, 0.98), chill * (0.34 + mottled * 0.49));
    float heatWaves = pow(0.5 + 0.5 * sin(local.y * 48.0 + local.x * 4.0 + noise(local * 5.0) * 8.0 - time * 3.5), 5.0);
    color = mix(color, vec3(1.0, 0.29, 0.037), heat * (0.20 + heatWaves * 0.52) * (1.0 - fissures));
    float wave = exp(-pow((length(local) - (1.0 - pulse) * 1.25) / 0.055, 2.0)) * pulse;
    float intensity = 0.63 * parched + 0.37 * standingWater + 0.32 * healthy * land + heat * 0.56 + chill * 0.49;
    gl_FragColor = vec4(mix(color, vec3(0.92, 0.79, 0.49), wave), clamp(area * intensity + wave * 0.66, 0.0, 0.92));
    #include <colorspace_fragment>
  }
`;

const storm = /* glsl */ `
  ${patch}
  uniform float layer;
  void main() {
    vec3 direction = normalize(point);
    vec2 local = localPoint(direction);
    float area = shapeMask(direction, local, 1.30);
    if (area < 0.001) discard;
    float rain = weather.y;
    float humidity = weather.z;
    float wind = weather.w;
    vec2 flow = local - vec2(-0.075, -0.016) * time * (0.35 + 1.9 * wind);
    vec2 part = local * 4.5;
    float spin = atan(part.y, part.x) + length(part) * 1.95 + time * 0.56;
    float spiral = (0.5 + 0.5 * sin(spin * 2.0)) * 0.27 * rain;
    float cloud = noise(flow * (6.0 + layer * 3.0)) * 0.48 +
                  noise(flow * 15.0 + layer * 4.2) * 0.26 +
                  noise(flow * 31.0 - layer * 3.8) * 0.14 + spiral;
    float coverage = smoothstep(mix(0.64, 0.31, max(humidity * 0.75, rain)), 0.86, cloud);
    float thickness = area * coverage * (0.10 + rain * 0.89 + humidity * 0.39);
    vec3 deck = mix(vec3(0.08, 0.19, 0.32), vec3(0.70, 0.88, 1.0), smoothstep(0.39, 0.84, cloud + layer * 0.09));
    gl_FragColor = vec4(deck, thickness * (1.0 - layer * 0.40));
    #include <colorspace_fragment>
  }
`;

const rainVertex = /* glsl */ `
  attribute vec4 seed;
  uniform vec3 center;
  uniform vec3 east;
  uniform vec3 north;
  uniform float radius;
  uniform vec4 weather;
  uniform float time;
  varying float opacity;

  void main() {
    float speed = 0.68 + seed.z * 0.72 + weather.w * 0.42;
    float fall = fract(seed.y * 16.1 + time * speed);
    float angle = seed.x * 6.283185;
    float spread = sqrt(seed.y) * radius * (0.36 + weather.y * 0.68);
    vec3 target = normalize(center + east * (cos(angle) * spread - fall * weather.w * 0.095)
                                   + north * sin(angle) * spread);
    float altitude = 1.011 + (1.0 - fall) * (0.075 + weather.y * 0.081);
    vec3 end = target * (altitude + seed.w * (0.016 + seed.z * 0.018)) + east * seed.w * weather.w * 0.019;
    opacity = smoothstep(seed.z * .94, seed.z * .94 + .12, weather.y);
    opacity *= smoothstep(0.0, 0.17, fall) * (1.0 - smoothstep(0.83, 1.0, fall));
    gl_Position = projectionMatrix * modelViewMatrix * vec4(end, 1.0);
  }
`;

const rainFragment = /* glsl */ `
  varying float opacity;
  void main() {
    gl_FragColor = vec4(0.29, 0.73, 1.0, opacity * 0.85);
    #include <colorspace_fragment>
  }
`;

const streamVertex = /* glsl */ `
  attribute vec4 seed;
  uniform vec3 center;
  uniform vec3 east;
  uniform vec3 north;
  uniform float radius;
  uniform vec4 weather;
  uniform float time;
  varying float opacity;

  void main() {
    float u = seed.x;
    float lane = seed.y * 2.0 - 1.0;
    float span = 0.76 + weather.w * 0.59;
    float along = (u * 2.0 - 1.0) * radius * span;
    float lift = 0.043 + seed.z * 0.084;
    float sway = sin(u * 3.8 + seed.z * 6.0) * 0.15 + cos(u * 7.0 + lane) * 0.055;
    vec3 direction = normalize(center + east * -along + north * radius * (lane + sway));
    float head = fract(time * (0.35 + weather.w * 0.65) + seed.z);
    float trail = smoothstep(0.42, 1.0, 1.0 - fract(head - u));
    float strength = smoothstep(0.12, 0.87, weather.w);
    opacity = pow(sin(u * 3.14159), 0.7) * trail * strength * (1.0 - abs(lane) * 0.63);
    gl_Position = projectionMatrix * modelViewMatrix * vec4(direction * (1.0 + lift), 1.0);
  }
`;

const streamFragment = /* glsl */ `
  uniform vec3 terrain;
  varying float opacity;
  void main() {
    gl_FragColor = vec4(mix(vec3(0.52, 0.83, 1.0), vec3(1.0, 0.60, 0.25), terrain.z), opacity * 0.9);
    #include <colorspace_fragment>
  }
`;

function segmentGeometry(count, seedFor) {
  const geometry = new BufferGeometry();
  const attributes = new Float32Array(count * 8);
  for (let i = 0; i < count; i++) {
    attributes.set(seedFor(i, 0), i * 8);
    attributes.set(seedFor(i, 1), i * 8 + 4);
  }
  geometry.setAttribute('seed', new BufferAttribute(attributes, 4));
  geometry.setAttribute('position', new BufferAttribute(new Float32Array(count * 6), 3));
  return geometry;
}

const fraction = (value) => value - Math.floor(value);
const rainSeeds = Array.from({ length: 530 }, (_, i) => [
  fraction((i + 1) * 0.61803399), fraction((i + 1) * 0.75487767), fraction((i + 1) * 0.41421356),
]);
const limit = MathUtils.clamp;

export function OrbitClimate({ region, flight, values, reducedMotion }) {
  const surfaceMaterial = useRef(null);
  const ocean = useLoader(TextureLoader, '/homecoming/planets/ku-water.webp');
  const rainGeometry = useMemo(() => segmentGeometry(rainSeeds.length, (i, end) => [...rainSeeds[i], end]), []);
  const streamGeometry = useMemo(() => segmentGeometry(17 * 36, (i, end) => {
    const lane = Math.floor(i / 36);
    return [(i % 36 + end) / 36, lane / 16, fraction((lane + 1) * 0.61803399), 0];
  }), []);
  const uniforms = useMemo(() => {
    const center = regionDirection(region);
    const east = new Vector3().crossVectors(new Vector3(0, 1, 0), center).normalize();
    const north = new Vector3().crossVectors(center, east).normalize();
    return {
      center: { value: center }, east: { value: east }, north: { value: north },
      radius: { value: Math.sin(MathUtils.degToRad(region.radius + 6)) },
      time: { value: 0 }, weather: { value: new Vector4() }, terrain: { value: new Vector3() },
      ocean: { value: ocean }, pulse: { value: 0 },
    };
  }, [region, ocean]);
  const lowerCloud = useMemo(() => ({ ...uniforms, layer: { value: 0 } }), [uniforms]);
  const upperCloud = useMemo(() => ({ ...uniforms, layer: { value: 1 } }), [uniforms]);
  const weatherTarget = useMemo(() => {
    const { weather, settings } = flight;
    const temperature = settings.heat >= 0 ? Math.max(settings.heat / 8, (weather.temperature - 26) / 19) : settings.heat / 5;
    return new Vector4(
      limit(temperature, -1, 1),
      limit(Math.sqrt(weather.rain / 270), 0, 1),
      limit((weather.humidity - 18) / 83, 0, 1),
      limit(weather.wind / 16, 0, 1),
    );
  }, [flight]);
  const terrainTarget = useMemo(() => new Vector3(...values), [values]);

  useFrame((_, delta) => {
    const current = surfaceMaterial.current?.uniforms;
    if (!current) return;
    const elapsed = Math.min(delta, .06);
    const blend = reducedMotion ? 1 : 1 - Math.exp(-8.5 * elapsed);
    current.weather.value.lerp(weatherTarget, blend);
    current.terrain.value.lerp(terrainTarget, blend);
    if (!reducedMotion) current.time.value += elapsed;
    current.pulse.value = reducedMotion ? 0 : flight.pulse ? Math.max(0, 1 - (performance.now() - flight.pulse) / 940) : 0;
  });

  return <>
    <mesh renderOrder={1}>
      <sphereGeometry args={[1.004, 80, 48]} />
      <shaderMaterial ref={surfaceMaterial} vertexShader={vertex} fragmentShader={surface} uniforms={uniforms} transparent depthWrite={false} />
    </mesh>
    <mesh renderOrder={5}>
      <sphereGeometry args={[1.061, 72, 48]} />
      <shaderMaterial vertexShader={vertex} fragmentShader={storm} uniforms={lowerCloud} transparent depthWrite={false} />
    </mesh>
    <mesh renderOrder={6}>
      <sphereGeometry args={[1.081, 72, 48]} />
      <shaderMaterial vertexShader={vertex} fragmentShader={storm} uniforms={upperCloud} transparent depthWrite={false} />
    </mesh>
    <lineSegments geometry={rainGeometry} renderOrder={7} frustumCulled={false}>
      <shaderMaterial vertexShader={rainVertex} fragmentShader={rainFragment} uniforms={uniforms} blending={AdditiveBlending} transparent depthWrite={false} />
    </lineSegments>
    <lineSegments geometry={streamGeometry} renderOrder={8} frustumCulled={false}>
      <shaderMaterial vertexShader={streamVertex} fragmentShader={streamFragment} uniforms={uniforms} blending={AdditiveBlending} transparent depthWrite={false} />
    </lineSegments>
  </>;
}
