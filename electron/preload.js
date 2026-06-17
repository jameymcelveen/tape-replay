import { contextBridge, ipcRenderer } from 'electron';

contextBridge.exposeInMainWorld('tapeReplay', {
  platform: process.platform,
  getPatchInfo: () => ipcRenderer.invoke('tapereplay:get-patch-info'),
  openExternal: (url) => ipcRenderer.invoke('tapereplay:open-external', url),
});
