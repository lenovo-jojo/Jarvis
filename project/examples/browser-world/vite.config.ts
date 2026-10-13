import { defineConfig, type Plugin } from "vite";
import { writeFile } from "node:fs/promises";
import { resolve } from "node:path";

function sceneEditSavePlugin(): Plugin {
  return {
    name: "critter-comforts-scene-save",

    configureServer(server) {
      server.middlewares.use(
        "/__cc/save-scene-edits",
        (req, res, next) => {
          if (req.method !== "POST") {
            next();
            return;
          }

          let body = "";

          req.setEncoding("utf8");

          req.on("data", (chunk) => {
            body += chunk;

            if (body.length > 10_000_000) {
              req.destroy();
            }
          });

          req.on("end", async () => {
            try {
              const parsed = JSON.parse(body);

              const sceneEdits = Array.isArray(parsed)
                ? parsed
                : parsed?.sceneEdits;

              if (!Array.isArray(sceneEdits)) {
                throw new Error("scene edit payload must contain a sceneEdits array");
              }

              // Finish validation before starting any file writes.
              if (parsed?.editorAssets !== undefined &&
                  (!Array.isArray(parsed.editorAssets?.assets) || !Array.isArray(parsed.editorAssets?.instances))) {
                throw new Error("editorAssets must contain assets and instances arrays");
              }

              const target = resolve(
                process.cwd(),
                "src/data/scene-edits.json"
              );

              const writes = [
                writeFile(target, JSON.stringify(sceneEdits, null, 2) + "\n", "utf8"),
              ];

              if (parsed?.editorAssets !== undefined) {
                writes.push(
                  writeFile(
                    resolve(process.cwd(), "src/data/editor-assets.json"),
                    JSON.stringify(parsed.editorAssets, null, 2) + "\n",
                    "utf8"
                  )
                );
              }

              await Promise.all(writes);

              res.statusCode = 200;
              res.setHeader(
                "Content-Type",
                "application/json"
              );
              res.end(
                JSON.stringify({
                  ok: true,
                  objects: sceneEdits.length,
                })
              );
            } catch (error) {
              console.error(
                "[scene-save] failed",
                error
              );

              res.statusCode = 500;
              res.setHeader(
                "Content-Type",
                "application/json"
              );
              res.end(
                JSON.stringify({
                  ok: false,
                  error: String(error),
                })
              );
            }
          });
        }
      );

      server.middlewares.use(
        "/__cc/save-editor-assets",
        (req, res, next) => {
          if (req.method !== "POST") {
            next();
            return;
          }

          let body = "";
          req.setEncoding("utf8");
          req.on("data", (chunk) => {
            body += chunk;
            if (body.length > 10_000_000) req.destroy();
          });

          req.on("end", async () => {
            try {
              const parsed = JSON.parse(body);
              if (!parsed || !Array.isArray(parsed.assets) || !Array.isArray(parsed.instances)) {
                throw new Error("editor asset payload must contain assets and instances arrays");
              }

              await writeFile(
                resolve(process.cwd(), "src/data/editor-assets.json"),
                JSON.stringify(parsed, null, 2) + "\n",
                "utf8"
              );

              res.statusCode = 200;
              res.setHeader("Content-Type", "application/json");
              res.end(JSON.stringify({ ok: true, assets: parsed.assets.length, instances: parsed.instances.length }));
            } catch (error) {
              console.error("[editor-assets-save] failed", error);
              res.statusCode = 500;
              res.setHeader("Content-Type", "application/json");
              res.end(JSON.stringify({ ok: false, error: String(error) }));
            }
          });
        }
      );
    },
  };
}

export default defineConfig({
  plugins: [sceneEditSavePlugin()],

  server: {
    port: 3333,
    strictPort: true,
    host: true,
  },
  build: {
    target: "ES2022",
    outDir: "dist",
    assetsInlineLimit: 0,
  },
});
