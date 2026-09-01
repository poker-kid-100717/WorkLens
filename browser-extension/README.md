# WorkLens Browser Extension

One-click save for LinkedIn and Indeed job postings into your self-hosted WorkLens.
Not published to the Chrome/Edge Web Store — install it as an unpacked extension
pointed at your own WorkLens API.

## What it does

- On a LinkedIn job page (`linkedin.com/jobs/...`) or an Indeed job page
  (`indeed.com/...`), click the extension icon.
- It reads the job title, company, and location straight off the page and pre-fills
  a small form. Review/edit if anything looks off, then click **Save to tracker**.
- The job is created in your WorkLens exactly like anything added via "Save from
  URL" in the web app — same status pipeline, same reminders, same analytics.

This never touches LinkedIn's or Indeed's private APIs and does no automated
searching or crawling — it only reads the page you're already looking at, on demand,
when you click the icon. That keeps it well clear of the ToS issues that apply to
automated scraping/searching (see the main project README).

## Installing (Chrome / Edge / Brave)

1. Open `chrome://extensions` (or `edge://extensions`).
2. Turn on **Developer mode** (top right).
3. Click **Load unpacked** and select this `browser-extension/` folder.
4. Click the extension icon once, then **Settings**, and set the **API base URL** to
   your running WorkLens API — e.g. `http://localhost:5080/api` if you're running
   docker-compose on the same machine, or `http://<your-server-ip>:5080/api` if the
   backend runs on another machine on your network.
5. Chrome will prompt for permission to access that host when you save — approve it.
   This is expected and only grants access to the one host you typed in, not general
   browsing history or other sites.

## Using it

1. Browse to any LinkedIn or Indeed job posting.
2. Click the WorkLens extension icon.
3. Confirm/edit the pre-filled title, company, location, and URL. Add notes if you
   want.
4. Click **Save to tracker**. It shows up immediately in the Tracker tab's "Saved"
   column.

## If extraction comes back empty

LinkedIn and Indeed change their page markup periodically without notice, so the
content scripts (`content-linkedin.js`, `content-indeed.js`) may occasionally miss a
field. If that happens:
- The popup still pre-fills the page title and URL at minimum.
- Just type in whatever's missing (company/location) before saving — it still works,
  it's just not fully automatic that time.
- If it stops working entirely, open the relevant `content-*.js` file and update the
  CSS selectors to match the site's current markup (inspect the page and find the
  new class/data-testid names).

## Why this isn't in the Chrome Web Store

Publishing requires a Google Developer account, a review process, and ongoing
maintenance commitments that don't make sense for a personal, self-hosted tool
pointed at your own private API. Loading it unpacked is the standard way to run a
personal-use extension like this.
