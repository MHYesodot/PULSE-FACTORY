import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { z } from "zod";
import { checkStatus, generateImage, outputDirectory, transformImage } from "./drawthings-client.js";

const server = new McpServer({
  name: "pulse-factory-local-image",
  version: "0.1.0",
});

server.tool(
  "draw_things_status",
  "Check the Draw Things API on 127.0.0.1 without contacting any remote service.",
  {},
  async () => {
    const status = await checkStatus();
    return {
      content: [{ type: "text", text: JSON.stringify(status) }],
      isError: !status.connected,
    };
  },
);

server.tool(
  "generate_local_image",
  `Generate one image through the local Draw Things service and save a PNG plus provenance JSON under ${outputDirectory}.`,
  {
    assetId: z.string().describe("Short output label, such as test-mcp-smoke or concept-a-portrait."),
    prompt: z.string().describe("Image-generation prompt."),
    negativePrompt: z.string().optional(),
    width: z.number().int().optional(),
    height: z.number().int().optional(),
    steps: z.number().int().optional(),
    cfgScale: z.number().optional(),
    seed: z.number().int().optional(),
  },
  async (args) => {
    const saved = await generateImage(args);
    return {
      content: [{
        type: "text",
        text: JSON.stringify({ success: true, ...saved }),
      }],
    };
  },
);

server.tool(
  "transform_approved_local_image",
  "Transform an explicitly approved local PNG reference. No references are approved yet; unknown IDs are refused.",
  {
    assetId: z.string(),
    referenceId: z.string().describe("Approval ID from the local allowlist; arbitrary file paths are not accepted."),
    prompt: z.string(),
    negativePrompt: z.string().optional(),
    denoisingStrength: z.number().optional(),
    steps: z.number().int().optional(),
    cfgScale: z.number().optional(),
    seed: z.number().int().optional(),
  },
  async (args) => {
    const saved = await transformImage(args);
    return {
      content: [{
        type: "text",
        text: JSON.stringify({ success: true, ...saved }),
      }],
    };
  },
);

const transport = new StdioServerTransport();
await server.connect(transport);
