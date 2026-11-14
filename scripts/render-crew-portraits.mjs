import { spawn } from "node:child_process";
import { createServer } from "node:http";
import { mkdir, mkdtemp, readFile, rm, writeFile } from "node:fs/promises";
import { existsSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const project = fileURLToPath(new URL("../", import.meta.url));
const crew = {
  "commander-zorvax": { angle: -.27, frame: 1.78, key: 0xffe4bf, rim: 0xe8a0ff },
  "dr-nyxen": { angle: .24, frame: 1.68, key: 0xbceafa, rim: 0x9cbafa },
  "alien-student": { angle: -.22, frame: 1.42, key: 0xffe0a7, rim: 0x9ef3ff },
};

async function renderPortraits(THREE, GLTFLoader, characters) {
  const width = 520;
  const height = 576;
  const aspect = width / height;
  const renderer = new THREE.WebGLRenderer({ alpha: true, antialias: true, preserveDrawingBuffer: true });
  renderer.setSize(width, height);
  renderer.setClearColor(0x000000, 0);
  renderer.toneMapping = THREE.NeutralToneMapping;
  renderer.toneMappingExposure = 1.08;

  function light(color, intensity, position) {
    const source = new THREE.DirectionalLight(color, intensity);
    source.position.set(...position);
    return source;
  }

  for (const [name, { angle, frame, key, rim }] of Object.entries(characters)) {
    const { scene: model, animations } = await new GLTFLoader().loadAsync('/assets/characters/' + name + '.glb');
    const scene = new THREE.Scene();
    scene.add(model, new THREE.HemisphereLight(0xdbdbff, 0x483040, 1.4));
    scene.add(light(key, 2.3, [-3, 5, 4]));
    scene.add(light(0xa5b5ff, .9, [3, 2, 4]));
    scene.add(light(rim, 3.2, [1, 3, -3]));
    const idle = animations.find((clip) => clip.name === "Idle");
    if (idle) {
      const mixer = new THREE.AnimationMixer(model);
      mixer.clipAction(idle).play();
      mixer.setTime(idle.duration * .37);
    }

    const bounds = new THREE.Box3().setFromObject(model, true);
    const subject = bounds.getCenter(new THREE.Vector3());
    subject.x = 0;
    subject.y = bounds.max.y - frame * .36;
    subject.z = 0;
    const camera = new THREE.OrthographicCamera(-frame * aspect / 2, frame * aspect / 2, frame / 2, -frame / 2, .1, 16);
    camera.position.copy(subject).add(new THREE.Vector3(Math.sin(angle) * 5, .35, Math.cos(angle) * 5));
    camera.lookAt(subject);
    renderer.render(scene, camera);
    const portrait = await new Promise((done) => renderer.domElement.toBlob(done, "image/webp", .94));
    if (portrait?.type !== "image/webp") throw new Error("Could not render " + name);
    const saved = await fetch("/portraits/" + name, { method: "POST", body: portrait });
    if (!saved.ok) throw new Error("Could not submit " + name);
  }
  renderer.dispose();
}

const chrome = process.env.CHROME_PATH ??
  (process.platform === "darwin" ? "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome" : "chromium");
if (process.platform === "darwin" && !existsSync(chrome)) {
  throw new Error("Set CHROME_PATH to the Chrome or Chromium executable.");
}

const portraits = new Map();
let finish;
let fail;
const complete = new Promise((done, reject) => { finish = done; fail = reject; });
const page = `<!doctype html><html lang="en"><title>Crew portraits</title>
<script type="importmap">{"imports":{"three":"/three/build/three.module.js","three/addons/":"/three/examples/jsm/"}}</script>
<script type="module">
  import * as THREE from "three";
  import { GLTFLoader } from "three/addons/loaders/GLTFLoader.js";
  await (${renderPortraits.toString()})(THREE, GLTFLoader, ${JSON.stringify(crew)})
    .catch((error) => fetch("/error", { method: "POST", body: String(error) }));
</script>`;

const server = createServer(async (request, response) => {
  try {
    const path = new URL(request.url, "http://localhost").pathname;
    if (path === "/") {
      response.writeHead(200, { "Content-Type": "text/html" }).end(page);
      return;
    }
    if (request.method === "POST") {
      const model = path.split("/").at(-1);
      if (path !== "/error" && (!path.startsWith("/portraits/") || !Object.hasOwn(crew, model))) {
        response.writeHead(404).end();
        return;
      }
      const chunks = [];
      for await (const chunk of request) chunks.push(chunk);
      const data = Buffer.concat(chunks);
      response.writeHead(204).end();
      if (path === "/error") fail(new Error(data.toString()));
      else {
        portraits.set(model, data);
        if (portraits.size === Object.keys(crew).length) finish();
      }
      return;
    }

    let resource;
    if (/^\/three\/(?:build|examples\/jsm)\/[\w./-]+\.js$/.test(path) && !path.includes("..")) resource = "node_modules" + path;
    if (/^\/assets\/characters\/[\w-]+\.glb$/.test(path)) resource = "public" + path;
    if (!resource) { response.writeHead(404).end(); return; }
    const bytes = await readFile(resolve(project, resource));
    response.writeHead(200, { "Content-Type": path.endsWith(".js") ? "text/javascript" : "model/gltf-binary" }).end(bytes);
  } catch (error) {
    response.writeHead(500).end();
    fail(error);
  }
});

const profile = await mkdtemp(join(tmpdir(), "homecoming-portraits-"));
await new Promise((done) => server.listen(0, "127.0.0.1", done));
const browser = spawn(chrome, ["--headless", "--use-angle=swiftshader", "--enable-unsafe-swiftshader",
  "--disable-background-networking", "--no-first-run",
  "--user-data-dir=" + profile,
  "http://127.0.0.1:" + server.address().port], { stdio: "ignore" });
browser.once("error", fail);
browser.once("exit", (code) => fail(new Error("Chrome exited before portrait rendering finished (" + code + ").")));
const timeout = setTimeout(() => fail(new Error("Portrait rendering timed out.")), 45000);
try {
  await complete;
  const destination = resolve(project, "public/homecoming/crew");
  await mkdir(destination, { recursive: true });
  for (const [name, image] of portraits) {
    const target = join(destination, name + ".webp");
    await writeFile(target, image);
    process.stdout.write(target + "\n");
  }
} finally {
  clearTimeout(timeout);
  server.close();
  server.closeAllConnections();
  if (browser.pid && browser.exitCode === null && browser.signalCode === null) {
    const exited = new Promise((done) => browser.once("exit", done));
    browser.kill();
    await exited;
  }
  await rm(profile, { recursive: true, force: true });
}
