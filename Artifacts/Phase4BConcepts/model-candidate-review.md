# Local image model comparison and recommendation

Research snapshot: 2026-11-09. The user approved **SDXL 1.0 Base with Draw
Things' local 8-bit conversion**. The revision-pinned checkpoint was downloaded
and verified, imported, and converted in Draw Things. The local adapter reports
`sd_xl_base_1.0_q8p.ckpt` as the loaded model. A smoke image and three portrait
attempts were generated locally; the portraits have poor composition/prompt
compliance and are not approved concepts. No Pulse Factory screenshots,
prompts, or source files were sent to external services.

## Device and Draw Things support

- Device: Apple M1 Pro, 16 GiB unified memory; Draw Things 26.1020.0 is
  installed.
- Draw Things' model documentation lists SDXL Base, SSD-1B, Z-Image, and
  FLUX.2 4B model families as supported. On this Mac, the imported 8-bit SDXL
  Base model loaded and generated the four local PNGs. This verifies that
  specific model/adapter path, not memory benchmarks, image quality, or the
  other model families.
- Draw Things documents an in-app **Convert to 8-bit** operation and gives an
  illustrative RAM reduction from 16 GB to 8 GB. Its documentation does not
  publish the converted SDXL file's exact on-disk size or an M1 Pro benchmark.
- Memory figures below are planning ranges, not vendor guarantees. They depend
  on resolution, quantization, app/runtime overhead, other open apps, and
  model-loading options. Shared memory is used by both macOS and inference.
- Public model file sizes are exact for the named upstream files. The Draw
  Things model catalog's packaged download sizes were not exposed in the
  published documentation, so upstream sizes are used where an app package
  size is not available.

## Comparison

| Candidate and exact version | Download / memory | Draw Things fit and speed | Artistic tradeoffs and style consistency | License |
|---|---|---|---|---|
| **Recommended: SDXL 1.0 Base, Draw Things 8-bit conversion.** Upstream revision `462165984030d82259a11f4367a4eed129e94a7b`; convert this base model inside Draw Things. | Source checkpoint `sd_xl_base_1.0.safetensors`: **6,938,078,334 bytes** (6.94 GB / 6.46 GiB). The converted copy is produced locally; its exact package size is not published. Draw Things' 8-bit guide gives an example of model RAM falling from about 16 GB to 8 GB. Plan on roughly 8–11 GiB for inference allocation and more total unified-memory pressure once macOS and buffers are included; not measured on this M1 Pro. | `.sdxlBase` is listed as supported. The 8-bit conversion is an app feature. Most balanced fit for 16 GiB, with smaller output sizes and no simultaneous Unity/editor workload recommended during generation. Generation speed is unmeasured on this machine; SDXL is not a few-step model by default. | Best overall art-direction flexibility here: the original SDXL base has broad stylized-art capability and a large SDXL LoRA ecosystem. A single fixed style description, seed, sampler, size, and optional rights-reviewed style LoRA can keep the three concept directions visually related. This is the best quality/control versus memory compromise, not necessarily the fastest. | **CreativeML Open RAIL++-M.** Commercial use is allowed subject to its use restrictions and obligations; not unrestricted and no warranty. No revenue cap was found in the cited license. |
| **SSD-1B**, exact upstream revision `60987f37e94cd59c36b1cba832b9f97b57395a10`; use official A1111-compatible checkpoint `SSD-1B-A1111.safetensors`. | **4,465,671,322 bytes** (4.47 GB / 4.16 GiB). A rough 6–9 GiB total inference-memory planning range is inferred from its 1.3B parameter size and checkpoint; no M1 Pro/Draw Things peak-memory measurement found. | Draw Things lists `.ssd1b` support. This is the strongest lower-memory alternative that is explicitly listed as a supported model family. Segmind reports the distilled model as 50% smaller and up to 60% faster than SDXL on its own tested GPUs; do not treat that as an M1 Pro speed benchmark. | Faster, smaller, and likely sufficient for broad stylized concepts, but the model card notes limitations with fine detail, text, and complex composition. Its distinct distilled character may make it less flexible than full SDXL for iterating a single art direction. | **Apache-2.0** for the released model. The model card discloses training on datasets including a Midjourney-derived dataset; the license is permissive, but data provenance may be a separate commercial-review concern. |
| **SDXL 1.0 Base, unconverted FP16** (same revision as recommended row). | Same exact source file: **6,938,078,334 bytes** (6.46 GiB). Rough **9–13 GiB** inference allocation at 768–1024-pixel output, before all other macOS use; not measured. | Supported SDXL family. Plausible on 16 GiB with careful settings, but offers less headroom than the app's documented 8-bit conversion and may be more sensitive to other memory use. | Highest fidelity/flexibility of the SDXL choices in this comparison. Still more steps than Turbo/Lightning. LoRAs and fixed prompt settings help keep style coherent. | CreativeML Open RAIL++-M, same terms as the recommended row. |
| **SDXL Lightning 4-step**, upstream revision `c9a24f48e1c025556787b0c58dd67a091ece2e44`. The full `sdxl_lightning_4step.safetensors` checkpoint is **6,938,040,682 bytes**; the separate 4-step LoRA is **393,854,592 bytes** and requires an SDXL base. | Full checkpoint is about 6.46 GiB and uses roughly the same memory class as SDXL; distillation reduces steps, not the base weight footprint. | Draw Things supports SDXL and LoRAs, but this exact Lightning checkpoint/LoRA has not been verified in Draw Things. Treat as conditional, not the first install. | Four-step generation can shorten iteration. The publisher describes the 2/4/8-step variants as its higher-quality options and calls 1-step experimental. Lower-step distillation can still trade some control/detail for speed. | Hugging Face tags the checkpoint **OpenRAIL++**; review its included license and the base SDXL license together. |
| **SDXL-Turbo 1.0**, upstream revision `71153311d3dbb46851df1931d3ca6e939de83304`. | Full `sd_xl_turbo_1.0.safetensors`: **6,938,081,905 bytes** (about 6.46 GiB). FP16 file is smaller, but the same model family and comparable inference-buffer needs remain. Roughly 9–13 GiB unconverted; not measured. | SDXL-family import is the documented path; the exact Turbo checkpoint was not listed separately in the Draw Things compatibility table. It can generate in 1–4 steps, but Draw Things import is not runtime-tested. | Much faster iteration, but the official card prefers 512-pixel images, notes imperfect detail/text/people, and says CFG/negative prompts are not used in the normal way. Not my choice for the polished 1024-pixel concept boards. | **Stability AI Community License**, not the base model's Open RAIL++-M license. Commercial use is governed by separate terms, including membership/revenue and acceptable-use conditions; check the current agreement before commercial use. |
| **FLUX.2 [klein] 4B**, upstream revision `e7b7dc27f91deacad38e78976d1f2b499d76a294`. | The full public Diffusers weight set totals **15,964,212,614 bytes** (about 14.87 GiB), before small configs/tokenizers. Official FP8 transformer-only file is **4,070,624,520 bytes**; it is not a complete pipeline because text-encoder and VAE weights are separate. The full-precision set alone nearly fills 16 GiB; not viable as-is. | Draw Things lists `.flux2_4b` support. Its app-specific quantized package size and peak RAM for this Mac are not published in the sources reviewed. The model card reports about 13 GB VRAM for its reference configuration, which is not directly equivalent to unified memory. Consider only if the Draw Things catalog offers a compact, compatible package and measured memory leaves headroom. | Strong modern image-generation/editing quality and four-step sampling; likely good prompt following and detail, but a full local run could be memory-constrained and may be slower than SDXL on this Mac. Style matching still needs fixed prompts/settings and a consistent seed. | **Apache-2.0**, commercial use allowed subject to the model card's out-of-scope use requirements. |
| **Z-Image-Turbo**, upstream revision `f332072aa78be7aecdf3ee76d5c247082da564a6`. | The official repository's three transformer shards, three text-encoder shards, and VAE total **32,832,339,790 bytes** (about 30.57 GiB), before small files. The full upstream checkpoint set is far beyond 16 GiB. No exact Draw Things quantized package size was found. | Draw Things lists `.zImage` support, but this exact upstream weight set is not feasible on this Mac. Do not substitute an unverified community 4-bit conversion: Draw Things compatibility, size, quality, and license of such a package would need separate review. | The model card describes a 6B family and Turbo sampling in 8 function evaluations. High-quality potential, but the official weight set is too large for the target device. | **Apache-2.0** for the official upstream model. |

### Recommendation for Pulse Factory

**Use SDXL 1.0 Base converted to 8-bit inside Draw Things**, once explicitly
approved. It gives the best blend of stylized-concept quality, established
control/LoRA options, local-memory practicality, and predictable licensing
among the candidates with a realistic path on this Mac. Keep the same style
paragraph, canvas dimensions, sampler, steps, and seed strategy across related
shots. Do not add a LoRA until its exact source and commercial license are
reviewed.

**SSD-1B is the fallback** if SDXL still causes memory pressure or generation
speed is too slow. It is smaller and Apache-2.0, but its model-card data
provenance and weaker fine-detail/composition performance make it a less
compelling art-direction base.

FLUX.2 Klein 4B could be a later quality comparison if Draw Things exposes a
specific quantized local package that fits this Mac; the upstream full model is
too large to assume it will. Z-Image-Turbo is not recommended until an exact,
smaller, Draw Things-compatible package is identified and licensed.

## Approved checkpoint verification

- Download location:
  `~/Library/Application Support/Pulse Factory/Models/sd_xl_base_1.0.safetensors`
- Downloaded size: **6,938,078,334 bytes**, exactly matching the pinned model
  repository metadata.
- SHA-256:
  `31e35c80fc4829d14f90153f4c74cd59c90b779f6afe05a74cd6120b893f7e5b`, matching
  the model repository's LFS object digest.
- The SafeTensors header parsed successfully and contained 2,515 tensors.
- Draw Things' status reports the loaded model as
  `sd_xl_base_1.0_q8p.ckpt`; the checkpoint has been imported and converted to
  8-bit locally. A smoke image and three portrait attempts were generated.

## Local API and approval record

The project-owned MCP adapter is registered in Copilot Global configuration and
its stdio handshake/tool listing was verified earlier. **The requested
adapter-to-Draw-Things status path was verified:** the listener is bound only
to `127.0.0.1:7860`, and `draw_things_status` returned
`connected: true`, `modelName: "sd_xl_base_1.0_q8p.ckpt"` using a local MCP
stdio client. That client also generated the smoke image and three portrait
attempts through the loopback API. The Copilot chat tool surface itself was not
exposed to this assistant, so that specific integration layer could not be
independently observed. No project screenshot or generated asset was sent to
an external generation service.

The user explicitly approved this exact SDXL checkpoint and local 8-bit
conversion. That approval does not authorize downloading any other model,
using cloud compute, or using a project screenshot as an image reference.

## Sources

- [Draw Things: supported model bases](https://wiki.drawthings.ai/wiki/Supported_model_bases)
- [Draw Things: convert to 8-bit](https://wiki.drawthings.ai/wiki/Convert_to_8-bit)
- [Draw Things: device/model compatibility](https://wiki.drawthings.ai/wiki/How_Powerful_Is_My_Device%3F)
- [Draw Things: install a model or LoRA](https://wiki.drawthings.ai/wiki/Install_a_Model_or_LoRA)
- [SDXL 1.0 Base model card](https://huggingface.co/stabilityai/stable-diffusion-xl-base-1.0/tree/462165984030d82259a11f4367a4eed129e94a7b)
- [SDXL 1.0 Base license](https://huggingface.co/stabilityai/stable-diffusion-xl-base-1.0/blob/main/LICENSE.md)
- [Segmind SSD-1B model card](https://huggingface.co/segmind/SSD-1B/tree/60987f37e94cd59c36b1cba832b9f97b57395a10)
- [ByteDance SDXL-Lightning model card](https://huggingface.co/ByteDance/SDXL-Lightning/tree/c9a24f48e1c025556787b0c58dd67a091ece2e44)
- [Stability AI SDXL-Turbo model card](https://huggingface.co/stabilityai/sdxl-turbo/tree/71153311d3dbb46851df1931d3ca6e939de83304)
- [SDXL-Turbo license](https://huggingface.co/stabilityai/sdxl-turbo/blob/main/LICENSE.md)
- [Black Forest Labs FLUX.2 Klein 4B model card](https://huggingface.co/black-forest-labs/FLUX.2-klein-4B/tree/e7b7dc27f91deacad38e78976d1f2b499d76a294)
- [Tongyi-MAI Z-Image-Turbo model card](https://huggingface.co/Tongyi-MAI/Z-Image-Turbo/tree/f332072aa78be7aecdf3ee76d5c247082da564a6)
- [Apache License 2.0](https://www.apache.org/licenses/LICENSE-2.0)
