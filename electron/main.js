import { app, BrowserWindow, ipcMain, shell } from 'electron';
import { spawn } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import http from 'node:http';
import {
  checkAndApplyPatchUpdate,
  getPatchInfo,
  resolveFrontendIndex,
} from './patchUpdateService.js';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const rootDir = path.resolve(__dirname, '..');
const isDev = !app.isPackaged;

let backendProcess;
let mainWindow;
let lastPatchResult = null;

function waitForBackend(url, timeoutMs = 60000) {
  const started = Date.now();

  return new Promise((resolve, reject) => {
    function attempt() {
      const request = http.get(url, (response) => {
        response.resume();
        if (response.statusCode === 200) {
          resolve();
          return;
        }

        retry();
      });

      request.on('error', retry);

      function retry() {
        if (Date.now() - started > timeoutMs) {
          reject(new Error('Backend did not start in time'));
          return;
        }

        setTimeout(attempt, 500);
      }
    }

    attempt();
  });
}

function resolveBackendLaunch() {
  if (isDev) {
    return {
      command: 'dotnet',
      args: ['run', '--project', path.join(rootDir, 'backend')],
      cwd: rootDir,
      env: {
        ...process.env,
        ASPNETCORE_ENVIRONMENT: 'Development',
      },
    };
  }

  const backendDir = path.join(process.resourcesPath, 'backend');
  const executableName = process.platform === 'win32' ? 'TapeReplay.Api.exe' : 'TapeReplay.Api';
  const executablePath = path.join(backendDir, executableName);
  const dataDir = path.join(app.getPath('userData'), 'data');

  fs.mkdirSync(dataDir, { recursive: true });

  return {
    command: executablePath,
    args: [],
    cwd: backendDir,
    env: {
      ...process.env,
      TAPEREPLAY_DATA_DIR: dataDir,
      ASPNETCORE_ENVIRONMENT: 'Production',
    },
  };
}

function startBackend() {
  const launch = resolveBackendLaunch();

  backendProcess = spawn(launch.command, launch.args, {
    cwd: launch.cwd,
    env: launch.env,
    stdio: isDev ? 'inherit' : 'pipe',
  });

  if (!isDev && backendProcess.stderr) {
    backendProcess.stderr.on('data', (chunk) => {
      console.error(`[backend] ${chunk.toString()}`);
    });
  }

  backendProcess.on('exit', (code) => {
    if (code !== 0 && code !== null) {
      console.error(`Backend exited with code ${code}`);
    }
  });
}

async function createWindow() {
  mainWindow = new BrowserWindow({
    width: 1400,
    height: 900,
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
    },
  });

  if (isDev) {
    await mainWindow.loadURL('http://localhost:5173');
    mainWindow.webContents.openDevTools({ mode: 'detach' });
  } else {
    await mainWindow.loadFile(resolveFrontendIndex(rootDir));
  }
}

function registerIpc() {
  ipcMain.handle('tapereplay:get-patch-info', () => ({
    ...getPatchInfo(),
    lastUpdateResult: lastPatchResult,
  }));

  ipcMain.handle('tapereplay:open-external', async (_event, url) => {
    if (typeof url !== 'string' || !/^https?:\/\//i.test(url)) {
      throw new Error('Invalid URL');
    }

    await shell.openExternal(url);
    return true;
  });
}

app.whenReady().then(async () => {
  registerIpc();

  if (app.isPackaged) {
    lastPatchResult = await checkAndApplyPatchUpdate();
  }

  startBackend();

  try {
    await waitForBackend('http://localhost:5180/api/health');
    await createWindow();
  } catch (error) {
    console.error(error.message);
    app.quit();
  }
});

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') {
    app.quit();
  }
});

app.on('activate', async () => {
  if (BrowserWindow.getAllWindows().length === 0) {
    await createWindow();
  }
});

app.on('before-quit', () => {
  if (backendProcess && !backendProcess.killed) {
    backendProcess.kill();
  }
});
