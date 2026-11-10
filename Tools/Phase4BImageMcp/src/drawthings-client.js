import { mkdir, readFile, realpath, writeFile } from "node:fs/promises";
import path from "node:path";
import { randomUUID } from "node:crypto";
import { fileURLToPath } from "node:url";

const PROJECT_ROOT = await realpath(fileURLToPath(new URL("../../..", import.meta.url)));
const OUTPUT_DIR = path.join(PROJECT_ROOT, "Artifacts", "Phase4BConcepts", "Generated");
const API_BASE = "http://127.0.0.1:7860";
const PNG_SIGNATURE = Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]);
const MAX_IMAGE_BYTES = 32 * 1024 * 1024;
const MAX_RESPONSE_BYTES = 64 * 1024 * 1024;

const APPROVED_REFERENCES_PATH = fileURLToPath(
  new URL("../approved-references.json", import.meta.url),
);

function assertAssetId(assetId) {
  if (!/^[a-z0-9][a-z0-9_-]{0,47}$/.test(assetId)) {
    throw new Error("assetId must be 1-48 lowercase letters, digits, '_' or '-'.");
  }
}

function pngFromBase64(image) {
  if (typeof image !== "string" || image.length === 0 || image.length > MAX_IMAGE_BYTES * 1.4) {
    throw new Error("The local generator returned an empty or oversized image.");
  }
  if (!/^(?:[A-Za-z0-9+/]{4})*(?:[A-Za-z0-9+/]{2}==|[A-Za-z0-9+/]{3}=)?$/.test(image)) {
    throw new Error("The local generator returned invalid base64 image data.");
  }

  const buffer = Buffer.from(image, "base64");
  if (buffer.length > MAX_IMAGE_BYTES || !buffer.subarray(0, 8).equals(PNG_SIGNATURE)) {
    throw new Error("The local generator did not return a valid PNG within the size limit.");
  }
  return buffer;
}

function safeRunName(assetId) {
  assertAssetId(assetId);
  const timestamp = new Date().toISOString().replaceAll(":", "").replaceAll(".", "");
  return `${assetId}-${timestamp}-${randomUUID().slice(0, 8)}`;
}

async function localRequest(pathname, { method = "GET", body, timeoutMs = 15_000 } = {}) {
  const response = await fetch(new URL(pathname, API_BASE), {
    method,
    headers: body === undefined ? undefined : { "Content-Type": "application/json" },
    body: body === undefined ? undefined : JSON.stringify(body),
    signal: AbortSignal.timeout(timeoutMs),
  });

  if (!response.ok) {
    const message = (await response.text()).slice(0, 2_000);
    throw new Error(`Draw Things local API returned HTTP ${response.status}: ${message}`);
  }

  const contentLength = Number(response.headers.get("content-length") || 0);
  if (contentLength > MAX_RESPONSE_BYTES) {
    throw new Error("Draw Things local API response exceeded the size limit.");
  }
  const bytes = Buffer.from(await response.arrayBuffer());
  if (bytes.length > MAX_RESPONSE_BYTES) {
    throw new Error("Draw Things local API response exceeded the size limit.");
  }
  return JSON.parse(bytes.toString("utf8"));
}

async function approvedReference(referenceId) {
  const config = JSON.parse(await readFile(APPROVED_REFERENCES_PATH, "utf8"));
  const relativePath = config.approvedReferences?.[referenceId];
  if (typeof relativePath !== "string" || path.isAbsolute(relativePath)) {
    throw new Error("No local reference image has been approved for this integration.");
  }

  const candidate = path.resolve(PROJECT_ROOT, relativePath);
  const relative = path.relative(PROJECT_ROOT, candidate);
  if (!relative || relative.startsWith(`..${path.sep}`) || path.isAbsolute(relative)) {
    throw new Error("Approved reference must be a file inside the project.");
  }

  const actualPath = await realpath(candidate);
  const actualRelative = path.relative(PROJECT_ROOT, actualPath);
  if (!actualRelative || actualRelative.startsWith(`..${path.sep}`) || path.isAbsolute(actualRelative)) {
    throw new Error("Approved reference resolves outside the project.");
  }
  const image = await readFile(actualPath);
  if (image.length > MAX_IMAGE_BYTES || !image.subarray(0, 8).equals(PNG_SIGNATURE)) {
    throw new Error("Approved reference must be a PNG no larger than 32 MiB.");
  }
  return image.toString("base64");
}

async function savePng(assetId, imageBase64, metadata) {
  const png = pngFromBase64(imageBase64);
  await mkdir(OUTPUT_DIR, { recursive: true });

  const runName = safeRunName(assetId);
  const pngPath = path.join(OUTPUT_DIR, `${runName}.png`);
  const metadataPath = path.join(OUTPUT_DIR, `${runName}.json`);
  const metadataText = JSON.stringify(
    { schemaVersion: 1, createdAt: new Date().toISOString(), ...metadata, outputFile: `${runName}.png` },
    null,
    2,
  );

  await writeFile(pngPath, png, { flag: "wx" });
  try {
    await writeFile(metadataPath, `${metadataText}\n`, { flag: "wx" });
  } catch (error) {
    throw new Error(`PNG was saved at ${pngPath}, but its provenance file could not be saved: ${error.message}`);
  }
  return { pngPath, metadataPath, bytes: png.length };
}

export async function checkStatus() {
  try {
    const config = await localRequest("/");
    const modelName =
      typeof config.model === "string" ? config.model :
      typeof config.model_name === "string" ? config.model_name :
      typeof config.sd_model_checkpoint === "string" ? config.sd_model_checkpoint :
      null;
    return { connected: true, endpoint: API_BASE, modelName };
  } catch (error) {
    return { connected: false, endpoint: API_BASE, error: error.message };
  }
}

export async function generateImage({
  assetId,
  prompt,
  negativePrompt = "",
  width = 512,
  height = 512,
  steps = 20,
  cfgScale = 7.5,
  seed = -1,
}) {
  assertAssetId(assetId);
  if (typeof prompt !== "string" || prompt.trim().length === 0 || prompt.length > 8_000) {
    throw new Error("prompt must contain 1-8000 characters.");
  }
  if (typeof negativePrompt !== "string" || negativePrompt.length > 2_000) {
    throw new Error("negativePrompt must contain at most 2000 characters.");
  }
  if (!Number.isInteger(width) || width < 256 || width > 1024
      || !Number.isInteger(height) || height < 256 || height > 1024
      || width * height > 1_048_576) {
    throw new Error("Image dimensions must be 256-1024 pixels and no more than 1 megapixel.");
  }
  if (!Number.isInteger(steps) || steps < 1 || steps > 50) {
    throw new Error("steps must be an integer from 1 to 50.");
  }
  if (!Number.isFinite(cfgScale) || cfgScale < 1 || cfgScale > 20) {
    throw new Error("cfgScale must be between 1 and 20.");
  }
  if (!Number.isInteger(seed) || seed < -1) {
    throw new Error("seed must be an integer of -1 or greater.");
  }

  const result = await localRequest("/sdapi/v1/txt2img", {
    method: "POST",
    timeoutMs: 10 * 60 * 1000,
    body: {
      prompt,
      negative_prompt: negativePrompt,
      width,
      height,
      steps,
      cfg_scale: cfgScale,
      seed,
      batch_size: 1,
    },
  });

  if (!Array.isArray(result.images) || result.images.length !== 1) {
    throw new Error("Expected exactly one image from the local generator.");
  }

  return savePng(assetId, result.images[0], {
    mode: "text-to-image",
    endpoint: API_BASE,
    prompt,
    negativePrompt,
    parameters: { width, height, steps, cfgScale, seed },
  });
}

export async function transformImage({
  assetId,
  referenceId,
  prompt,
  negativePrompt = "",
  denoisingStrength = 0.6,
  steps = 20,
  cfgScale = 7.5,
  seed = -1,
}) {
  assertAssetId(assetId);
  if (typeof referenceId !== "string" || !/^[a-z0-9][a-z0-9_-]{0,47}$/.test(referenceId)) {
    throw new Error("referenceId must be a registered approval ID.");
  }
  if (typeof prompt !== "string" || prompt.trim().length === 0 || prompt.length > 8_000) {
    throw new Error("prompt must contain 1-8000 characters.");
  }
  if (typeof negativePrompt !== "string" || negativePrompt.length > 2_000) {
    throw new Error("negativePrompt must contain at most 2000 characters.");
  }
  if (!Number.isFinite(denoisingStrength) || denoisingStrength < 0 || denoisingStrength > 1) {
    throw new Error("denoisingStrength must be between 0 and 1.");
  }
  if (!Number.isInteger(steps) || steps < 1 || steps > 50) {
    throw new Error("steps must be an integer from 1 to 50.");
  }
  if (!Number.isFinite(cfgScale) || cfgScale < 1 || cfgScale > 20) {
    throw new Error("cfgScale must be between 1 and 20.");
  }
  if (!Number.isInteger(seed) || seed < -1) {
    throw new Error("seed must be an integer of -1 or greater.");
  }

  const image = await approvedReference(referenceId);
  const result = await localRequest("/sdapi/v1/img2img", {
    method: "POST",
    timeoutMs: 10 * 60 * 1000,
    body: {
      prompt,
      init_images: [image],
      negative_prompt: negativePrompt,
      denoising_strength: denoisingStrength,
      steps,
      cfg_scale: cfgScale,
      seed,
      batch_size: 1,
    },
  });

  if (!Array.isArray(result.images) || result.images.length !== 1) {
    throw new Error("Expected exactly one image from the local generator.");
  }

  return savePng(assetId, result.images[0], {
    mode: "image-to-image",
    endpoint: API_BASE,
    referenceId,
    prompt,
    negativePrompt,
    parameters: { denoisingStrength, steps, cfgScale, seed },
  });
}

export const outputDirectory = OUTPUT_DIR;
