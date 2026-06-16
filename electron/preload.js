import { contextBridge } from 'electron';

contextBridge.exposeInMainWorld('tapeReplay', {
  platform: process.platform,
});
