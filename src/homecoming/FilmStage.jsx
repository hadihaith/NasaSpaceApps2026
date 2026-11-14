"use client";

import { Suspense, useEffect, useMemo, useRef } from "react";
import { Canvas, useFrame, useThree } from "@react-three/fiber";
import { AdditiveBlending, MathUtils, SRGBColorSpace, Vector3, VideoTexture } from "three";
import { FilmActors } from "./FilmActors";
import { FilmPlanets } from "./FilmPlanets";
import { FilmProps } from "./FilmProps";

const ease = (a, b, t) => MathUtils.smootherstep(t, a, b);
const blend = MathUtils.lerp;


const tvX = 1.65;
const tvY = 1.97;
const tvZ = -.7;
const tvStart = [tvX, tvY, 1.95];
const tvLook = [tvX, tvY, tvZ];
const wide = [[.35, 1.95, 7.65], [.23, 1.24, 0]];
const SPACE = new Set(["signal", "earth"]);
const TV = new Set(["apollo", "reveal", "correction", "party", "years", "return"]);
const WIDE = new Set(["reveal", "correction", "party", "years", "return", "lab", "hearing"]);

function TrackingCamera({ shot, time, reduced }) {
  const invalidate = useThree((state) => state.invalidate);
  const cameraTarget = useMemo(() => new Vector3(), []);
  useFrame(({ camera, size }) => {
    const t = time.current;
    const still = reduced;
    let start = wide[0], end = wide[0], look = wide[1], target = wide[1], progress = 0, focal = 38;
    switch (shot.id) {
      case "signal":
        start = [6, 3.2, 100]; end = [-.38, 1.44, 5.65]; look = target = [0, 1.2, 0]; progress = still ? 1 : ease(.22, 4.5, t); break;
      case "apollo":
        start = end = tvStart; look = target = tvLook; break;
      case "reveal":
        start = tvStart; end = [1.05, 1.98, 7.5]; look = tvLook; target = [.28, 1.3, .06]; progress = still ? 1 : ease(.14, 2.25, t); break;
      case "correction":
        start = [-.75, 1.83, 4.37]; end = [-.49, 1.79, 3.95]; look = [-.50, 1.55, .43];
        target = [-.24, 1.67, .40]; progress = still ? .5 : ease(0, 2.4, t); focal = 34; break;
      case "party":
        start = [.2, 2.06, 7.50]; end = [.38, 2.24, 8.05]; look = [.2, 1.41, 0]; target = [.15, 1.6, -.1];
        progress = still ? .65 : ease(.57, 2.75, t); break;
      case "jam":
        start = [.06, 1.46, 3.85]; end = [-.1, 1.29, 3.54]; look = target = [.05, 1.09, .55];
        progress = still ? .45 : ease(.07, 1.62, t); focal = 37; break;
      case "years":
        start = [.36, 2.24, 8.05]; end = [.16, 2.03, 7.81]; look = [.15, 1.6, -.10]; target = [.14, 1.46, 0]; progress = still ? .55 : ease(.23, 3.7, t); break;
      case "return":
        start = [.16, 2.03, 7.81]; end = [1.15, 1.88, 6.63]; look = [.14, 1.46, 0]; target = [.5, 1.59, -.28]; progress = still ? .65 : ease(.28, 3.95, t); break;
      case "earth":
        start = [1.15, 1.54, 7.6]; end = [-.1, 1.4, 5.8]; look = [.38, 1.15, 0]; target = [.05, 1.17, 0]; progress = still ? .7 : ease(0, 3.9, t); break;
      case "launch":
        start = [-.28, 1.54, 3.7]; end = [.26, 1.37, 3.35]; look = [-.27, 1.12, .52]; target = [-.10, 1.08, .55];
        progress = still ? .5 : ease(.08, 1.48, t); break;
      case "lab":
        start = [-1.08, 2.03, 8.47]; end = [.1, 1.71, 6.95]; look = [0, 1.31, -.6]; target = [-.05, 1.24, -.48]; progress = still ? .69 : ease(.07, 5.8, t); break;
      case "hearing":
        start = [-.26, 2.10, 7.45]; end = [-.18, 1.97, 6.89]; look = [.05, 1.34, .45]; target = [-.18, 1.39, .38]; progress = still ? .55 : ease(0, 4.8, t); break;
      case "deflate":
        start = [.2, 1.62, 3.69]; end = [.32, 1.31, 3.93]; look = [0, 1.25, .53]; target = [0, 1.03, .56]; progress = still ? .5 : ease(.77, 2.65, t); break;
    }
    const quake = still || shot.id !== "launch" ? 0 : Math.sin(t * 93) * .045 * Math.exp(-Math.pow((t - .78) / .18, 2));
    camera.position.set(blend(start[0], end[0], progress) + quake, blend(start[1], end[1], progress) - quake * .35, blend(start[2], end[2], progress));
    cameraTarget.set(blend(look[0], target[0], progress) - quake, blend(look[1], target[1], progress), blend(look[2], target[2], progress));
    camera.lookAt(cameraTarget);
    const ratio = size.width / Math.max(size.height, 1);
    if (WIDE.has(shot.id) && ratio < 1.32) {
      const fit = 1.42 / Math.max(.42, ratio);
      focal = MathUtils.radToDeg(2 * Math.atan(Math.tan(MathUtils.degToRad(focal / 2)) * fit));
    }
    if (camera.fov !== focal) {
      camera.fov = focal;
      camera.updateProjectionMatrix();
    }
  }, -2);
  useEffect(() => { invalidate(); }, [invalidate, shot]);
  return null;
}

function Sky({ shot, time }) {
  const sky = useRef(null);
  const positions = useMemo(() => {
    const random = MathUtils.seededRandom;
    random(484);
    const out = new Float32Array(720 * 3);
    for (let i = 0; i < out.length; i += 3) {
      const phi = random() * Math.PI * 2;
      const z = random() * 2 - 1;
      const radius = 175 + 29 * random();
      const xy = Math.sqrt(1 - z * z) * radius;
      out.set([Math.cos(phi) * xy, z * radius, Math.sin(phi) * xy], i);
    }
    return out;
  }, []);
  useFrame(() => {
    sky.current.rotation.y = time.current * (shot.id === "signal" ? .008 : -.014);
    sky.current.material.opacity = shot.id === "signal" ? .7 * ease(.18, 2.45, time.current) : .82;
  });
  return <points ref={sky} frustumCulled={false}>
    <bufferGeometry><bufferAttribute attach="attributes-position" args={[positions, 3]} /></bufferGeometry>
    <pointsMaterial color="#ddd7ff" size={.45} transparent depthWrite={false} opacity={.8} blending={AdditiveBlending} sizeAttenuation />
  </points>;
}

function makeMonitorArtwork(artemis) {
  const canvas = document.createElement("canvas");
  canvas.width = 768;
  canvas.height = 460;
  const ctx = canvas.getContext("2d");
  const fill = ctx.createLinearGradient(0, 0, 768, 460);
  fill.addColorStop(0, "#132f4e");
  fill.addColorStop(1, "#0b061b");
  ctx.fillStyle = fill;
  ctx.fillRect(0, 0, 768, 460);
  ctx.strokeStyle = artemis ? "#84e6ff" : "#f7c261";
  ctx.lineWidth = 3;
  ctx.strokeRect(22, 22, 724, 416);
  ctx.fillStyle = "#bdf2ff";
  ctx.font = "bold 25px monospace";
  ctx.fillText(artemis ? "EARTH NEWS  //  LIVE" : "LUNAR TELEVISION  //  1969", 45, 69);
  ctx.fillStyle = "#ffcb7c";
  ctx.font = "900 80px system-ui";
  ctx.fillText(artemis ? "ARTEMIS" : "APOLLO 11", 44, 176);
  ctx.font = "bold 24px monospace";
  ctx.fillStyle = "#f6f2fc";
  ctx.fillText(artemis ? "BACK TO THE MOON" : "LUNAR SURFACE", 48, 225);
  ctx.strokeStyle = "#82e6ff";
  ctx.lineWidth = 5;
  ctx.beginPath(); ctx.ellipse(617, 304, 94, 46, -.5, 0, Math.PI * 2); ctx.stroke();
  ctx.fillStyle = "#d2e8f4";
  ctx.beginPath(); ctx.arc(618, 302, 48, 0, Math.PI * 2); ctx.fill();
  ctx.fillStyle = "#6992ad";
  ctx.beginPath(); ctx.arc(604, 297, 15, 0, Math.PI * 2); ctx.fill();
  ctx.fillStyle = "#f6d498";
  ctx.beginPath(); ctx.arc(531, 346, 10, 0, Math.PI * 2); ctx.fill();
  ctx.strokeStyle = "#7eb3c8";
  ctx.lineWidth = 2;
  for (let y = 24; y < 436; y += 6) {
    ctx.globalAlpha = .19;
    ctx.beginPath(); ctx.moveTo(24, y); ctx.lineTo(744, y); ctx.stroke();
  }
  return canvas;
}

function Television({ shot, video, time }) {
  const artemis = shot.id === "return";
  const artwork = useMemo(() => makeMonitorArtwork(artemis), [artemis]);
  const screen = useMemo(() => ({ frameTime: { value: 0 }, tape: { value: null }, exposure: { value: 1 } }), []);
  const cover = useRef(null);
  const on = useRef(null);
  const shader = useRef(null);
  useEffect(() => {
    const material = shader.current;
    const tape = video.current;
    if (!material || !tape || artemis) return;
    const texture = new VideoTexture(tape);
    texture.colorSpace = SRGBColorSpace;
    material.uniforms.tape.value = texture;
    return () => texture.dispose();
  }, [artemis, video]);
  useFrame(() => {
    const hasFrame = !!shader.current?.uniforms.tape.value && video.current?.readyState >= 2;
    if (shader.current) {
      shader.current.uniforms.frameTime.value = time.current;
      shader.current.uniforms.exposure.value = shot.id === "years" ? .19 : 1;
    }
    if (cover.current) cover.current.visible = !hasFrame;
    if (on.current) on.current.visible = hasFrame;
  });

  return <group position={[tvX, tvY, tvZ]}>
    <mesh position={[0, 0, -.22]}>
      <boxGeometry args={[3.16, 2.17, .55]} />
      <meshStandardMaterial color="#4b2b56" roughness={.62} metalness={.2} />
    </mesh>
    <mesh position={[0, 0, .038]}>
      <boxGeometry args={[2.96, 1.94, .14]} />
      <meshStandardMaterial color="#ba704e" roughness={.42} metalness={.5} />
    </mesh>
    <mesh position={[0, 0, .12]}>
      <boxGeometry args={[2.79, 1.77, .05]} />
      <meshStandardMaterial color="#191224" roughness={.76} />
    </mesh>
    <mesh ref={cover} position={[0, 0, .154]}>
      <planeGeometry args={[2.64, 1.61]} />
      <meshBasicMaterial toneMapped={false}><canvasTexture attach="map" args={[artwork]} colorSpace={SRGBColorSpace} /></meshBasicMaterial>
    </mesh>
    {!artemis && <mesh ref={on} visible={false} position={[0, 0, .159]}>
      <planeGeometry args={[2.64, 1.61]} />
      <shaderMaterial ref={shader} uniforms={screen}
        vertexShader={`varying vec2 uvScreen; void main(){uvScreen=uv; gl_Position=projectionMatrix*modelViewMatrix*vec4(position,1.);}`}
        fragmentShader={`uniform sampler2D tape; uniform float frameTime; uniform float exposure; varying vec2 uvScreen;
          void main(){
            vec2 p=uvScreen-.5; vec2 uv=p*(1.+.15*dot(p,p))+.5;
            vec2 framed=vec2(uv.x, (uv.y-.5)*.85+.5); vec3 c=texture2D(tape,framed).rgb;
            float light=dot(c,vec3(.299,.587,.114));
            float edge=(1.-smoothstep(.43,.48,max(abs(p.x),abs(p.y))+.075*dot(p,p)));
            float bars=.94+.06*sin(uv.y*571.+frameTime*38.);
            gl_FragColor=vec4(vec3(.83,.92,1.)*(.026+light*1.15)*edge*bars*exposure,1.);
            #include <colorspace_fragment>
          }`} />
    </mesh>}
    {[-.86, -.72, -.58, -.44, -.30].map((x) => <mesh key={x} position={[x, -.957, .075]}>
      <boxGeometry args={[.073, .045, .05]} /><meshBasicMaterial color={x < -.64 ? "#99f9ef" : "#e89d76"} />
    </mesh>)}
  </group>;
}

export function FilmStage({ shot, time, video, ready, reduced, failed }) {
  const space = SPACE.has(shot.id);
  return <Canvas key="homecoming-film" className="hc-live-stage" camera={{ position: [0, 1.8, 7.6], near: .08, far: 260, fov: 38 }}
    dpr={[1, 1.35]} gl={{ alpha: true, antialias: true, powerPreference: "default" }}
    onCreated={({ gl }) => {
      gl.domElement.addEventListener("webglcontextlost", (event) => { event.preventDefault(); failed(); }, { once: true });
      ready();
    }}>
    <TrackingCamera shot={shot} time={time} reduced={reduced} />
    <ambientLight intensity={space ? .23 : 1.31} color={space ? "#b7c3fd" : "#c7b6e9"} />
    <hemisphereLight args={["#d5cdfd", "#2a1538", 1.35]} />
    <directionalLight color="#ffcfad" intensity={space ? 2 : 3.9} position={[-3, 6, 4]} />
    <directionalLight color="#9b78ff" intensity={space ? .15 : 2.95} position={[2, 3, -2]} />
    <pointLight color={shot.id === "lab" ? "#40edff" : "#f974a5"} intensity={15} distance={10} position={[3, 2.3, 1.06]} />
    {space && <Sky shot={shot} time={time} />}
    <Suspense fallback={null}><FilmPlanets shot={shot.id} time={time} /></Suspense>
    <Suspense fallback={null}><FilmActors shot={shot} time={time} /></Suspense>
    {TV.has(shot.id) && <Television shot={shot} time={time} video={video} />}
    <FilmProps shot={shot.id} time={time} />
  </Canvas>;
}
