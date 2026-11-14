import { writeFile } from "node:fs/promises";
import {
  AnimationClip, BoxGeometry, CylinderGeometry, Euler, Group, Mesh, MeshStandardMaterial,
  Quaternion, QuaternionKeyframeTrack, SphereGeometry, VectorKeyframeTrack,
} from "three";
import { GLTFExporter } from "three/addons/exporters/GLTFExporter.js";

// The exporter uses FileReader to pack the GLB. Node already provides Blob.
globalThis.FileReader = class {
  async readAsArrayBuffer(blob) {
    this.result = await blob.arrayBuffer();
    this.onloadend();
  }
};

const material = (color, properties) => new MeshStandardMaterial({ color, roughness: .62, ...properties });
const teal = material("#68c8a8");
const mint = material("#a2ebc4");
const grape = material("#514578");
const lilac = material("#9877ba");
const graphite = material("#211c38");
const brass = material("#e4b456", { metalness: .25 });
const ivory = material("#fff1d1");
const lens = material("#4facbd", { metalness: .35, roughness: .2 });
const flash = material("#fcf5cf", { emissive: "#ffdf8f", emissiveIntensity: 1 });

const sphere = new SphereGeometry(1, 24, 16);
const box = new BoxGeometry(1, 1, 1);
const barrel = new CylinderGeometry(1, 1, 1, 24);
function part(parent, geometry, finish, position, scale) {
  const mesh = new Mesh(geometry, finish);
  mesh.position.set(...position);
  mesh.scale.set(...scale);
  parent.add(mesh);
  return mesh;
}
const oval = (parent, finish, pos, size) => part(parent, sphere, finish, pos, size);
const block = (parent, finish, pos, size) => part(parent, box, finish, pos, size);
function joint(parent, name, position = [0, 0, 0]) {
  const node = new Group();
  node.name = name;
  node.position.set(...position);
  parent.add(node);
  return node;
}

const bogos = new Group();
bogos.name = "Bogos Binted";
const body = joint(bogos, "Body");
oval(body, grape, [0, .63, 0], [.31, .41, .22]);
oval(body, lilac, [0, .81, .03], [.32, .18, .23]);
oval(body, brass, [0, .99, 0], [.27, .073, .23]);
for (const side of [-1, 1]) {
  oval(body, graphite, [side * .16, .22, 0], [.125, .21, .14]);
  oval(body, grape, [side * .16, .105, .10], [.15, .105, .22]);
  oval(body, brass, [side * .16, .092, .25], [.091, .047, .05]);
}

const head = joint(body, "Head", [0, 1.29, .015]);
oval(head, teal, [0, 0, 0], [.45, .34, .31]);
for (const side of [-1, 1]) {
  const ear = oval(head, teal, [side * .44, .01, -.015], [.19, .085, .12]);
  ear.rotation.z = side * .38;
}
oval(head, mint, [0, -.12, .175], [.25, .17, .158]);
oval(head, graphite, [0, -.125, .327], [.078, .045, .012]);
for (const [x, y, z, s] of [[-.192, .032, .249, 1], [.192, .032, .249, 1], [0, .173, .255, .71]]) {
  oval(head, ivory, [x, y, z], [.105 * s, .124 * s, .066 * s]);
  oval(head, graphite, [x + .012, y, z + .053 * s], [.062 * s, .079 * s, .021 * s]);
  oval(head, ivory, [x - .003, y + .029 * s, z + .072 * s], [.018 * s, .018 * s, .008 * s]);
}
const antenna = oval(head, teal, [0, .35, -.016], [.035, .135, .036]);
antenna.rotation.z = -.30;
oval(head, brass, [.036, .463, -.016], [.071, .066, .071]);

const camera = joint(body, "CameraRig", [0, .65, .24]);
for (const side of [-1, 1]) {
  const sleeve = oval(body, lilac, [side * .29, .67, .13], [.11, .22, .12]);
  sleeve.rotation.x = -.55;
  sleeve.rotation.z = side * -.27;
  oval(camera, mint, [side * .179, -.013, .157], [.07, .078, .074]);
}
block(camera, graphite, [0, 0, .12], [.32, .22, .14]);
block(camera, brass, [0, .097, .127], [.31, .065, .14]);
block(camera, ivory, [.103, .165, .113], [.13, .087, .055]);
block(camera, flash, [.103, .165, .144], [.104, .063, .01]);
part(camera, barrel, brass, [0, -.013, .208], [.098, .061, .098]).rotation.x = Math.PI / 2;
part(camera, barrel, lens, [0, -.013, .241], [.077, .009, .077]).rotation.x = Math.PI / 2;
const flare = oval(camera, flash, [.103, .165, .191], [0, 0, 0]);
flare.name = "Flash";

const rotation = (node, times, angles) => new QuaternionKeyframeTrack(node.name + ".quaternion", times,
  angles.flatMap((angle) => new Quaternion().setFromEuler(new Euler(...angle)).toArray()));
const vectors = (node, property, times, values) => new VectorKeyframeTrack(node.name + "." + property, times, values.flat());
const animations = [
  new AnimationClip("Idle", 2.4, [
    rotation(head, [0, 1.2, 2.4], [[0, -.12, .04], [.05, .14, -.05], [0, -.12, .04]]),
    vectors(body, "scale", [0, 1.2, 2.4], [[1, 1, 1], [1, 1.018, 1], [1, 1, 1]]),
  ]),
  new AnimationClip("Snap", 1.6, [
    rotation(head, [0, .48, .78, 1.6], [[0, -.12, .04], [-.08, 0, -.06], [.10, 0, .02], [0, -.12, .04]]),
    rotation(camera, [0, .48, .72, .80, 1.6], [[.20, -.20, -.06], [0, 0, 0], [0, 0, 0], [-.19, 0, -.04], [.20, -.20, -.06]]),
    vectors(flare, "scale", [0, .69, .72, .79, 1.6], [[0, 0, 0], [0, 0, 0], [.13, .10, .025], [0, 0, 0], [0, 0, 0]]),
  ]),
];

const glb = await new GLTFExporter().parseAsync(bogos, { binary: true, animations });
await writeFile(new URL("../public/assets/characters/bogos-binted.glb", import.meta.url), Buffer.from(glb));
