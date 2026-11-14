"use client";

import { useLayoutEffect, useMemo, useRef } from "react";
import { useFrame, useLoader } from "@react-three/fiber";
import { AnimationMixer, LoopOnce, LoopRepeat, MathUtils, Quaternion, Vector3 } from "three";
import { GLTFLoader } from "three/addons/loaders/GLTFLoader.js";
import { clone } from "three/addons/utils/SkeletonUtils.js";
import { CREW } from "./Crew";

const MAIN_CREW = ["commander", "scientist", "cadet"];
const IDS = [...MAIN_CREW, "bogos"];
const paths = IDS.map((id) => `/assets/characters/${CREW[id].asset}.glb`);
const height = { commander: 2.9, scientist: 3.03, cadet: 2.35, bogos: 1.82 };
const stageX = { commander: -1.38, scientist: .35, cadet: 2.05, bogos: -2.88 };
const up = new Vector3(0, 1, 0);
const right = new Vector3(1, 0, 0);
const front = new Vector3(0, 0, 1);

const gestures = {
  reveal: ["Welcome", "Idle", "Idle"],
  correction: ["Nod", "Eureka", "Idle"],
  party: ["Welcome", "Explain", "Wave", "Snap"],
  years: ["Idle", "Idle", "Idle"],
  return: ["Nod", "Explain", "Idle"],
  launch: ["Nod", "Idle", "Idle"],
  lab: ["Idle", "Explain", "Idle"],
  hearing: ["Welcome", "Eureka", "Idle"],
};
const visible = {
  reveal: IDS, correction: ["commander", "scientist"], party: IDS, years: IDS, return: IDS, earth: [],
  launch: ["commander"], lab: ["scientist", "cadet"], hearing: MAIN_CREW,
};
const smooth = (from, to, t) => MathUtils.smoothstep(t, from, to);
const hit = (at, span, t) => Math.exp(-Math.pow((t - at) / span, 2));

function createActor(gltf) {
  const scene = clone(gltf.scene);
  const mixer = new AnimationMixer(scene);
  scene.traverse((obj) => { if (obj.isSkinnedMesh) obj.frustumCulled = false; });
  const find = (name) => scene.getObjectByName(name) ?? scene.getObjectByName(name.replace(".", ""));
  return {
    scene,
    mixer,
    actions: Object.fromEntries(gltf.animations.map((clip) => [clip.name, mixer.clipAction(clip)])),
    head: find("head"),
    chest: find("chest") ?? find("spine"),
    arm: find("upper_arm.R") ?? find("upper.R"),
    pressArm: find("upper_arm.L"),
    pressForearm: find("forearm.L"),
    pressHand: find("hand.L"),
    worldInverse: new Quaternion(),
    worldRotation: new Quaternion(),
    offset: new Quaternion(),
    axis: new Vector3(),
    pivot: new Vector3(),
    from: new Vector3(),
    to: new Vector3(),
    contact: new Vector3(),
  };
}

function turnBone(actor, bone, worldAxis, angle) {
  if (!bone?.parent || !angle) return;
  bone.parent.getWorldQuaternion(actor.worldInverse).invert();
  actor.axis.copy(worldAxis).applyQuaternion(actor.worldInverse);
  bone.quaternion.premultiply(actor.offset.setFromAxisAngle(actor.axis, angle));
}

function reachForSwitch(actor, time) {
  const { pressArm, pressForearm, pressHand, pivot, from, to, contact, worldInverse, worldRotation, offset } = actor;
  if (!pressArm || !pressForearm || !pressHand) return;
  const reach = smooth(.12, .48, time) * (1 - smooth(.92, 1.43, time));
  if (reach < .001) return;
  const down = smooth(.54, .77, time);
  contact.set(MathUtils.lerp(-.39, -.025, down), MathUtils.lerp(1.34, 1.08, down), MathUtils.lerp(1.37, .865, down));
  for (let step = 0; step < 4; step++) {
    for (const joint of [pressForearm, pressArm]) {
      joint.getWorldPosition(pivot);
      pressHand.getWorldPosition(from);
      to.copy(contact).sub(pivot).normalize();
      from.sub(pivot).normalize();
      offset.setFromUnitVectors(from, to);
      worldRotation.identity().slerp(offset, reach * .60);
      joint.parent.getWorldQuaternion(offset);
      worldInverse.copy(offset).invert();
      joint.quaternion.premultiply(worldRotation.premultiply(worldInverse).multiply(offset));
      joint.updateMatrixWorld(true);
    }
  }
}

function Actor({ id, source, index, shot, time }) {
  const root = useRef(null);
  const rig = useMemo(() => createActor(source), [source]);
  const rigRef = useRef(null);
  const active = useRef("");
  useLayoutEffect(() => { rigRef.current = rig; }, [rig]);
  useFrame(() => {
    const group = root.current;
    const actor = rigRef.current;
    if (!actor) return;
    const t = time.current;
    const idShot = shot.id;
    group.visible = (visible[idShot] ?? []).includes(id);
    if (!group.visible) return;
    const name = gestures[idShot]?.[index] ?? "Idle";
    if (active.current !== idShot) {
      active.current = idShot;
      actor.mixer.stopAllAction();
      const action = actor.actions[name] ?? actor.actions.Idle;
      action.reset().setLoop(name === "Idle" ? LoopRepeat : LoopOnce, name === "Idle" ? Infinity : 1);
      action.clampWhenFinished = true;
      action.play();
    }
    const action = actor.actions[name] ?? actor.actions.Idle;
    const clipLength = action.getClip().duration;
    const delay = id === "bogos" ? .82 : idShot === "reveal" ? 1.46 : idShot === "correction" && index === 1 ? .44 : idShot === "hearing" && index === 1 ? 2.13 : .16;
    action.time = name === "Idle" ? (t + index * .39) % clipLength : Math.min(clipLength - .001, Math.max(0, t - delay));
    actor.mixer.update(0);

    const tired = idShot === "years" ? 1 : 0;
    const dancing = idShot === "party" ? smooth(.63, 1, t) : 0;
    const bounce = dancing * Math.sin(t * 8.05 + index * 1.7);
    const lean = idShot === "reveal" && index === 0 ? smooth(1.22, 2.22, t) : 0;
    const returnTurn = idShot === "return" ? smooth(.43 + index * .11, 1.20 + index * .12, t) : 0;
    let x = stageX[id] - lean * .31 + bounce * .12;
    let y = dancing * Math.max(0, bounce) * .19;
    let z = [1.07, .38, -.08, .4][index] + lean * .47;
    let yaw = [.28, -.39, -.55, .20][index] + bounce * .17;
    let pitch = -tired * [.14, .06, .18, .1][index] + returnTurn * .18;
    let look = returnTurn * .62 - tired * .12;
    if (idShot === "lab") {
      x = index === 1 ? -1.72 : 1.9;
      z = index === 1 ? .34 : .5;
      yaw = index === 1 ? .5 : -.58;
    }
    if (idShot === "correction") {
      look = index === 1 ? -.28 : .28 * smooth(.22, .54, t);
      pitch += index === 0 ? .17 * hit(1.25, .5, t) : 0;
    }
    if (idShot === "hearing") {
      x += index === 0 ? .15 : 0;
      look = index === 0 ? .38 * smooth(2.3, 3.03, t) : -.28;
      pitch += index === 0 ? -.21 * smooth(2.3, 3.2, t) : 0;
    }
    if (idShot === "launch") {
      x = -.92; y = -.52; z = .74; yaw = .35; pitch = -.18;
    }

    group.position.set(x, y, z);
    group.rotation.set(0, yaw, -.075 * bounce + tired * (index === 2 ? .135 : -.045));
    group.updateMatrixWorld(true);
    turnBone(actor, actor.chest, front, -.085 * bounce - tired * .058);
    turnBone(actor, actor.head, up, look);
    turnBone(actor, actor.head, right, pitch);
    if (idShot === "party") {
      turnBone(actor, actor.head, front, .085 * Math.cos(t * 8.05 + index));
      turnBone(actor, actor.arm, front, (index === 1 ? -.23 : .35) * bounce);
    }
    if (idShot === "launch" && index === 0) reachForSwitch(actor, t);
  });
  return <group ref={root} visible={false} scale={2.17 / height[id] * (id === "bogos" ? .72 : id === "cadet" ? .79 : 1)}>
    <primitive object={rig.scene} dispose={null} />
  </group>;
}

export function FilmActors({ shot, time }) {
  const sources = useLoader(GLTFLoader, paths);
  return IDS.map((id, index) => <Actor key={id} id={id} index={index} source={sources[index]} shot={shot} time={time} />);
}
