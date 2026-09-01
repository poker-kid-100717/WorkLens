const input = document.getElementById('api-base');
const statusEl = document.getElementById('status');

chrome.storage.sync.get('apiBaseUrl').then(({ apiBaseUrl }) => {
  input.value = apiBaseUrl || 'http://localhost:5080/api';
});

document.getElementById('save-btn').addEventListener('click', async () => {
  const value = input.value.trim().replace(/\/$/, '');

  let origin;
  try {
    origin = new URL(value).origin;
  } catch {
    statusEl.textContent = 'That does not look like a valid URL.';
    statusEl.style.color = '#e3556b';
    return;
  }

  // Extension pages (options/popup) get the host_permissions cross-origin fetch
  // bypass, but only for origins actually granted — request the API's origin here
  // since it's unknown until the user tells us (unlike linkedin.com/indeed.com,
  // which are hard-coded in the manifest).
  const granted = await chrome.permissions.request({ origins: [`${origin}/*`] });

  if (!granted) {
    statusEl.textContent = 'Permission denied — the extension needs access to that host to save jobs there.';
    statusEl.style.color = '#e3556b';
    return;
  }

  await chrome.storage.sync.set({ apiBaseUrl: value });
  statusEl.style.color = '#3ecf8e';
  statusEl.textContent = 'Saved.';
  setTimeout(() => (statusEl.textContent = ''), 2000);
});
