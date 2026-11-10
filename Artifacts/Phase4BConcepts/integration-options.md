# Image-generation integration assessment

Research date: 2026-11-09

## Current environment and discovery

- macOS 27.0.1, Apple Silicon (`arm64`), 16 GiB unified memory, about 288 GiB
  free at the home volume.
- VS Code Copilot supports MCP. No image-generation server was initially
  connected. A project-owned server is now registered in Copilot Global MCP,
  but the active session has not loaded it yet.
- Node.js 22.12.0 and Python 3.12.1 are installed. `uv` and the Comfy CLI are
  not installed.
- Ollama is installed, but has no local models and its server is stopped. It
  is not currently an image-generation option.
- Draw Things is installed from the Mac App Store and passes code-signature and
  Gatekeeper verification. It is running, and its API listener was observed at
  `127.0.0.1:7860` only (not on a wildcard/LAN address). Comfy Desktop is not
  installed.
- A project-owned Draw Things MCP adapter has been implemented under
  `Tools/Phase4BImageMcp/`. The official `@modelcontextprotocol/sdk` 1.32.2
  dependency is pinned in its package lock, its dependency audit reports no
  high-severity issues, and an MCP stdio handshake successfully listed its
  three tools. A local stdio MCP client called `draw_things_status` through the
  project server and verified `connected: true`, endpoint
  `http://127.0.0.1:7860`, and model
  `sd_xl_base_1.0_q8p.ckpt`. The same adapter generated one smoke PNG and three
  portrait PNGs. The pipeline works; the portrait outputs do not adequately
  follow the requested board/UI composition.
- The adapter is registered in `~/.copilot/mcp-config.json`. The user reports
  reloading VS Code. This assistant's tool registry does not expose the custom
  MCP tools, so VS Code tool presentation is not independently confirmed here;
  the server itself has been tested over MCP stdio.
- This Copilot session has no image-generation or animation-generation tool.
  ChatGPT image capabilities do not automatically become tools in this
  Copilot session.
- The Draw Things local API is active and loopback-only. A smoke image and three
  concept portraits were generated through it; no project image was uploaded
  and no generation charge was made.
- Draw Things' published privacy policy says it uses no third-party analytics
  or advertising frameworks for tracking; Apple-provided information is outside
  that statement. This policy review is not a runtime network audit, and the
  app has not yet been launched for network observation. The setup did contact
  the App Store, npm registry, and public model metadata endpoint for app,
  package, and model information; no project screenshots, prompts, or source
  files were sent to those services.

The absence of relevant environment-variable names is not proof that no
credentials exist in another app or keychain. No credentials were read.

## Options

| Option | Capabilities and consistency | Cost and commercial-use notes | Assessment |
|---|---|---|---|
| **Draw Things locally + a narrowly scoped local MCP adapter** | The Mac app listing advertises local/offline image creation and image-to-image. A project-owned adapter now exposes text-to-image, approved local image-to-image, and saving PNGs into the concept folder. Fixed model, seed, prompt recipe, and approved local references can improve consistency. | The Mac App Store listing is **Free · In-App Purchases** and says local creation needs no subscription; optional cloud features must not be used. Local model files still have their own licenses. The adapter uses the pinned official MCP SDK and has no provider API. | **Recommended for the strict $0/private workflow**, conditional on confirming the Draw Things local API works on this Mac and selecting a commercially suitable model. No first-party Draw Things MCP server was identified; this project-owned adapter avoids installing the unreviewed community bridge. |
| **ComfyUI locally + Comfy-Org `comfy-mcp`** | Official Comfy-Org MCP repository/package supports local ComfyUI workflows, text-prompt generation, asynchronous jobs, and copying output files. Saved workflows/models/seeds can support repeatability; image references can be staged into local ComfyUI. The server is currently described as beta. | Local software/model use can avoid generation fees, but the model license governs commercial suitability. `comfy-mcp` is dual-licensed `AGPL-3.0-or-later OR LicenseRef-Comfy-Commercial`; commercial terms are available from Comfy Org, so review the AGPL implications before use. The tool surface also includes cloud/partner features that can incur charges; do not sign in, set `COMFY_API_KEY`, call partner tools, or use workflows with partner nodes. The documented spend gate defaults to no spend and requires the stated comfy-cli floor, but the safe workflow should still be strictly local. | **Best off-the-shelf first-party MCP integration**, but heavier and more complex. Comfy's own docs caution that large open-weight models may be slow on Mac GPUs; this machine has 16 GiB unified memory. Use only after a small local model benchmark and license check. |
| **Community `mcp-drawthings` package** | Claims text-to-image, image-to-image, seed/settings, and PNG file output over a local Draw Things API. It is MIT-licensed and has a Node.js requirement of 18+. | The wrapper's MIT license does not license the app, model weights, or generated content. No cloud fee is inherent if its API host remains local. | **Do not install as-is.** Its source accepts arbitrary image input paths and output paths, and its host can be overridden. That gives the MCP process broad local file-read/write capability. The README recommends unpinned `npx -y`, which would execute the package without a reviewed local checkout. A custom allowlisted bridge is safer. |
| **Hosted Comfy Cloud / OpenAI / Runway** | Hosted image/animation capabilities may offer stronger models, but none is connected to this session. | Requires network upload and a service account; price/quotas vary, and no $0 entitlement has been verified. This conflicts with the current $0 cap and no-upload instruction. | **Not eligible now. Do not connect or call.** A ChatGPT subscription does not imply an OpenAI API entitlement in this session. |

### Recommendation

Use **Draw Things local plus the project-owned MCP adapter**, now installed
and registered. This keeps intended inference on the Mac, avoids cloud
credentials and per-image API charges, and restricts the MCP surface to the
three needed operations. The community bridge was reviewed only to assess the
API route; it was not installed. The custom bridge:

- call only a verified loopback Draw Things endpoint;
- offer no arbitrary host, path, shell, or cloud options;
- restrict generated output to
  `Artifacts/Phase4BConcepts/`;
- refuse image-to-image unless the exact input file is approved;
- record the model, prompt, parameters, dimensions, and output path; and
- use a pinned, lockfile-backed version of the official MCP SDK.

If the Draw Things API cannot be verified or the chosen model cannot meet the
commercial-license requirement, the fallback is local ComfyUI + the
Comfy-Org MCP server, after reviewing its license and keeping all partner and
cloud capabilities unused.

## Setup status and authentication

The user authorized free local integration setup and approved the specific
SDXL Base model and its local 8-bit conversion. The Draw Things app has been
installed from the Mac App Store and verified with `codesign`/Gatekeeper. The
adapter and pinned MCP SDK have been installed and tested; the Copilot Global
MCP config is written. The API and loaded 8-bit model were verified at
loopback. One smoke PNG and three portrait concepts were generated locally;
their visual compliance is poor, and none is approved. No project image was
uploaded, no generation charge was made, and no account or cloud
authentication was configured.

### Recommended: Draw Things local + scoped adapter

1. **Complete:** Draw Things was installed from the [official Mac App Store
   listing](https://apps.apple.com/us/app/draw-things-offline-ai-art/id6444050820)
   and its signature/Gatekeeper status was verified. The listing says
   **Free · In-App Purchases**; remain on local generation and do not enable
   Cloud Compute, sign in for cloud, subscribe, or buy credits.
2. **Complete:** the only model reviewed and approved so far is Stability AI
   SDXL 1.0 Base. Its exact file, license, size, and checksum are recorded in
   [model-candidate-review.md](model-candidate-review.md); the user imported
   and converted it locally to 8-bit.
3. **Complete:** the installed app's listener was observed on
   `127.0.0.1:7860` only. Do not change the setting to a wildcard or LAN
   address. The `draw_things_status` adapter tool returned a successful local
   connection.
4. The project-owned adapter is already implemented at
   `Tools/Phase4BImageMcp/`, using Node.js 22.12.0 and the pinned official
   MCP SDK 1.32.2. It sends HTTP requests only to `127.0.0.1:7860`, saves only
   beneath `Artifacts/Phase4BConcepts/Generated/`, and refuses image references
   until they have an explicit allowlist entry. `npm test` and the MCP
   initialization/tool-list handshake passed. The dependency audit found no
   high-severity issue.
5. The VS Code Copilot Global config registers
   `pulse-factory-local-art` from the exact project path. Its configuration
   contains no credentials. The user reports reloading VS Code. This assistant
   could not independently confirm that the Copilot UI surfaced the tools, but
   the adapter was launched via stdio and its status tool successfully reached
   the verified loopback API. Endpoint authentication is not used; the loopback
   binding and narrow file access are safeguards.
6. **Complete:** a 512×512, 8-step text-only smoke PNG and three matched
   576×1024, 20-step portrait attempts were generated and verified. The
   portraits did not reliably follow the board topology or UI brief. The first
   MCP client call timed out at its default 60 seconds; a retry with a
   10-minute client timeout succeeded. No reference screenshot was used.
   Improve the composition strategy before generating the nine remaining
   proposed views.

After the adapter has been implemented and reviewed, its Copilot Global
portable configuration would use the VS Code `mcpServers` format, for example:

```json
{
  "mcpServers": {
    "pulse-factory-local-art": {
      "command": "/usr/local/bin/node",
      "args": [
        "/Users/michaelhurwitz/Projects/PULSE-FACTORY/Tools/Phase4BImageMcp/src/server.js"
      ]
    }
  }
}
```

This configuration is already present in `~/.copilot/mcp-config.json` and
matches the installed adapter. The MCP stdio process itself listens on no
network port; its image client is hard-coded to the Draw Things loopback
address. It does not expose arbitrary file paths, shell commands, cloud
endpoints, or model downloads.

**Authentication:** for this local path, the bridge uses MCP stdio
between VS Code and a local process, then loopback HTTP to the local app. There
is no hosted account or API token to configure. Do not enable a remote API
listener. If authentication is required by the actual installed app, stop and
review that design rather than sending credentials.

### Alternative: official local Comfy MCP

If selected instead, Comfy-Org's current documented quickstart is:

1. Install Comfy Desktop only from
   [Comfy's official download page](https://comfy.org/download) and its
   [macOS installation guide](https://docs.comfy.org/installation/desktop/macos).
   Their guide lists macOS 13+ and Apple Silicon M1+ requirements, and
   recommends at least 4.85 GB per installation, excluding model files.
   Do not bypass Gatekeeper. Comfy Desktop checks for updates periodically;
   its docs say automatic installation can be disabled, although update checks
   themselves cannot.
2. Install the local engine and local MCP server in an isolated Python
   environment. The upstream quickstart uses
   `pip install comfy-mcp "comfy-cli>=1.14.0"`, then `comfy install` and
   `comfy launch`. Python 3.12.1 meets the documented Python >=3.10 floor; `uv`
   is absent, so a dedicated `venv` is preferable to a global installation.
   These commands download packages and ComfyUI/model data but do not by
   themselves authorize paid calls. The following shows an isolated
   environment and the documented package floor; it is provided for review
   only and has not been run:

   ```bash
   python3 -m venv "$HOME/.venvs/comfy-mcp"
   "$HOME/.venvs/comfy-mcp/bin/python" -m pip install \
     "comfy-mcp==0.10.0" "comfy-cli>=1.14.0"
   "$HOME/.venvs/comfy-mcp/bin/comfy" install
   "$HOME/.venvs/comfy-mcp/bin/comfy" launch
   ```

   Check the current package versions and license notices before installing;
   the MCP server is beta and upstream documents that `comfy-cli` is a separate
   required install.
3. Keep it local: do not connect Comfy Cloud, run `auth_login`, set
   `COMFY_API_KEY`, or use partner/API nodes. Use a workflow containing only a
   local model and local nodes. Confirm that the execution target is
   `127.0.0.1:8188` before submitting work. Never pass a paid workflow or set
   `confirm_spend=true`.
4. In VS Code, add a User → Global → Copilot Global **Command (stdio)** MCP
   server that launches the environment's `comfy-mcp` executable and sets
   `COMFY_BIN` to that same environment's `comfy` executable. The
   configuration should omit `COMFY_API_KEY` entirely. VS Code's portable
   format uses top-level `mcpServers`; do not copy it into another config
   format without adapting its schema.
5. Review the Comfy MCP license and the exact local model license before
   commercial use. Then use only local prompt/image workflows and verify one
   small PNG before starting the concept batch.

This option has more install dependencies and a much broader MCP tool surface
(the upstream README currently describes 41 tools). A custom, restricted
adapter may be preferable if a hard tool-level boundary against cloud/partner
operations is required.

For reference, the Comfy MCP Copilot Global entry would be:

```json
{
  "mcpServers": {
    "comfy-mcp-local": {
      "command": "/Users/michaelhurwitz/.venvs/comfy-mcp/bin/comfy-mcp",
      "env": {
        "COMFY_BIN": "/Users/michaelhurwitz/.venvs/comfy-mcp/bin/comfy"
      }
    }
  }
}
```

Do not add `COMFY_API_KEY`. Local generation requires no Comfy Cloud
authentication. The example is not installed or active.

## Human review still required

No image model is selected. Before installing/downloading weights, review the
specific model's commercial-use terms, redistribution restrictions, attribution
requirements, and any revenue or field-of-use thresholds. AI-service software
licenses, model-weight licenses, and output ownership/usage terms are distinct.
The [Stability AI Community License](https://stability.ai/license), for
example, has an under-US$1M annual-revenue condition for its Core Models and
may require a different license above that threshold; this is only an example,
not a blanket recommendation for a particular checkpoint.

The local Phase 4A screenshot remains private. `reference_upload=false` is
respected: it has not been submitted to any service, and it will not be used
as an image-to-image input until separately approved. The initial text-to-image
test and concept board can be generated from the level JSON and prepared prompt
package alone.

## Sources

- [VS Code: Add MCP servers](https://code.visualstudio.com/docs/agent-customization/mcp-servers)
- [VS Code: MCP configuration reference](https://code.visualstudio.com/docs/agents/reference/mcp-configuration)
- [Comfy Org: Comfy MCP local and cloud options](https://docs.comfy.org/agent-tools/mcp)
- [Comfy Org: Comfy Desktop for macOS](https://docs.comfy.org/installation/desktop/macos)
- [Comfy Org: Comfy MCP source, README, and license](https://github.com/Comfy-Org/comfy-mcp)
- [Draw Things App Store listing](https://apps.apple.com/us/app/draw-things-offline-ai-art/id6444050820)
- [Draw Things privacy policy](https://drawthings.ai/policy/)
- [Community Draw Things MCP source reviewed, not installed](https://github.com/james-see/mcp-drawthings)
- [Stability AI license terms](https://stability.ai/license)
