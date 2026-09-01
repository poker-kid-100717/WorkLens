// Minimal service worker. Currently just ensures default settings exist on install
// so the options page and popup have something sane to read on first use.
chrome.runtime.onInstalled.addListener(async () => {
  const existing = await chrome.storage.sync.get('apiBaseUrl');
  if (!existing.apiBaseUrl) {
    await chrome.storage.sync.set({ apiBaseUrl: 'http://localhost:5080/api' });
  }
});
