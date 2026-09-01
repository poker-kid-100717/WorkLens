// Extracts job details from an Indeed job posting page. Indeed's DOM/class names
// also change without notice — selectors below target commonly stable data-testid
// attributes and fall back to heading/URL text if those aren't present.

function textOf(selector) {
  const el = document.querySelector(selector);
  return el ? el.textContent.trim().replace(/\s+/g, ' ') : '';
}

function extractIndeedJob() {
  const title =
    textOf('[data-testid="jobsearch-JobInfoHeader-title"]') ||
    textOf('.jobsearch-JobInfoHeader-title') ||
    document.title.split(' - ')[0];

  const company =
    textOf('[data-testid="inlineHeader-companyName"]') ||
    textOf('.jobsearch-InlineCompanyRating a') ||
    textOf('[data-testid="jobsearch-CompanyInfoContainer"] a');

  const location =
    textOf('[data-testid="inlineHeader-companyLocation"]') ||
    textOf('[data-testid="job-location"]');

  return {
    title: title || '',
    company: company || '',
    location: location || '',
    url: window.location.href.split('?')[0],
    source: 'Indeed'
  };
}

chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  if (message?.type === 'EXTRACT_JOB') {
    sendResponse(extractIndeedJob());
  }
});
