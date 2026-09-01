// Extracts job details from a LinkedIn job posting page (/jobs/view/... or the
// jobs search detail pane) and responds to a request from the popup.
// LinkedIn's DOM changes without notice — these selectors target the stable-ish
// class names as of writing. If extraction comes back empty, the popup falls back
// to a manual entry form pre-filled with just the page title/URL.

function textOf(selector) {
  const el = document.querySelector(selector);
  return el ? el.textContent.trim().replace(/\s+/g, ' ') : '';
}

function extractLinkedInJob() {
  // Job detail page selectors (both the standalone /jobs/view/ page and the
  // search-results detail pane use very similar class names).
  const title =
    textOf('.job-details-jobs-unified-top-card__job-title h1') ||
    textOf('.jobs-unified-top-card__job-title') ||
    textOf('h1.top-card-layout__title') ||
    document.title.split(' | ')[0];

  const company =
    textOf('.job-details-jobs-unified-top-card__company-name a') ||
    textOf('.job-details-jobs-unified-top-card__company-name') ||
    textOf('.jobs-unified-top-card__company-name') ||
    textOf('.topcard__org-name-link');

  const location =
    textOf('.job-details-jobs-unified-top-card__primary-description-container span') ||
    textOf('.jobs-unified-top-card__bullet') ||
    textOf('.topcard__flavor--bullet');

  return {
    title: title || '',
    company: company || '',
    location: location || '',
    url: window.location.href.split('?')[0],
    source: 'LinkedIn'
  };
}

chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  if (message?.type === 'EXTRACT_JOB') {
    sendResponse(extractLinkedInJob());
  }
});
