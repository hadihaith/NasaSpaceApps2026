"use client";

import { useEffect, useMemo, useRef } from "react";
import { extend, useFrame } from "@react-three/fiber";
import {
  AdditiveBlending, CanvasTexture, Color, DynamicDrawUsage, DoubleSide,
  Object3D, Shape, SRGBColorSpace, MathUtils,
} from "three";
import { RoundedBoxGeometry } from "three/addons/geometries/RoundedBoxGeometry.js";

extend({ RoundedBoxGeometry });

export const PROP_CUES = { party: [.72], jam: [.58, 1.08], launch: [.74], deflate: [.84] };

const PAPER = ["#ffbf59", "#fa529c", "#64ddeb", "#91eea5", "#a590ff", "#fff1d7"];
const INDIGO = "#2a2849";
const GOLD = "#ef9f45";
const INSERT = [0, 1, .55];
const PARTY_CANNON = [.82, .93, .57];
const CANNON_YAW = -.20;
const CANNON_LEAN = -.48;
const CANNON_TIP = .485;
const clamp = (n) => MathUtils.clamp(n, 0, 1);
const smooth = (n, start, end) => MathUtils.smoothstep(n, start, end);
const seconds = (time) => (typeof time === "number" ? time : time?.current) ?? 0;
const damped = (age, decay, speed) => age < 0 ? 0 : Math.exp(-age * decay) * Math.cos(age * speed);

// Keep particle paths identical for replay, scrubbing and pauses.
function random(index, channel) {
  const value = Math.sin((index + 1) * 12.9898 + channel * 78.233) * 43758.5453;
  return value - Math.floor(value);
}

function graphic(draw, width, height) {
  const canvas = document.createElement("canvas");
  canvas.width = width;
  canvas.height = height;
  draw(canvas.getContext("2d"), width, height);
  const texture = new CanvasTexture(canvas);
  texture.colorSpace = SRGBColorSpace;
  return texture;
}

function useGraphic(draw, width, height) {
  const texture = useMemo(() => graphic(draw, width, height), [draw, width, height]);
  useEffect(() => () => texture.dispose(), [texture]);
  return texture;
}

function drawBanner(context, width, height) {
  const red = context.createLinearGradient(0, 0, 0, height);
  red.addColorStop(0, "#d25777");
  red.addColorStop(.44, "#a93162");
  red.addColorStop(1, "#76224f");
  context.fillStyle = red;
  context.beginPath();
  context.moveTo(0, 2);
  context.lineTo(width, 2);
  context.lineTo(width - 38, height / 2);
  context.lineTo(width, height - 2);
  context.lineTo(0, height - 2);
  context.lineTo(38, height / 2);
  context.closePath();
  context.fill();
  context.strokeStyle = "#e7a85e";
  context.lineWidth = 5;
  context.setLineDash([11, 8]);
  context.strokeRect(56, 12, width - 112, height - 24);

  context.fillStyle = "#fff1c5";
  context.font = "900 70px system-ui";
  context.textAlign = "center";
  context.textBaseline = "middle";
  context.fillText("WELCOME HOME!", width / 2, height * .52);
  for (const x of [139, width - 139]) {
    context.save();
    context.translate(x, height / 2);
    context.rotate(Math.PI / 4);
    context.fillRect(-12, -12, 24, 24);
    context.restore();
  }
}

function Banner({ time, old = false }) {
  const texture = useGraphic(drawBanner, 1100, 150);
  const prop = useRef(null);
  useFrame(() => {
    const t = seconds(time);
    const open = old ? 1 : smooth(t, .76, 1.32);
    const flutter = old ? .012 : Math.sin(t * 8) * .055 * (1 - smooth(t, 1.3, 2.6));
    prop.current.scale.y = .025 + .975 * open;
    prop.current.rotation.z = (old ? -.16 : -.065) + flutter;
    prop.current.position.y = 3.03 + .34 * (1 - open);
  });
  return <mesh ref={prop} position={[.28, 3.03, -.8]}>
    <planeGeometry args={[3.5, .50]} />
    <meshBasicMaterial map={texture} color={old ? "#958797" : "#ffffff"} transparent alphaTest={.08} side={DoubleSide} />
  </mesh>;
}

function starShape() {
  const shape = new Shape();
  for (let i = 0; i < 10; i++) {
    const angle = Math.PI / 2 + i * Math.PI / 5;
    const radius = i % 2 ? .185 : .36;
    const x = Math.cos(angle) * radius;
    const y = Math.sin(angle) * radius;
    if (i === 0) shape.moveTo(x, y);
    else shape.lineTo(x, y);
  }
  shape.closePath();
  return shape;
}

const star = starShape();
const balloonMesh = { depth: .08, bevelEnabled: true, bevelSegments: 4, steps: 1, bevelSize: .055, bevelThickness: .066 };

function FoilBalloon({ time, mode = "party", position, color = "#ffbe58", shape = "star", phase = 0, size = 1 }) {
  const root = useRef(null);
  const foil = useRef(null);
  const ribbon = useRef(null);
  useFrame(() => {
    const t = seconds(time);
    const aged = mode === "old";
    const deflate = mode === "deflate";
    const drift = aged ? .035 : .11;
    const air = deflate ? smooth(t, PROP_CUES.deflate[0], 2.1) : 0;
    const afloat = aged ? .65 : 1;
    const track = mode === "party" ? smooth(t, .35, 1.12) : 1;
    root.current.position.set(
      position[0] + Math.sin(t * 1.56 + phase) * drift + air * .4,
      position[1] + Math.cos(t * 1.9 + phase) * drift - (1 - track) * .37 - air * .78,
      position[2] + Math.sin(t * 1.22 + phase) * .035,
    );
    root.current.rotation.y = phase * .19 + Math.sin(t * 1.44 + phase) * .3;
    const losingAir = deflate ? Math.sin(air * Math.PI * 5) * air * .54 * (1 - air) : 0;
    root.current.rotation.z = Math.sin(t * 1.91 + phase) * .09 - (aged ? .22 : .05) + air * 1.1 + losingAir;
    const height = size * (afloat - air * .86);
    foil.current.scale.set(size * (1 - air * .71), height, size * (afloat - air * .91));
    ribbon.current.position.y = -.33 * height;
    ribbon.current.rotation.z = Math.sin(t * 2.2 + phase) * .14 - air * .80;
  });
  return <group ref={root} position={position}>
    <group ref={foil}>
      <mesh rotation={shape === "star" ? [0, 0, -.12] : [0, 0, 0]}>
        {shape === "star" ? <extrudeGeometry args={[star, balloonMesh]} /> : <sphereGeometry args={[.29, 20, 12]} />}
        <meshPhysicalMaterial
          color={mode === "old" ? "#95644d" : color}
          metalness={shape === "star" ? .30 : .08}
          roughness={mode === "old" ? .51 : .25}
          clearcoat={.65}
          clearcoatRoughness={.31}
          emissive={color}
          emissiveIntensity={mode === "old" ? .045 : .17}
        />
      </mesh>
      <mesh position={[0, -.29, 0]}><coneGeometry args={[.044, .11, 5]} /><meshStandardMaterial color={color} /></mesh>
    </group>
    <group ref={ribbon}>
      <mesh position={[.032, -.196, .006]} rotation={[0, 0, .16]}>
        <cylinderGeometry args={[.006, .005, .40, 5]} /><meshBasicMaterial color="#e5afca" />
      </mesh>
      <mesh position={[.045, -.51, .009]} rotation={[0, 0, -.16]}>
        <cylinderGeometry args={[.005, .005, .24, 5]} /><meshBasicMaterial color="#e5afca" />
      </mesh>
    </group>
  </group>;
}

function cannonTip(shot) {
  const position = shot === "party" ? PARTY_CANNON : INSERT;
  const scale = shot === "party" ? .86 : 1.16;
  const horizontal = Math.sin(-CANNON_LEAN) * CANNON_TIP;
  const vertical = .16 + Math.cos(CANNON_LEAN) * CANNON_TIP;
  return [
    position[0] + scale * (Math.cos(CANNON_YAW) * horizontal + Math.sin(CANNON_YAW) * .12),
    position[1] + scale * vertical,
    position[2] + scale * (.12 * Math.cos(CANNON_YAW) - Math.sin(CANNON_YAW) * horizontal),
  ];
}

function PartyCannon({ time, shot, position = INSERT, scale = 1 }) {
  const barrel = useRef(null);
  const amber = useRef(null);
  const flash = useRef(null);
  const streamer = useRef(null);
  useFrame(() => {
    const t = seconds(time);
    const hit = shot === "party" ? PROP_CUES.party[0] : PROP_CUES.jam[0];
    const recoil = shot === "party" || shot === "jam" ? damped(t - hit, 8, 22) : 0;
    barrel.current.rotation.z = (shot === "deflate" ? -.29 : CANNON_LEAN) + .13 * recoil;
    barrel.current.position.set(-.026 * recoil, .16 - .08 * recoil, .12);
    const armed = shot === "party" || shot === "jam";
    amber.current.emissiveIntensity = armed ? .55 + .45 * Math.sin(t * 10) : .12;
    const amplitude = armed ? Math.exp(-Math.pow((t - hit - .044) / .09, 2)) : 0;
    flash.current.intensity = amplitude * 7.5;
    if (streamer.current) {
      const twitch = damped(t - PROP_CUES.jam[0], 6, 23) + .69 * damped(t - PROP_CUES.jam[1], 7, 27);
      streamer.current.rotation.x = .38 + twitch * .52;
      streamer.current.scale.y = 1 + Math.max(0, twitch) * .21;
    }
  });

  return <group position={position} scale={scale} rotation={[0, CANNON_YAW, 0]}>
    {(shot === "jam" || shot === "deflate") && <mesh position={[0, -.27, -.042]}>
      <roundedBoxGeometry args={[.87, .35, .67, 2, .055]} />
      <meshStandardMaterial color="#251b38" metalness={.38} roughness={.47} />
    </mesh>}
    <mesh position={[0, -.104, .03]} castShadow>
      <roundedBoxGeometry args={[.96, .16, .68, 2, .035]} />
      <meshStandardMaterial color={INDIGO} metalness={.36} roughness={.54} />
    </mesh>
    <mesh position={[0, -.048, .033]}>
      <boxGeometry args={[.89, .04, .59]} /><meshStandardMaterial color="#735275" metalness={.28} roughness={.57} />
    </mesh>
    <mesh position={[-.35, -.068, .37]}>
      <boxGeometry args={[.12, .039, .018]} /><meshStandardMaterial ref={amber} color="#fdca67" emissive="#ec7d43" />
    </mesh>
    <mesh position={[.34, -.068, .37]}>
      <boxGeometry args={[.12, .039, .018]} /><meshBasicMaterial color="#61d8d5" />
    </mesh>
    <group ref={barrel} position={[0, .16, .12]} rotation={[0, 0, CANNON_LEAN]}>
      <mesh position={[0, .13, 0]} castShadow>
        <cylinderGeometry args={[.239, .152, .61, 28]} />
        <meshStandardMaterial color="#c44381" metalness={.24} roughness={.43} />
      </mesh>
      <mesh position={[0, .08, 0]}>
        <cylinderGeometry args={[.203, .183, .121, 28]} />
        <meshStandardMaterial color="#f18d55" metalness={.24} roughness={.36} emissive="#992a18" emissiveIntensity={.10} />
      </mesh>
      <mesh position={[0, .265, 0]}>
        <cylinderGeometry args={[.242, .224, .085, 28]} /><meshStandardMaterial color={GOLD} metalness={.44} roughness={.38} />
      </mesh>
      <mesh position={[0, .43, 0]}>
        <cylinderGeometry args={[.25, .25, .104, 32]} /><meshStandardMaterial color="#f8b35c" metalness={.43} roughness={.27} />
      </mesh>
      <mesh position={[0, CANNON_TIP, 0]} rotation={[-Math.PI / 2, 0, 0]}>
        <circleGeometry args={[.207, 28]} /><meshBasicMaterial color="#160a24" />
      </mesh>
      <mesh position={[.005, .491, 0]} rotation={[-Math.PI / 2, 0, -.35]}>
        <ringGeometry args={[.16, .183, 29]} /><meshBasicMaterial color="#a55b71" />
      </mesh>
      {(shot === "jam" || shot === "years" || shot === "return" || shot === "hearing") && <group ref={streamer} position={[.043, .50, .045]} rotation={[.38, 0, .17]}>
        {Array.from({ length: 5 }, (_, i) => <mesh
          key={i}
          position={[Math.sin(i * .83) * .047, .042 + i * .047, Math.cos(i * .83) * .045]}
          rotation={[i * .27, i * .54, Math.cos(i * .83) * .55]}
        >
          <boxGeometry args={[.075, .070, .009]} />
          <meshStandardMaterial color={shot === "jam" ? "#79e2d6" : "#5e8a97"} metalness={.22} roughness={.57} />
        </mesh>)}
      </group>}
      <mesh position={[0, -.185, .024]}>
        <sphereGeometry args={[.128, 16, 10]} /><meshStandardMaterial color="#644869" metalness={.38} roughness={.53} />
      </mesh>
    </group>
    <pointLight ref={flash} position={[.26, .67, .24]} intensity={0} color="#ffca67" distance={3.1} />
  </group>;
}

function LaunchSwitch({ time }) {
  const cap = useRef(null);
  const light = useRef(null);
  const ring = useRef(null);
  useFrame(() => {
    const t = seconds(time);
    const beat = PROP_CUES.launch[0];
    const down = smooth(t, beat - .055, beat + .04) - smooth(t, beat + .12, beat + .45);
    cap.current.position.z = .19 - down * .116;
    light.current.intensity = (Math.exp(-Math.pow((t - beat - .04) / .14, 2)) * 3.5) + (.35 + .30 * smooth(t, beat, beat + .35));
    ring.current.emissiveIntensity = .16 + smooth(t, beat, beat + .24) * (1.1 + .28 * Math.sin(t * 8));
  });
  return <group position={INSERT} rotation={[-.28, -.21, -.13]}>
    <mesh position={[0, 0, -.058]} castShadow>
      <roundedBoxGeometry args={[1.55, .83, .29, 2, .085]} />
      <meshStandardMaterial color="#37284b" metalness={.4} roughness={.46} />
    </mesh>
    <mesh position={[0, .004, .095]}>
      <roundedBoxGeometry args={[1.46, .748, .055, 2, .023]} />
      <meshStandardMaterial color="#9a665d" metalness={.47} roughness={.55} />
    </mesh>
    <mesh position={[0, .016, .13]}>
      <boxGeometry args={[1.30, .638, .028]} /><meshStandardMaterial color="#272641" metalness={.28} roughness={.5} />
    </mesh>
    <mesh position={[0, .03, .16]}>
      <torusGeometry args={[.324, .039, 11, 42]} />
      <meshStandardMaterial ref={ring} color="#ee9751" emissive="#ff6d31" emissiveIntensity={.2} metalness={.45} roughness={.35} />
    </mesh>
    <mesh ref={cap} position={[0, .03, .19]} scale={[1, 1, .47]}>
      <sphereGeometry args={[.274, 32, 16]} />
      <meshPhysicalMaterial color="#ff3a59" emissive="#d51935" emissiveIntensity={.23} roughness={.22} clearcoat={.65} />
    </mesh>
    {[-.60, .60].map((x) => <group key={x} position={[x, 0, .148]}>
      <mesh position={[0, .19, 0]}><circleGeometry args={[.026, 8]} /><meshBasicMaterial color="#d99f64" /></mesh>
      <mesh position={[0, -.18, 0]}><circleGeometry args={[.026, 8]} /><meshBasicMaterial color="#c59879" /></mesh>
      <mesh><boxGeometry args={[.06, .035, .016]} /><meshBasicMaterial color={x < 0 ? "#8c709e" : "#78cbd0"} /></mesh>
    </group>)}
    <pointLight ref={light} color="#ff5060" position={[0, .1, .62]} distance={2.65} intensity={.25} />
  </group>;
}

function ParticleBurst({ time, shot }) {
  const sparks = shot !== "party";
  const size = sparks ? 42 : 164;
  const instances = useRef(null);
  const dummy = useMemo(() => new Object3D(), []);
  const tint = useMemo(() => new Color(), []);
  const elapsedOffset = PROP_CUES[shot][0];
  const origin = useMemo(() => cannonTip(shot), [shot]);
  useEffect(() => {
    const mesh = instances.current;
    mesh.instanceMatrix.setUsage(DynamicDrawUsage);
    for (let i = 0; i < size; i++) {
      const shade = sparks ? (i % 4 === 0 ? "#fff5c8" : "#ff884a") : PAPER[i % PAPER.length];
      mesh.setColorAt(i, tint.set(shade));
    }
    if (mesh.instanceColor) mesh.instanceColor.needsUpdate = true;
  }, [size, sparks, tint]);

  const paths = useMemo(() => Array.from({ length: size }, (_, i) => {
    const fan = random(i, 5) - .5;
    return {
      start: elapsedOffset + (shot === "jam" && i > size / 2 ? .5 : 0) + random(i, 1) * .14,
      dx: shot === "party" ? -2.3 + fan * 6.8 : .44 + fan * 1.77,
      dy: shot === "party" ? 2.64 + 2.9 * random(i, 6) : .48 + random(i, 6) * 2.2,
      dz: -.7 + random(i, 7) * (shot === "party" ? 3.5 : 1.12),
      scale: .56 + random(i, 3) * .87,
      spin: (random(i, 2) - .5) * 15,
      life: sparks ? .24 + random(i, 4) * .37 : 1.38 + random(i, 4) * .74,
    };
  }), [elapsedOffset, shot, size, sparks]);

  useFrame(() => {
    const t = seconds(time);
    const mesh = instances.current;
    let visible = 0;
    for (let i = 0; i < size; i++) {
      const path = paths[i];
      const age = t - path.start;
      if (age < 0 || age > path.life) continue;
      const ease = (1 - Math.exp(-age * 1.9)) / 1.9;
      const grav = sparks ? 2.9 : 2.2;
      dummy.position.set(
        origin[0] + path.dx * ease + Math.sin(path.spin + age * 8) * .065 * age,
        origin[1] + path.dy * ease - grav * age * age * .5,
        origin[2] + path.dz * ease,
      );
      const falloff = 1 - smooth(age, path.life * .72, path.life);
      const scale = path.scale * falloff;
      if (sparks) {
        dummy.rotation.set(0, 0, -Math.atan2(path.dx, path.dy - grav * age));
        dummy.scale.set(.016 * scale, .18 * scale, .016 * scale);
      } else {
        dummy.rotation.set(i * .3 + age * path.spin * .68, path.spin * age, i + path.spin * age * .71);
        dummy.scale.set(.060 * scale, .135 * scale, .012 * scale);
      }
      dummy.updateMatrix();
      mesh.setColorAt(visible, tint.set(sparks ? (i % 4 === 0 ? "#fff5c8" : "#ff884a") : PAPER[i % PAPER.length]));
      mesh.setMatrixAt(visible++, dummy.matrix);
    }
    mesh.count = visible;
    mesh.instanceMatrix.needsUpdate = true;
    if (mesh.instanceColor) mesh.instanceColor.needsUpdate = true;
  });

  return <instancedMesh ref={instances} args={[undefined, undefined, size]} count={0} frustumCulled={false}>
    <boxGeometry args={[1, 1, 1]} />
    <meshBasicMaterial color="white" toneMapped={false} />
  </instancedMesh>;
}

function drawBloom(context, width, height) {
  const gradient = context.createRadialGradient(width / 2, height / 2, 0, width / 2, height / 2, width / 2);
  gradient.addColorStop(0, "#fff9da");
  gradient.addColorStop(.13, "#ffd399ca");
  gradient.addColorStop(.42, "#f7954a48");
  gradient.addColorStop(1, "#880c0200");
  context.fillStyle = gradient;
  context.fillRect(0, 0, width, height);
}

function MuzzleFlash({ time, shot }) {
  const sprite = useRef(null);
  const texture = useGraphic(drawBloom, 128, 128);
  useFrame(() => {
    const t = seconds(time);
    const start = PROP_CUES[shot][0];
    const second = shot === "jam" ? PROP_CUES.jam[1] : -1;
    const main = Math.exp(-Math.pow((t - start - .037) / .076, 2));
    const repeat = second > 0 ? Math.exp(-Math.pow((t - second - .025) / .051, 2)) * .53 : 0;
    const opacity = Math.min(1, main + repeat);
    sprite.current.visible = opacity > .006;
    sprite.current.material.opacity = opacity;
    sprite.current.scale.setScalar((shot === "jam" ? .56 : 1.02) + opacity * .40);
  });
  const position = cannonTip(shot);
  position[1] += .07;
  position[2] += .04;
  return <sprite ref={sprite} position={position}>
    <spriteMaterial map={texture} transparent blending={AdditiveBlending} depthWrite={false} toneMapped={false} />
  </sprite>;
}

function drawLampHaze(context, width, height) {
  const gradient = context.createRadialGradient(width / 2, height / 2, 0, width / 2, height / 2, width / 2);
  gradient.addColorStop(0, "#cec7eaeb");
  gradient.addColorStop(.30, "#a99cba5e");
  gradient.addColorStop(.68, "#655b8a23");
  gradient.addColorStop(1, "#73628b00");
  context.fillStyle = gradient;
  context.fillRect(0, 0, width, height);
}

function Aftermath({ time }) {
  const clouds = useRef([]);
  const texture = useGraphic(drawLampHaze, 96, 96);
  const origin = useMemo(() => cannonTip("jam"), []);
  useFrame(() => {
    const t = seconds(time);
    for (let i = 0; i < clouds.current.length; i++) {
      const sprite = clouds.current[i];
      const age = t - PROP_CUES.jam[0] - i * .125;
      const since = clamp(age / 1.36);
      const rise = Math.max(0, age);
      sprite.material.opacity = smooth(age, 0, .25) * (1 - since) * .4;
      sprite.position.set(origin[0] + rise * .16 + Math.sin(i + rise * 3) * .08, origin[1] + rise * .64, origin[2] - i * .022);
      sprite.scale.setScalar(.15 + rise * .52);
    }
  });
  return <>{Array.from({ length: 7 }, (_, i) =>
    <sprite key={i} ref={(object) => { clouds.current[i] = object; }}>
      <spriteMaterial transparent depthWrite={false} map={texture} opacity={0} />
    </sprite>
  )}</>;
}

function Dust({ time }) {
  const ref = useRef(null);
  const positions = useMemo(() => new Float32Array(Array.from({ length: 265 }, (_, i) => [
    -5.6 + 11.2 * random(i, 10), .14 + 4.9 * random(i, 12), -3.1 + 5.8 * random(i, 13),
  ]).flat()), []);
  useFrame(() => {
    const t = seconds(time);
    ref.current.position.set(.085 * t, .046 * Math.sin(t), .015 * t);
  });
  return <points ref={ref}>
    <bufferGeometry><bufferAttribute attach="attributes-position" args={[positions, 3]} /></bufferGeometry>
    <pointsMaterial color="#dac8db" size={.025} transparent opacity={.37} sizeAttenuation depthWrite={false} />
  </points>;
}

function drawControl(context, width, height) {
  context.fillStyle = "#0e162b";
  context.fillRect(0, 0, width, height);
  context.strokeStyle = "#234252";
  context.lineWidth = 1;
  for (let x = 0; x < width; x += 16) { context.beginPath(); context.moveTo(x, 0); context.lineTo(x, height); context.stroke(); }
  for (let y = 0; y < height; y += 16) { context.beginPath(); context.moveTo(0, y); context.lineTo(width, y); context.stroke(); }
  const points = [[0,41],[34,42],[46,20],[56,52],[71,41],[104,41],[129,32],[141,47],[163,41],[191,42],[203,23],[213,53],[234,40],[256,40]];
  context.strokeStyle = "#6de9da";
  context.lineWidth = 3;
  context.beginPath();
  points.forEach(([x, y], i) => i ? context.lineTo(x, y) : context.moveTo(x, y));
  context.stroke();
}

function Console({ time, position = [0, .94, 1.27], variant = "flight" }) {
  const panel = useGraphic(drawControl, 256, 72);
  const status = useRef(null);
  useFrame(() => {
    const t = seconds(time);
    status.current.emissiveIntensity = .38 + (.6 + .37 * Math.sin(t * 4)) * .32;
  });
  return <group position={position}>
    <mesh position={[0, -.705, -.076]}>
      <roundedBoxGeometry args={[2.74, .60, .53, 2, .095]} />
      <meshStandardMaterial color={variant === "lab" ? "#0e2b3d" : "#211a36"} roughness={.59} metalness={.44} />
    </mesh>
    <mesh position={[0, -.32, 0]} rotation={[-.18, 0, 0]}>
      <roundedBoxGeometry args={[4.08, .53, .64, 2, .081]} />
      <meshStandardMaterial color={variant === "lab" ? "#163c52" : INDIGO} roughness={.49} metalness={.42} />
    </mesh>
    <mesh position={[0, -.068, .029]} rotation={[-.21, 0, 0]}>
      <roundedBoxGeometry args={[4.11, .11, .69, 2, .038]} />
      <meshStandardMaterial color={variant === "lab" ? "#476980" : "#675278"} metalness={.32} roughness={.5} />
    </mesh>
    <mesh position={[0, -.33, .326]} rotation={[-.18, 0, 0]}>
      <roundedBoxGeometry args={[3.86, .285, .038, 2, .013]} />
      <meshStandardMaterial color={variant === "lab" ? "#174750" : "#353255"} metalness={.53} roughness={.45} />
    </mesh>
    <mesh position={[0, -.384, .367]} rotation={[-.18, 0, 0]}>
      <boxGeometry args={[3.47, .014, .014]} />
      <meshBasicMaterial color={variant === "lab" ? "#3babbb" : "#c68fa2"} />
    </mesh>
    {[-1.53, 1.29].map((start) => <group key={start} position={[start, -.277, .344]} rotation={[-.18, 0, 0]}>
      {Array.from({ length: 5 }, (_, slot) => <mesh key={slot} position={[.065 * slot, 0, 0]}>
        <boxGeometry args={[.025, .07, .016]} />
        <meshBasicMaterial color={slot === 0 ? "#d79954" : "#100f24"} />
      </mesh>)}
    </group>)}
    <mesh position={[.06, .001, .24]} rotation={[-1.28, 0, 0]}>
      <planeGeometry args={[.91, .25]} /><meshBasicMaterial map={panel} toneMapped={false} />
    </mesh>
    {[-1.65, -1.45, -1.24, -1.04, -.84, .89, 1.09, 1.29, 1.49, 1.69].map((x, index) =>
      <mesh key={x} position={[x, .008, .21]} rotation={[-.2, 0, 0]}>
        <boxGeometry args={[.106, .045, index === 6 ? .15 : .11]} />
        <meshStandardMaterial ref={index === 6 ? status : undefined} color={PAPER[index % PAPER.length]} emissive={PAPER[index % PAPER.length]} emissiveIntensity={.17} />
      </mesh>
    )}
  </group>;
}

function drawReport(context, width, height) {
  context.fillStyle = "#ffe8ba";
  context.fillRect(0, 0, width, height);
  context.fillStyle = "#ae694f";
  context.fillRect(0, 0, width, 13);
  context.fillStyle = "#522d50";
  [36, 62, 89, 119, 148].forEach((y, index) => {
    context.fillRect(26, y, index % 2 ? width * .58 : width * .76, index === 0 ? 6 : 4);
  });
  context.save();
  context.translate(width / 2, height * .70);
  context.rotate(-.15);
  context.strokeStyle = "#a73560";
  context.lineWidth = 5;
  context.strokeRect(-width * .385, -39, width * .77, 76);
  context.fillStyle = "#a73560";
  context.textAlign = "center";
  context.textBaseline = "middle";
  context.font = "900 44px system-ui";
  context.fillText("SIMULATION", 0, 0);
  context.restore();
}

function Report({ time }) {
  const prop = useRef(null);
  const texture = useGraphic(drawReport, 480, 300);
  useFrame(() => {
    const t = seconds(time);
    const presented = smooth(t, .25, .82);
    const corrected = smooth(t, 2.12, 2.72);
    prop.current.position.set(-1.0 + presented * .32 + corrected * .41, .98 + presented * .45, 1.52 + corrected * .32);
    prop.current.rotation.set(-.56 + presented * .37 + corrected * .22, -.46 + corrected * .4, .16 - corrected * .23);
  });
  return <group ref={prop} position={[-1, .98, 1.52]}>
    <mesh position={[0, 0, -.025]}>
      <roundedBoxGeometry args={[1.26, .81, .065, 2, .025]} /><meshStandardMaterial color="#584264" roughness={.56} />
    </mesh>
    <mesh position={[0, 0, .013]}>
      <planeGeometry args={[1.15, .719]} />
      <meshBasicMaterial map={texture} color="#f8e7dc" />
    </mesh>
    <mesh position={[0, .371, .027]}>
      <roundedBoxGeometry args={[.22, .073, .05, 2, .017]} /><meshStandardMaterial color="#d7a473" roughness={.4} metalness={.44} />
    </mesh>
  </group>;
}

export function FilmProps({ shot: currentShot, time }) {
  const shot = typeof currentShot === "string" ? currentShot : currentShot?.id;
  const party = shot === "party";
  const jam = shot === "jam";
  const old = shot === "years" || shot === "return";
  const lab = shot === "lab";
  const hearing = shot === "hearing";
  const deflate = shot === "deflate";

  return <>
    {(party || jam) && <>
      <PartyCannon shot={shot} time={time} position={party ? PARTY_CANNON : INSERT} scale={party ? .86 : 1.16} />
      <ParticleBurst shot={shot} time={time} />
      <MuzzleFlash shot={shot} time={time} />
    </>}
    {jam && <Aftermath time={time} />}
    {(party || old) && <>
      <Banner time={time} old={old} />
      <FoilBalloon time={time} mode={old ? "old" : "party"} position={[2.75, 2.43, -.72]} phase={.6} />
      <FoilBalloon time={time} mode={old ? "old" : "party"} position={[-2.74, 2.52, -.83]} color="#f0659e" shape="sphere" phase={2} size={.88} />
      {party && <FoilBalloon time={time} position={[2.20, 2.69, -.77]} color="#7b72f1" shape="sphere" phase={3} size={.76} />}
    </>}
    {old && <>
      <PartyCannon shot={shot} time={time} position={PARTY_CANNON} scale={.86} />
      <Dust time={time} />
    </>}
    {shot === "launch" && <LaunchSwitch time={time} />}
    {(shot === "reveal" || shot === "correction" || party || old || lab || hearing) &&
      <Console time={time} variant={lab ? "lab" : "flight"} />
    }
    {hearing && <>
      <FoilBalloon time={time} mode="old" position={[1.47, 2.12, -.11]} phase={1.1} size={.82} />
      <PartyCannon shot={shot} time={time} position={[-1.60, 1.01, 1.32]} scale={.61} />
      <Report time={time} />
    </>}
    {deflate && <>
      <PartyCannon shot={shot} time={time} position={[0, .46, .68]} scale={.72} />
      <FoilBalloon time={time} mode="deflate" position={[.13, 1.38, .57]} phase={.64} size={1.27} />
    </>}
  </>;
}
