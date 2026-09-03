import './styles.css';
import * as THREE from 'three';
import { OrbitControls } from 'three/examples/jsm/controls/OrbitControls.js';
import { GLTFLoader } from 'three/examples/jsm/loaders/GLTFLoader.js';
import { acceleratedRaycast, computeBoundsTree, disposeBoundsTree } from 'three-mesh-bvh';
import {
  Box,
  Database,
  MousePointer2,
  Scan,
  Search,
  Upload,
  X,
  createIcons
} from 'lucide';

type ElementSummary = {
  id: string;
  number: string;
  sourceType: string;
  system: string;
  subSystem: string;
  layerName: string;
  style: string;
};

type SceneChunk = {
  id: string;
  uri: string;
  elementCount: number;
};

type SceneManifest = {
  sceneId: string;
  name: string;
  elementCount: number;
  typeCounts: Record<string, number>;
  chunks: SceneChunk[];
  elements: ElementSummary[];
};

type HighlightedMesh = {
  mesh: THREE.Mesh;
  original: THREE.Material | THREE.Material[];
  highlight: THREE.Material | THREE.Material[];
};

type GltfExtensionData = {
  HYPAR_info?: { id?: string };
};

const apiState = requiredElement<HTMLSpanElement>('api-state');
const canvasHost = requiredElement<HTMLDivElement>('canvas-host');
const clearSelectionButton = requiredElement<HTMLButtonElement>('clear-selection');
const elementCount = requiredElement<HTMLElement>('element-count');
const elementList = requiredElement<HTMLDivElement>('element-list');
const emptyState = requiredElement<HTMLDivElement>('empty-state');
const fileInput = requiredElement<HTMLInputElement>('file-input');
const fitButton = requiredElement<HTMLButtonElement>('fit-button');
const loadingLabel = requiredElement<HTMLElement>('loading-label');
const loadingState = requiredElement<HTMLDivElement>('loading-state');
const meshCount = requiredElement<HTMLElement>('mesh-count');
const noSelection = requiredElement<HTMLElement>('no-selection');
const properties = requiredElement<HTMLElement>('selection-details');
const renderState = requiredElement<HTMLElement>('render-state');
const sampleButton = requiredElement<HTMLButtonElement>('sample-button');
const sceneName = requiredElement<HTMLElement>('scene-name');
const searchInput = requiredElement<HTMLInputElement>('search-input');
const selectedNumber = requiredElement<HTMLElement>('selected-number');
const selectedType = requiredElement<HTMLElement>('selected-type');
const sourceProperties = requiredElement<HTMLDListElement>('source-properties');
const summaryProperties = requiredElement<HTMLDListElement>('summary-properties');
const toast = requiredElement<HTMLDivElement>('toast');
const typeCounts = requiredElement<HTMLDivElement>('type-counts');
const uploadButton = requiredElement<HTMLButtonElement>('upload-button');

createIcons({ icons: { Box, Database, MousePointer2, Scan, Search, Upload, X } });

(THREE.BufferGeometry.prototype as unknown as { computeBoundsTree: typeof computeBoundsTree }).computeBoundsTree = computeBoundsTree;
(THREE.BufferGeometry.prototype as unknown as { disposeBoundsTree: typeof disposeBoundsTree }).disposeBoundsTree = disposeBoundsTree;
THREE.Mesh.prototype.raycast = acceleratedRaycast;

const scene = new THREE.Scene();
scene.background = new THREE.Color(0x171a1f);
scene.fog = new THREE.Fog(0x171a1f, 65, 240);

const camera = new THREE.PerspectiveCamera(46, 1, 0.01, 2500);
camera.position.set(12, 9, 12);

const renderer = new THREE.WebGLRenderer({ antialias: true, powerPreference: 'high-performance' });
renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
renderer.outputColorSpace = THREE.SRGBColorSpace;
renderer.toneMapping = THREE.ACESFilmicToneMapping;
renderer.toneMappingExposure = 1.05;
renderer.shadowMap.enabled = true;
renderer.shadowMap.type = THREE.PCFSoftShadowMap;
canvasHost.appendChild(renderer.domElement);

const controls = new OrbitControls(camera, renderer.domElement);
controls.enableDamping = true;
controls.dampingFactor = 0.08;
controls.screenSpacePanning = true;

const hemisphere = new THREE.HemisphereLight(0xe7f0f4, 0x2a2520, 2.2);
scene.add(hemisphere);
const keyLight = new THREE.DirectionalLight(0xffffff, 3.2);
keyLight.position.set(12, 18, 10);
keyLight.castShadow = true;
scene.add(keyLight);
const fillLight = new THREE.DirectionalLight(0x91b7c4, 1.4);
fillLight.position.set(-10, 5, -8);
scene.add(fillLight);

const grid = new THREE.GridHelper(160, 80, 0x526168, 0x2b3237);
grid.position.y = -0.01;
scene.add(grid);

const raycaster = new THREE.Raycaster();
raycaster.firstHitOnly = true;
const pointer = new THREE.Vector2();
const loader = new GLTFLoader();
const objectByElementId = new Map<string, THREE.Object3D>();

let manifest: SceneManifest | null = null;
let modelRoot: THREE.Object3D | null = null;
let selectedId: string | null = null;
let highlighted: HighlightedMesh[] = [];
let pointerStart = new THREE.Vector2();
let toastTimer = 0;

sampleButton.addEventListener('click', () => loadSample());
uploadButton.addEventListener('click', () => fileInput.click());
fileInput.addEventListener('change', () => {
  const file = fileInput.files?.[0];
  if (file) void uploadFile(file);
  fileInput.value = '';
});
fitButton.addEventListener('click', fitCameraToModel);
clearSelectionButton.addEventListener('click', clearSelection);
searchInput.addEventListener('input', renderElementList);
renderer.domElement.addEventListener('pointerdown', (event) => pointerStart.set(event.clientX, event.clientY));
renderer.domElement.addEventListener('pointerup', handleViewportSelection);
window.addEventListener('resize', resize);

resize();
renderer.setAnimationLoop(() => {
  controls.update();
  renderer.render(scene, camera);
});

void initialize();

async function initialize(): Promise<void> {
  try {
    const response = await fetch('/api/health');
    if (!response.ok) throw new Error('模型服务不可用');
    apiState.classList.add('connected');
    apiState.lastChild!.textContent = '服务已连接';
    await loadSample();
  } catch (error) {
    apiState.lastChild!.textContent = '服务未连接';
    showToast(errorMessage(error), true);
  }
}

async function loadSample(): Promise<void> {
  await loadFromRequest(fetch('/api/scenes/sample', { method: 'POST' }), '正在生成示例管线');
}

async function uploadFile(file: File): Promise<void> {
  const body = new FormData();
  body.append('file', file);
  await loadFromRequest(fetch('/api/scenes/import', { method: 'POST', body }), `正在读取 ${file.name}`);
}

async function loadFromRequest(request: Promise<Response>, label: string): Promise<void> {
  setLoading(true, label);
  try {
    const response = await request;
    if (!response.ok) {
      const problem = await response.json().catch(() => ({ error: response.statusText })) as { error?: string };
      throw new Error(problem.error || '场景生成失败');
    }

    const nextManifest = await response.json() as SceneManifest;
    await loadManifest(nextManifest);
  } catch (error) {
    showToast(errorMessage(error), true);
  } finally {
    setLoading(false);
  }
}

async function loadManifest(nextManifest: SceneManifest): Promise<void> {
  disposeCurrentModel();
  manifest = nextManifest;
  sceneName.textContent = nextManifest.name;
  elementCount.textContent = String(nextManifest.elementCount);
  renderTypeCounts();
  renderElementList();
  clearSelection();

  const chunkRoots = await Promise.all(nextManifest.chunks.map(async (chunk) => {
    const gltf = await loader.loadAsync(chunk.uri);
    gltf.scene.name = `chunk:${chunk.id}`;
    return gltf.scene;
  }));

  const root = new THREE.Group();
  root.name = `scene:${nextManifest.sceneId}`;
  root.add(...chunkRoots);
  modelRoot = root;
  scene.add(root);

  let meshes = 0;
  root.traverse((object) => {
    const elementId = directElementId(object);
    if (elementId) objectByElementId.set(elementId.toLowerCase(), object);
    if (!(object instanceof THREE.Mesh)) return;
    meshes += 1;
    object.castShadow = true;
    object.receiveShadow = true;
    if (object.geometry.attributes.position && object.geometry.index) {
      object.geometry.computeBoundsTree();
    }
  });

  meshCount.textContent = `${meshes} 网格`;
  renderState.textContent = `${nextManifest.chunks.length} 分块已加载`;
  emptyState.hidden = true;
  fitCameraToModel();
}

function handleViewportSelection(event: PointerEvent): void {
  if (!modelRoot || pointerStart.distanceTo(new THREE.Vector2(event.clientX, event.clientY)) > 5) return;

  const bounds = renderer.domElement.getBoundingClientRect();
  pointer.x = ((event.clientX - bounds.left) / bounds.width) * 2 - 1;
  pointer.y = -((event.clientY - bounds.top) / bounds.height) * 2 + 1;
  raycaster.setFromCamera(pointer, camera);

  const hit = raycaster.intersectObject(modelRoot, true)[0];
  const id = hit ? inheritedElementId(hit.object) : null;
  if (id) void selectElement(id);
  else clearSelection();
}

async function selectElement(id: string): Promise<void> {
  if (!manifest) return;
  clearHighlight();
  selectedId = id.toLowerCase();
  const summary = manifest.elements.find((element) => element.id.toLowerCase() === selectedId);
  const root = objectByElementId.get(selectedId);
  if (!summary || !root) return;

  highlightObject(root);
  updateActiveListItem();
  noSelection.hidden = true;
  properties.hidden = false;
  selectedType.textContent = typeLabel(summary.sourceType);
  selectedNumber.textContent = summary.number;
  renderProperties(summaryProperties, {
    系统: summary.system,
    子系统: summary.subSystem,
    图层: summary.layerName,
    形状: summary.style,
    'Element ID': summary.id
  });
  sourceProperties.replaceChildren(createLoadingRow());

  try {
    const response = await fetch(`/api/scenes/${manifest.sceneId}/elements/${summary.id}`);
    if (!response.ok) throw new Error('无法读取构件信息');
    const source = await response.json() as Record<string, unknown>;
    if (selectedId === id.toLowerCase()) renderProperties(sourceProperties, source);
  } catch (error) {
    if (selectedId === id.toLowerCase()) renderProperties(sourceProperties, { 错误: errorMessage(error) });
  }
}

function highlightObject(root: THREE.Object3D): void {
  root.traverse((object) => {
    if (!(object instanceof THREE.Mesh)) return;
    const original = object.material;
    const highlight = Array.isArray(original)
      ? original.map((material) => highlightedMaterial(material))
      : highlightedMaterial(original);
    object.material = highlight;
    highlighted.push({ mesh: object, original, highlight });
  });
}

function highlightedMaterial(original: THREE.Material): THREE.Material {
  const material = original.clone();
  if ('emissive' in material && material.emissive instanceof THREE.Color) {
    material.emissive.set(0xf0a33a);
    if ('emissiveIntensity' in material) material.emissiveIntensity = 0.72;
  }
  material.transparent = false;
  material.opacity = 1;
  return material;
}

function clearSelection(): void {
  selectedId = null;
  clearHighlight();
  updateActiveListItem();
  properties.hidden = true;
  noSelection.hidden = false;
}

function clearHighlight(): void {
  for (const entry of highlighted) {
    entry.mesh.material = entry.original;
    const materials = Array.isArray(entry.highlight) ? entry.highlight : [entry.highlight];
    materials.forEach((material) => material.dispose());
  }
  highlighted = [];
}

function renderElementList(): void {
  const term = searchInput.value.trim().toLocaleLowerCase();
  const elements = (manifest?.elements ?? []).filter((element) =>
    [element.number, element.sourceType, element.system, element.subSystem, element.layerName]
      .some((value) => value.toLocaleLowerCase().includes(term)));

  const fragment = document.createDocumentFragment();
  for (const element of elements) {
    const button = document.createElement('button');
    button.type = 'button';
    button.className = 'element-row';
    button.dataset.elementId = element.id.toLowerCase();
    button.setAttribute('role', 'option');

    const type = document.createElement('span');
    type.className = `type-mark type-${element.sourceType.toLowerCase()}`;
    type.textContent = typeInitial(element.sourceType);
    const text = document.createElement('span');
    text.className = 'element-row-text';
    const name = document.createElement('strong');
    name.textContent = element.number;
    const detail = document.createElement('span');
    detail.textContent = `${typeLabel(element.sourceType)} · ${element.system || '未分类'}`;
    text.append(name, detail);
    button.append(type, text);
    button.addEventListener('click', () => {
      void selectElement(element.id);
      focusElement(element.id);
    });
    fragment.append(button);
  }

  elementList.replaceChildren(fragment);
  updateActiveListItem();
}

function renderTypeCounts(): void {
  typeCounts.replaceChildren();
  for (const [type, count] of Object.entries(manifest?.typeCounts ?? {})) {
    const item = document.createElement('span');
    item.textContent = `${typeLabel(type)} ${count}`;
    typeCounts.append(item);
  }
}

function renderProperties(target: HTMLDListElement, values: Record<string, unknown>): void {
  const fragment = document.createDocumentFragment();
  for (const [key, rawValue] of Object.entries(values)) {
    const term = document.createElement('dt');
    term.textContent = key;
    const description = document.createElement('dd');
    description.textContent = displayValue(rawValue);
    fragment.append(term, description);
  }
  target.replaceChildren(fragment);
}

function createLoadingRow(): DocumentFragment {
  const fragment = document.createDocumentFragment();
  const term = document.createElement('dt');
  term.textContent = '状态';
  const description = document.createElement('dd');
  description.textContent = '读取中';
  fragment.append(term, description);
  return fragment;
}

function displayValue(value: unknown): string {
  if (value === null || value === undefined || value === '') return '-';
  if (Array.isArray(value)) return value.map(displayValue).join(', ');
  if (typeof value === 'object') return JSON.stringify(value);
  if (typeof value === 'boolean') return value ? '是' : '否';
  return String(value);
}

function fitCameraToModel(): void {
  if (!modelRoot) return;
  const box = new THREE.Box3().setFromObject(modelRoot);
  if (box.isEmpty()) return;
  const center = box.getCenter(new THREE.Vector3());
  const size = box.getSize(new THREE.Vector3());
  const radius = Math.max(size.length() * 0.5, 1);
  const direction = new THREE.Vector3(1, 0.75, 1).normalize();
  camera.position.copy(center).addScaledVector(direction, radius * 2.2);
  camera.near = Math.max(radius / 1000, 0.001);
  camera.far = Math.max(radius * 100, 1000);
  camera.updateProjectionMatrix();
  controls.target.copy(center);
  controls.maxDistance = radius * 20;
  controls.update();
}

function focusElement(id: string): void {
  const object = objectByElementId.get(id.toLowerCase());
  if (!object) return;
  const box = new THREE.Box3().setFromObject(object);
  if (box.isEmpty()) return;
  const center = box.getCenter(new THREE.Vector3());
  const size = Math.max(box.getSize(new THREE.Vector3()).length(), 0.5);
  const direction = camera.position.clone().sub(controls.target).normalize();
  controls.target.copy(center);
  camera.position.copy(center).addScaledVector(direction, size * 2.4);
  controls.update();
}

function disposeCurrentModel(): void {
  clearSelection();
  objectByElementId.clear();
  if (!modelRoot) return;
  scene.remove(modelRoot);
  modelRoot.traverse((object) => {
    if (!(object instanceof THREE.Mesh)) return;
    object.geometry.disposeBoundsTree?.();
    object.geometry.dispose();
    const materials = Array.isArray(object.material) ? object.material : [object.material];
    materials.forEach((material) => material.dispose());
  });
  modelRoot = null;
}

function directElementId(object: THREE.Object3D): string | null {
  const extensions = object.userData.gltfExtensions as GltfExtensionData | undefined;
  return extensions?.HYPAR_info?.id?.toString() ?? null;
}

function inheritedElementId(object: THREE.Object3D): string | null {
  let current: THREE.Object3D | null = object;
  while (current && current !== modelRoot) {
    const id = directElementId(current);
    if (id) return id;
    current = current.parent;
  }
  return null;
}

function updateActiveListItem(): void {
  elementList.querySelectorAll<HTMLButtonElement>('.element-row').forEach((row) => {
    const active = row.dataset.elementId === selectedId;
    row.classList.toggle('active', active);
    row.setAttribute('aria-selected', String(active));
    if (active) row.scrollIntoView({ block: 'nearest' });
  });
}

function setLoading(loading: boolean, label = ''): void {
  loadingState.hidden = !loading;
  loadingLabel.textContent = label;
  sampleButton.disabled = loading;
  uploadButton.disabled = loading;
}

function resize(): void {
  const width = Math.max(canvasHost.clientWidth, 1);
  const height = Math.max(canvasHost.clientHeight, 1);
  camera.aspect = width / height;
  camera.updateProjectionMatrix();
  renderer.setSize(width, height, false);
}

function showToast(message: string, error = false): void {
  window.clearTimeout(toastTimer);
  toast.textContent = message;
  toast.classList.toggle('error', error);
  toast.hidden = false;
  toastTimer = window.setTimeout(() => { toast.hidden = true; }, 5000);
}

function errorMessage(error: unknown): string {
  return error instanceof Error ? error.message : String(error);
}

function typeLabel(type: string): string {
  return ({ Pipe: '直管', Elbow: '弯头', Tee: '三通', Cross: '四通', Reducer: '变径' } as Record<string, string>)[type] ?? type;
}

function typeInitial(type: string): string {
  return ({ Pipe: 'P', Elbow: 'E', Tee: 'T', Cross: 'X', Reducer: 'R' } as Record<string, string>)[type] ?? type.slice(0, 1);
}

function requiredElement<T extends HTMLElement>(id: string): T {
  const element = document.getElementById(id);
  if (!element) throw new Error(`Missing element #${id}`);
  return element as T;
}
