# Pulse Factory local image MCP

This project-owned MCP server bridges VS Code Copilot to the locally running
Draw Things app. It has no cloud provider integration or API key support.
Requests are hard-coded to `127.0.0.1:7860`; generated PNG and metadata files
are saved under `Artifacts/Phase4BConcepts/Generated/`.

The only image-to-image inputs accepted are IDs in
`approved-references.json`. That file is intentionally empty. Do not add a
project screenshot or other reference until the user explicitly approves the
specific file.

Draw Things 26.1020.0 is installed and running. Its API listener was observed
on `127.0.0.1:7860` only. A local stdio MCP client verified
`connected: true` and the loaded model `sd_xl_base_1.0_q8p.ckpt`, then generated
a smoke PNG and three 576×1024 portrait PNGs. The files and provenance JSON
are in `Artifacts/Phase4BConcepts/Generated/`. The generation path works, but
the portraits do not reliably match the requested game board/UI and are not
approved concepts.

The first portrait call using the MCP SDK client's default 60-second request
timeout timed out without producing a file. Retrying with an explicit
600,000-millisecond request timeout succeeded. The current Copilot chat did
not expose the custom MCP tools, so generation was verified with a standalone
local stdio client rather than the chat tool surface.

Install the pinned dependencies after reviewing this source:

```sh
npm ci --ignore-scripts
```

In the VS Code **Copilot Global** MCP configuration, the server command is
`/usr/local/bin/node` with argument
`/Users/michaelhurwitz/Projects/PULSE-FACTORY/Tools/Phase4BImageMcp/src/server.js`.
There is no API-key authentication for this local-only route. The project
server is registered in Copilot Global MCP; the host session still needs to
load/restart the server before its tools are available in that chat.

Validation:

```sh
npm test
node --check src/server.js
```
