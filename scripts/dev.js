import { spawn } from "node:child_process";
import path from "node:path";
import { fileURLToPath } from "node:url";

const repositoryRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const webDirectory = path.join(repositoryRoot, "apps", "web");
const processes = [];
let isStopping = false;

const services = [
  {
    name: "API",
    command: "dotnet",
    args: ["run", "--no-build", "--project", "apps/api/api.csproj"],
    cwd: repositoryRoot,
  },
  {
    name: "Worker",
    command: "dotnet",
    args: ["run", "--no-build", "--project", "apps/worker/worker.csproj"],
    cwd: repositoryRoot,
  },
  {
    name: "Web",
    command: process.execPath,
    args: [path.join("node_modules", "next", "dist", "bin", "next"), "dev"],
    cwd: webDirectory,
  },
];

function stopAll(exitCode) {
  if (isStopping) {
    return;
  }

  isStopping = true;
  for (const child of processes) {
    if (child.exitCode === null && !child.killed) {
      child.kill();
    }
  }

  if (exitCode !== undefined) {
    process.exitCode = exitCode;
  }
}

process.on("SIGINT", () => stopAll(0));
process.on("SIGTERM", () => stopAll(0));

function startServices() {
  for (const entry of services) {
    startProcess(entry);
  }
}

function startProcess(entry, onExit) {
  const child = spawn(entry.command, entry.args, {
    cwd: entry.cwd,
    stdio: "inherit",
    windowsHide: true,
  });
  processes.push(child);

  child.on("error", (error) => {
    console.error(`[${entry.name}] Could not start: ${error.message}`);
    stopAll(1);
  });

  child.on("exit", (code, signal) => {
    if (!isStopping) {
      if (signal) {
        console.log(`[${entry.name}] Stopped after signal ${signal}.`);
      } else if (code !== 0) {
        console.error(`[${entry.name}] Exited with code ${code}.`);
      }
      if (onExit) {
        onExit(code, signal);
      } else {
        stopAll(code ?? 1);
      }
    }
  });
}

function startWorkerBuild() {
  startProcess(
    {
      name: "Worker build",
      command: "dotnet",
      args: [
        "build",
        "apps/worker/worker.csproj",
        "--no-restore",
        "-m:1",
        "-p:BuildProjectReferences=false",
        "-p:UseSharedCompilation=false",
      ],
      cwd: repositoryRoot,
    },
    (code) => {
      if (code === 0) {
        startServices();
      } else {
        console.error("Worker build failed; services were not started.");
        stopAll(code ?? 1);
      }
    },
  );
}

console.log("Building API and worker sequentially before starting services.");
startProcess(
  {
    name: "API build",
    command: "dotnet",
    args: [
      "build",
      "apps/api/api.csproj",
      "--no-restore",
      "-m:1",
      "-p:UseSharedCompilation=false",
    ],
    cwd: repositoryRoot,
  },
  (code) => {
    if (code === 0) {
      startWorkerBuild();
    } else {
      console.error("API build failed; services were not started.");
      stopAll(code ?? 1);
    }
  },
);
