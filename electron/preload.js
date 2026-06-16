import { contextBridge, ipcRenderer } from 'electron';

contextBridge.exposeInMainWorld('tapeReplay', {
  platform: process.platform,
  getPatchInfo: () => ipcRenderer.invoke('tapereplay:get-patch-info'),
});
