const formEl = document.getElementById('form');
const unsupportedEl = document.getElementById('unsupported');
const statusEl = document.getElementById('status');
const saveBtn = document.getElementById('save-btn');

const fields = {
  title: document.getElementById('title'),
  company: document.getElementById('company'),
  location: document.getElementById('location'),
  url: document.getElementById('url'),
  notes: document.getElementById('notes')
};

function setStatus(text, kind) {
  statusEl.textContent = text;
  statusEl.className = `status ${kind ? 'status-' + kind : ''}`;
}

async function init() {
  const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
  if (!tab?.url) return showUnsupported();

  const isLinkedIn = /linkedin\.com\/jobs/.test(tab.url);
  const isIndeed = /indeed\.com/.test(tab.url);

  if (!isLinkedIn && !isIndeed) return showUnsupported();

  formEl.style.display = 'flex';

  try {
    const extracted = await chrome.tabs.sendMessage(tab.id, { type: 'EXTRACT_JOB' });
    if (extracted) {
      fields.title.value = extracted.title || '';
      fields.company.value = extracted.company || '';
      fields.location.value = extracted.location || '';
      fields.url.value = extracted.url || tab.url;
    } else {
      fields.url.value = tab.url;
    }
  } catch {
    // Content script may not have loaded yet (e.g. page just opened) — fall back
    // to at least prefilling the URL so the user isn't starting from nothing.
    fields.url.value = tab.url;
    fields.title.value = tab.title || '';
  }
}

function showUnsupported() {
  unsupportedEl.style.display = 'block';
  formEl.style.display = 'none';
}

async function getApiBase() {
  const { apiBaseUrl } = await chrome.storage.sync.get('apiBaseUrl');
  return (apiBaseUrl || 'http://localhost:5080/api').replace(/\/$/, '');
}

saveBtn.addEventListener('click', async () => {
  const title = fields.title.value.trim();
  const company = fields.company.value.trim();

  if (!title || !company) {
    setStatus('Title and company are required.', 'error');
    return;
  }

  saveBtn.disabled = true;
  setStatus('Saving…');

  try {
    const apiBase = await getApiBase();
    const response = await fetch(`${apiBase}/applications`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        title,
        company,
        location: fields.location.value.trim() || undefined,
        url: fields.url.value.trim() || undefined,
        notes: fields.notes.value.trim() || undefined
      })
    });

    if (response.status === 409) {
      setStatus('Already tracked.', 'error');
    } else if (!response.ok) {
      const text = await response.text();
      setStatus(`Save failed: ${text || response.status}`, 'error');
    } else {
      setStatus('Saved to your tracker.', 'success');
    }
  } catch (err) {
    setStatus(
      'Could not reach your WorkLens API. Check the API URL in Settings and make sure the backend is running and reachable.',
      'error'
    );
  } finally {
    saveBtn.disabled = false;
  }
});

init();
