import assert from 'node:assert/strict';
import { mkdir } from 'node:fs/promises';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { chromium } from 'playwright';

const baseUrl = process.env.VIEWER_URL ?? 'http://127.0.0.1:5188';
const executablePath = process.env.CHROME_PATH ?? 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe';
const outputDirectory = fileURLToPath(new URL('../test-results/', import.meta.url));

await mkdir(outputDirectory, { recursive: true });

const browser = await chromium.launch({ executablePath, headless: true });
try {
  await verifyDesktop(browser);
  await verifyMobile(browser);
} finally {
  await browser.close();
}

async function verifyDesktop(browser) {
  const page = await browser.newPage({ viewport: { width: 1440, height: 900 }, deviceScaleFactor: 1 });
  const errors = [];
  page.on('console', (message) => {
    if (message.type() === 'error') errors.push(message.text());
  });
  page.on('pageerror', (error) => errors.push(error.message));

  await page.goto(baseUrl, { waitUntil: 'networkidle' });
  await page.locator('#loading-state').waitFor({ state: 'hidden', timeout: 30_000 });
  await assertVisibleScene(page);
  assert.equal(await page.locator('#element-count').textContent(), '87');
  assert.match(await page.locator('#mesh-count').textContent(), /[1-9]\d* 网格/);

  await page.locator('.element-row').first().click();
  await page.locator('#selection-details').waitFor({ state: 'visible' });
  assert.equal(await page.locator('#selected-number').textContent(), 'C0');
  await page.locator('#source-properties').getByText('C0', { exact: true }).waitFor();

  await page.locator('#clear-selection').click();
  assert(await selectFromViewport(page), 'No selectable mesh was found through viewport clicks.');
  await page.locator('#source-properties dd').first().waitFor();
  assert.equal(await page.locator('#selection-details').isVisible(), true);
  assert.deepEqual(errors, []);

  await page.screenshot({ path: join(outputDirectory, 'desktop.png'), fullPage: true });
  await page.close();
}

async function verifyMobile(browser) {
  const page = await browser.newPage({ viewport: { width: 390, height: 844 }, deviceScaleFactor: 1 });
  await page.goto(baseUrl, { waitUntil: 'networkidle' });
  await page.locator('#loading-state').waitFor({ state: 'hidden', timeout: 30_000 });
  await assertVisibleScene(page);

  const overflow = await page.evaluate(() => ({
    width: document.documentElement.scrollWidth,
    clientWidth: document.documentElement.clientWidth,
    height: document.documentElement.scrollHeight,
    clientHeight: document.documentElement.clientHeight
  }));
  assert(overflow.width <= overflow.clientWidth, `Horizontal overflow: ${JSON.stringify(overflow)}`);
  assert(overflow.height <= overflow.clientHeight, `Vertical overflow: ${JSON.stringify(overflow)}`);
  await page.screenshot({ path: join(outputDirectory, 'mobile.png'), fullPage: true });
  await page.close();
}

async function assertVisibleScene(page) {
  const canvas = page.locator('#canvas-host canvas');
  await canvas.waitFor({ state: 'visible' });
  const screenshot = await canvas.screenshot();
  const source = `data:image/png;base64,${screenshot.toString('base64')}`;
  const pixels = await page.evaluate(async (imageSource) => {
    const image = new Image();
    image.src = imageSource;
    await image.decode();
    const probe = document.createElement('canvas');
    probe.width = image.naturalWidth;
    probe.height = image.naturalHeight;
    const context = probe.getContext('2d', { willReadFrequently: true });
    context.drawImage(image, 0, 0);
    const data = context.getImageData(0, 0, probe.width, probe.height).data;
    let min = 255;
    let max = 0;
    let colored = 0;
    for (let index = 0; index < data.length; index += 16) {
      const r = data[index];
      const g = data[index + 1];
      const b = data[index + 2];
      min = Math.min(min, r, g, b);
      max = Math.max(max, r, g, b);
      if (Math.max(r, g, b) - Math.min(r, g, b) > 8) colored += 1;
    }
    return { min, max, colored };
  }, source);
  assert(pixels.max - pixels.min > 40, `Canvas lacks visual contrast: ${JSON.stringify(pixels)}`);
  assert(pixels.colored > 100, `Canvas lacks model-colored pixels: ${JSON.stringify(pixels)}`);
}

async function selectFromViewport(page) {
  const canvas = page.locator('#canvas-host canvas');
  const box = await canvas.boundingBox();
  if (!box) return false;

  for (let y = 0.25; y <= 0.75; y += 0.08) {
    for (let x = 0.2; x <= 0.8; x += 0.06) {
      await page.mouse.click(box.x + box.width * x, box.y + box.height * y);
      if (await page.locator('#selection-details').isVisible()) return true;
    }
  }
  return false;
}
