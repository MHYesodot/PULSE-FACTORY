import assert from "node:assert/strict";
import test from "node:test";
import path from "node:path";
import { generateImage, outputDirectory, transformImage } from "../src/drawthings-client.js";

test("generated output is constrained to the concept asset folder", () => {
  assert.equal(path.basename(outputDirectory), "Generated");
  assert.match(outputDirectory, /Artifacts[/\\]Phase4BConcepts[/\\]Generated$/);
});

test("text-to-image rejects path-like and invalid asset IDs before any network call", async () => {
  await assert.rejects(
    generateImage({ assetId: "../outside", prompt: "test" }),
    /assetId must be/,
  );
  await assert.rejects(
    generateImage({ assetId: "test", prompt: "test", width: 2048, height: 2048 }),
    /dimensions/,
  );
});

test("image-to-image refuses all references until a reference has explicit approval", async () => {
  await assert.rejects(
    transformImage({ assetId: "test", referenceId: "phase4a-level-two", prompt: "test" }),
    /No local reference image has been approved/,
  );
});
