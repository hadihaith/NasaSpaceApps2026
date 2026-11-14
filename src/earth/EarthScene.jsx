import React, { Component, Suspense, useEffect, useMemo, useRef, useState, useSyncExternalStore } from 'react';
import { Canvas, useFrame, useLoader, useThree } from '@react-three/fiber';
import { MathUtils, Quaternion, SRGBColorSpace, TextureLoader, Vector3 } from 'three';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import { Atmosphere, EnvironmentEffects, regionDirection } from './EnvironmentEffects';
import { OrbitClimate } from './OrbitClimate';
import { KuExchangeEarth } from '../homecoming/FilmPlanets';
import { RegionSelector } from './RegionSelector';
import { EARTH_REGIONS, EARTH_TEXTURE_URL, normalizeEnvironment, toCoordinates } from './regions';
import './earth-scene.css';

const rotationQuery = '(prefers-reduced-motion: reduce)';
const getReducedMotion = () => window.matchMedia(rotationQuery).matches;
const subscribeToReducedMotion = (callback) => {
  const media = window.matchMedia(rotationQuery);
  media.addEventListener('change', callback);
  return () => media.removeEventListener('change', callback);
};

class EarthTextureLoader extends TextureLoader {
  load(url, onLoad, onProgress, onError) {
    return super.load(url, (texture) => {
      texture.colorSpace = SRGBColorSpace;
      texture.anisotropy = 4;
      onLoad(texture);
    }, onProgress, onError);
  }
}

class GlobeErrorBoundary extends Component {
  state = { failed: false };

  static getDerivedStateFromError() {
    return { failed: true };
  }

  render() {
    return this.state.failed ? this.props.fallback : this.props.children;
  }
}

const up = new Vector3(0, 1, 0);
const cameraAid = new Vector3();
const rotation = new Quaternion();
const identity = new Quaternion();

function GlobeControls({ region, autoRotate, reducedMotion, flight }) {
  const { camera, gl, size } = useThree();
  const controlsRef = useRef(null);
  const focusPending = useRef(true);
  const refocusAfter = useRef(0);
  const distanceBeforeFlight = useRef(null);
  const flightAngle = useRef(0);
  const flightDirection = useRef(new Vector3());
  const lookDirection = useMemo(() => {
    const point = regionDirection(region ?? EARTH_REGIONS[0]);
    return point.applyAxisAngle(up, MathUtils.degToRad(-10)).normalize();
  }, [region]);

  useEffect(() => {
    const controls = new OrbitControls(camera, gl.domElement);
    controls.enablePan = false;
    controls.dampingFactor = 0.085;
    controls.rotateSpeed = 0.6;
    controls.zoomSpeed = 0.75;
    controls.autoRotateSpeed = 0.23;
    controls.minPolarAngle = 0.18;
    controls.maxPolarAngle = Math.PI - 0.18;
    const stopFocus = () => {
      focusPending.current = false;
      refocusAfter.current = performance.now() + 7000;
    };
    controls.addEventListener('start', stopFocus);
    controls.addEventListener('end', stopFocus);
    controlsRef.current = controls;
    return () => {
      controls.removeEventListener('start', stopFocus);
      controls.removeEventListener('end', stopFocus);
      controls.dispose();
      controlsRef.current = null;
    };
  }, [camera, gl]);

  useEffect(() => {
    const widthRatio = Math.max(1, size.height / Math.max(1, size.width));
    const snugDistance = 3.92 * widthRatio;
    const controls = controlsRef.current;
    controls.minDistance = 1.96 * widthRatio;
    controls.maxDistance = 7.5 * widthRatio;
    camera.position.setLength(snugDistance);
    controls.update();
  }, [camera, size.height, size.width]);

  useEffect(() => {
    focusPending.current = true;
    refocusAfter.current = performance.now() + 5500;
  }, [lookDirection]);

  useFrame((_, delta) => {
    const controls = controlsRef.current;
    if (!controls) return;
    const step = Math.min(delta, 0.05);
    if (flight) {
      const wide = size.width / Math.max(1, size.height);
      const fit = Math.max(1, .89 / wide);
      const here = camera.position.length();
      if (flight.running && distanceBeforeFlight.current === null) distanceBeforeFlight.current = here;
      controls.enabled = !flight.running;
      controls.enableDamping = !reducedMotion;
      controls.autoRotate = false;
      if (flight.running) {
        if (!reducedMotion) flightAngle.current = Math.min(.31, flightAngle.current + step * .067);
        flightDirection.current.copy(lookDirection).applyAxisAngle(up, flightAngle.current - .08);
        cameraAid.copy(camera.position).normalize().lerp(flightDirection.current, reducedMotion ? 1 : 1 - Math.exp(-3.8 * step)).normalize();
        const distance = MathUtils.damp(here, (reducedMotion ? 3.92 : 2.72) * fit, reducedMotion ? 1000 : 3.2, step);
        camera.position.copy(cameraAid).multiplyScalar(distance);
        controls.update();
      } else if (distanceBeforeFlight.current !== null) {
        const distance = MathUtils.damp(here, Math.max(3.25 * fit, distanceBeforeFlight.current), reducedMotion ? 1000 : 2.8, step);
        camera.position.setLength(distance);
        controls.update();
        if (Math.abs(distance - Math.max(3.25 * fit, distanceBeforeFlight.current)) < .02) {
          distanceBeforeFlight.current = null;
          flightAngle.current = 0;
        }
      }
      if (flight.running) return;
    }
    controls.enableDamping = !reducedMotion;
    controls.autoRotate = autoRotate && !reducedMotion && !focusPending.current
      && performance.now() > refocusAfter.current;

    if (focusPending.current) {
      const distance = camera.position.length();
      cameraAid.copy(camera.position).normalize();
      if (reducedMotion || cameraAid.angleTo(lookDirection) < 0.008) {
        camera.position.copy(lookDirection).multiplyScalar(distance);
        focusPending.current = false;
      } else {
        rotation.setFromUnitVectors(cameraAid, lookDirection);
        rotation.slerpQuaternions(identity, rotation, 1 - Math.exp(-3.8 * step));
        camera.position.applyQuaternion(rotation);
      }
    }
    controls.update(step);
  });
  return null;
}

const markerAxis = new Vector3(0, 0, 1);

function GlobeMarkers({ region, onRegionSelect, immersive }) {
  const positions = useMemo(() => EARTH_REGIONS.map((item) => {
    const normal = regionDirection(item);
    return { ...item, position: normal.clone().multiplyScalar(1.008), orientation: new Quaternion().setFromUnitVectors(markerAxis, normal) };
  }), []);

  return positions.map(({ id, position, orientation }) => (
    <group key={id} position={position} quaternion={orientation}>
      <mesh onClick={(event) => {
        event.stopPropagation();
        if (event.delta < 5) onRegionSelect(id);
      }}>
        <circleGeometry args={[0.059, 20]} />
        <meshBasicMaterial color="#0a1823" transparent opacity={immersive ? .3 : region?.id === id ? 0.58 : 0.82} />
      </mesh>
      <mesh position-z={0.001}>
        <ringGeometry args={immersive ? [0.047, 0.051, 32] : [0.040, 0.052, 24]} />
        <meshBasicMaterial color={region?.id === id ? '#ffe1af' : '#a0d5ef'} transparent opacity={immersive && region?.id !== id ? .58 : 1} />
      </mesh>
      <mesh position-z={0.002}>
        <circleGeometry args={[immersive ? .012 : .018, 12]} />
        <meshBasicMaterial color={region?.id === id ? '#ffe1af' : '#a0d5ef'} />
      </mesh>
    </group>
  ));
}

function Globe({ region, values, autoRotate, reducedMotion, onRegionSelect, onReady, onContextLost, useKuGlobe, flight }) {
  const basemap = useLoader(EarthTextureLoader, EARTH_TEXTURE_URL);
  const gl = useThree((state) => state.gl);
  const time = useRef(0);
  useFrame((_, delta) => { if (useKuGlobe && !reducedMotion) time.current += Math.min(delta, .05); });

  useEffect(() => {
    onReady(true);
    const handleLoss = (event) => {
      event.preventDefault();
      onContextLost(true);
    };
    gl.domElement.addEventListener('webglcontextlost', handleLoss);
    return () => gl.domElement.removeEventListener('webglcontextlost', handleLoss);
  }, [gl, onReady, onContextLost]);

  return (
    <>
      <GlobeControls region={region} autoRotate={autoRotate} reducedMotion={reducedMotion} flight={flight} />
      {useKuGlobe && <KuExchangeEarth time={time} position={[0, 0, 0]} radius={1} interactive />}
      {flight && region ? <OrbitClimate key={region.id} region={region} flight={flight} values={values} reducedMotion={reducedMotion} />
        : <EnvironmentEffects basemap={basemap} region={region} values={values} reducedMotion={reducedMotion} overlay={useKuGlobe} />}
      {!useKuGlobe && <Atmosphere />}
      {!flight?.running && <GlobeMarkers region={region} onRegionSelect={onRegionSelect} immersive={Boolean(flight)} />}
    </>
  );
}

function MapFallback({ region, values, loading = false }) {
  const [moisture, vegetation, drought] = values;
  const dry = MathUtils.clamp(0.72 * drought + 0.65 * Math.max(0, 0.52 - moisture), 0, 1);
  const wet = MathUtils.smoothstep(moisture, 0.42, 1);
  const hue = MathUtils.lerp(MathUtils.lerp(95 + vegetation * 20, 192, wet), 23, dry);
  return (
    <div className={`earth-map-fallback${loading ? '' : ' earth-map-active'}`}>
      <div className="earth-map-image" style={{ backgroundImage: `url(${EARTH_TEXTURE_URL})` }} role="img" aria-label="NASA Blue Marble world map">
        {region && (
          <span className="earth-map-highlight" aria-hidden="true" style={{
            left: `${(region.longitude + 180) / 3.6}%`, top: `${(90 - region.latitude) / 1.8}%`,
            width: `${region.radius / (1.8 * Math.cos(MathUtils.degToRad(region.latitude)))}%`,
            height: `${region.radius / 0.9}%`, '--earth-highlight': `hsl(${hue} 96% 64%)`,
          }} />
        )}
      </div>
      <p role="status">{loading ? 'Loading the 3D globe…' : '2D map · region selection available'}</p>
    </div>
  );
}

export function EarthScene({ regionId, environment, onRegionSelect, presentation = 'default', flight = null }) {
  const viewport = useRef(null);
  const [ready, setReady] = useState(false);
  const [contextLost, setContextLost] = useState(false);
  const [idleRotation, setIdleRotation] = useState(true);
  const [visible, setVisible] = useState(true);
  const reducedMotion = useSyncExternalStore(subscribeToReducedMotion, getReducedMotion, () => true);
  const region = EARTH_REGIONS.find(({ id }) => id === regionId);
  const values = normalizeEnvironment(environment);
  const fallback = <MapFallback region={region} values={values} />;

  useEffect(() => {
    if (!window.IntersectionObserver) return;
    const observer = new IntersectionObserver(([entry]) => setVisible(entry.isIntersecting));
    observer.observe(viewport.current);
    return () => observer.disconnect();
  }, []);

  return (
    <section className={`earth-scene earth-scene-${presentation}`} aria-label="Earth visualization">
      <div className="earth-viewport" ref={viewport}>
        {!flight && <><div className="earth-grid" aria-hidden="true" />
        <div className="earth-orbit" aria-hidden="true" />
        <div className="earth-viewport-header">
          <span><i aria-hidden="true" /> Simulation view</span>
          <span>{region ? toCoordinates(region) : 'Select a region'}</span>
        </div></>}
        <div className="earth-canvas">
          {contextLost ? fallback : (
            <GlobeErrorBoundary fallback={fallback}>
              <Canvas fallback={fallback} frameloop={visible ? 'always' : 'never'}
                camera={{ position: [3.1, 0.8, 1.15], fov: 38, near: 0.1, far: 30 }}
                dpr={[1, 1.5]} flat gl={{ powerPreference: 'default', failIfMajorPerformanceCaveat: !flight }}>
                <Suspense fallback={null}>
                  <Globe region={region} values={values} autoRotate={!flight && idleRotation} reducedMotion={reducedMotion}
                    onRegionSelect={onRegionSelect} onReady={setReady} onContextLost={setContextLost}
                    useKuGlobe={presentation === "homecoming" || Boolean(flight)} flight={flight} />
                </Suspense>
              </Canvas>
            </GlobeErrorBoundary>
          )}
          {!ready && !contextLost && <div className="earth-loading"><MapFallback region={region} values={values} loading /></div>}
        </div>
        {!flight && <div className="earth-viewport-footer">
          <span>Drag to orbit <b>·</b> Scroll to zoom</span>
          {!reducedMotion && <button type="button" aria-pressed={idleRotation} onClick={() => setIdleRotation((value) => !value)}>
            Idle rotation <b>{idleRotation ? 'on' : 'off'}</b>
          </button>}
        </div>}
      </div>
      {!flight && <><RegionSelector regionId={regionId} onRegionSelect={onRegionSelect} />
      <p className="earth-credit">Basemap <a href="https://svs.gsfc.nasa.gov/2915/">NASA / GSFC Blue Marble</a></p></>}
    </section>
  );
}

export default EarthScene;
