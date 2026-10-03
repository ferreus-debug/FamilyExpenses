// End-to-end walk-through of phase 4 in a real browser: owner signs up, creates an event and
// families, invites family B; Bo signs up via the link and registers expenses; access rules hold.
// Run with tests/e2e/run.sh (starts the app on a throw-away database).
import { chromium } from 'playwright-core';

const BASE = process.env.BASE ?? 'http://127.0.0.1:5058';
const SHOTS = new URL('./shots/', import.meta.url).pathname;
const CHROME = process.env.CHROME_PATH; // optional; otherwise Playwright's own browser
const step = (msg) => console.log(`• ${msg}`);

const browser = await chromium.launch(CHROME ? { executablePath: CHROME } : {});
const errors = [];

async function newUser() {
  const context = await browser.newContext({ viewport: { width: 1280, height: 900 }, locale: 'da-DK' });
  const page = await context.newPage();
  page.on('pageerror', e => errors.push(`pageerror: ${e.message}`));
  page.on('response', r => { if (r.status() >= 400) errors.push(`HTTP ${r.status()} ${r.url()}`); });
  page.on('requestfailed', r => { if (!r.url().startsWith('https://fonts.') && !r.url().endsWith('/_blazor/disconnect')) errors.push(`failed ${r.url()} ${r.failure()?.errorText}`); });
  return page;
}

// Interactive Blazor pages are prerendered; wait until the circuit is connected before clicking.
async function ready(page) {
  await page.waitForLoadState('networkidle');
  await page.waitForFunction(() => window.Blazor !== undefined);
  await page.waitForTimeout(800);
}

async function dialogFill(page, label, value) {
  const dialog = page.locator('.mud-dialog');
  await dialog.getByLabel(label, { exact: false }).first().fill(value);
}

async function register(page, url, name, email) {
  await page.goto(url);
  await page.fill('#name', name);
  await page.fill('#email', email);
  await page.fill('#password', 'hemmelig-kode');
  await page.fill('#confirm', 'hemmelig-kode');
  await page.click('button[type=submit]');
}

// ---------------- Anna: owner ----------------
const anna = await newUser();
step('Anna registrerer sig som første bruger');
await register(anna, `${BASE}/Account/Register`, 'Anna', 'anna@example.com');
await anna.waitForURL(`${BASE}/`);
await ready(anna);
await anna.getByText('Mine begivenheder').first().waitFor();
await anna.getByText('Anna', { exact: true }).first().waitFor();

step('Anna opretter en begivenhed');
await anna.getByRole('button', { name: 'Ny begivenhed' }).click();
await dialogFill(anna, 'Navn', 'Sommerhus 2026');
await anna.locator('.mud-dialog').getByRole('button', { name: 'Opret' }).click();
await anna.waitForURL(/\/events\/.+\/households$/);
const eventUrl = anna.url().replace(/\/households$/, '');
await ready(anna);

step('Anna tilføjer tre familier');
for (const name of ['Familie A', 'Familie B', 'Familie C']) {
  await anna.getByRole('button', { name: /Tilføj familie/ }).click();
  await dialogFill(anna, 'Familiens navn', name);
  await anna.locator('.mud-dialog').getByRole('button', { name: 'Tilføj' }).click();
  await anna.locator('.mud-card', { hasText: name }).first().waitFor();
}
if (await anna.getByRole('button', { name: /Tilføj familie/ }).count() !== 0) throw new Error('Kunne tilføje en 4. familie');

async function addPerson(page, family, name, child) {
  const card = page.locator('.mud-card', { hasText: family });
  await card.getByRole('button', { name: 'Tilføj person' }).click();
  await dialogFill(page, 'Navn', name);
  if (child) await page.locator('.mud-dialog').getByText('Barn (tæller 0,5)').click();
  await page.locator('.mud-dialog').getByRole('button', { name: 'Gem' }).click();
  await card.getByText(name, { exact: true }).waitFor();
}

step('Anna tilføjer personer');
await addPerson(anna, 'Familie A', 'Anna', false);
await addPerson(anna, 'Familie A', 'Alma', true);
await addPerson(anna, 'Familie B', 'Bo', false);
await addPerson(anna, 'Familie B', 'Bjørn', true);
await addPerson(anna, 'Familie C', 'Carla', false);

step('Anna tilføjer en ekstra person (barn)');
await anna.getByRole('button', { name: 'Tilføj ekstra person' }).click();
await dialogFill(anna, 'Navn', 'Xenia');
await anna.locator('.mud-dialog').getByText('Barn (tæller 0,5)').click();
await anna.locator('.mud-dialog').getByRole('button', { name: 'Gem' }).click();
await anna.locator('.mud-card', { hasText: 'Ekstra person' }).getByText('Xenia').first().waitFor();
await anna.screenshot({ path: `${SHOTS}households.png`, fullPage: true });

step('Anna vælger sin egen husstand på oversigten');
await anna.goto(eventUrl);
await ready(anna);
await anna.getByText('Hvilken husstand hører du selv til?').waitFor();
await anna.getByRole('button', { name: 'Familie A' }).click();
await anna.getByText('Familie A (dig)').waitFor();

step('Anna opretter invitationslink til Familie B');
await anna.goto(`${eventUrl}/invitations`);
await ready(anna);
await anna.locator('.mud-paper', { hasText: 'Familie B' }).getByRole('button', { name: 'Opret link' }).click();
const linkField = anna.locator('.mud-dialog textarea, .mud-dialog input').first();
await linkField.waitFor();
const inviteUrl = await linkField.inputValue();
if (!inviteUrl.includes('/invite/')) throw new Error(`Uventet link: ${inviteUrl}`);
await anna.locator('.mud-dialog').getByRole('button', { name: 'Færdig' }).click();
await anna.getByText('Venter').waitFor();

// ---------------- Bo: invited ----------------
const bo = await newUser();
step('Bo åbner linket og opretter konto');
await bo.goto(inviteUrl);
await ready(bo);
await bo.getByText('Familie B').first().waitFor();
await bo.getByRole('link', { name: 'Opret konto' }).click();
await bo.waitForURL(/Account\/Register/);
await bo.getByText('Du er inviteret til').waitFor();
await bo.fill('#name', 'Bo');
await bo.fill('#email', 'bo@example.com');
await bo.fill('#password', 'hemmelig-kode');
await bo.fill('#confirm', 'hemmelig-kode');
await bo.click('button[type=submit]');
await bo.waitForURL(/\/invite\//);
await ready(bo);
await bo.getByRole('button', { name: 'Deltag som Familie B' }).click();
await bo.waitForURL(eventUrl);
await ready(bo);
await bo.getByText('Familie B (dig)').waitFor();
if (await bo.getByRole('link', { name: 'Invitationer' }).count() !== 0) throw new Error('Bo kan se invitationer');

step('Bo registrerer en udgift (betaler foreslås automatisk)');
await bo.goto(`${eventUrl}/expenses`);
await ready(bo);
await bo.getByRole('button', { name: 'Tilføj udgift' }).click();
await dialogFill(bo, 'Hvad var det', 'Indkøb Netto');
await dialogFill(bo, 'Beløb', '450,50');
await bo.locator('.mud-dialog').getByText(/6 personer · vægt 4/).waitFor();
await bo.locator('.mud-dialog').screenshot({ path: `${SHOTS}expense-dialog.png` });
await bo.locator('.mud-dialog').getByRole('button', { name: 'Gem' }).click();
await bo.getByText('Udgiften er gemt.').waitFor();
await bo.locator('tr', { hasText: 'Indkøb Netto' }).getByText('Bo').first().waitFor();
await bo.getByTestId('expenses-total').getByText('450,50 kr.').waitFor();

step('Bo registrerer en udgift kun for Familie C');
await bo.getByRole('button', { name: 'Tilføj udgift' }).click();
await dialogFill(bo, 'Hvad var det', 'Is til Carla');
await dialogFill(bo, 'Beløb', '40');
await bo.locator('.mud-dialog label.mud-checkbox').filter({ has: bo.getByText('Alle', { exact: true }) }).click();
await bo.locator('.mud-dialog label.mud-checkbox').filter({ has: bo.getByText('Familie C', { exact: true }) }).click();
await bo.locator('.mud-dialog').getByText(/1 person · vægt 1/).waitFor();
await bo.locator('.mud-dialog').getByRole('button', { name: 'Gem' }).click();
await bo.locator('tr', { hasText: 'Is til Carla' }).getByText('Familie C').waitFor();
await bo.screenshot({ path: `${SHOTS}expenses-bo.png`, fullPage: true });

// ---------------- Anna sees it ----------------
step('Anna ser Bos udgifter og kan rette dem');
await anna.goto(`${eventUrl}/expenses`);
await ready(anna);
const row = anna.locator('tr', { hasText: 'Indkøb Netto' });
await row.getByText('af Bo').waitFor();
if (await row.getByRole('button', { name: 'Ret Indkøb Netto' }).count() !== 1) throw new Error('Admin kan ikke rette');

step('Bo kan ikke rette Annas udgift');
await anna.getByRole('button', { name: 'Tilføj udgift' }).click();
await dialogFill(anna, 'Hvad var det', 'Sommerhus');
await dialogFill(anna, 'Beløb', '4250');
await anna.locator('.mud-dialog').getByRole('button', { name: 'Gem' }).click();
await anna.locator('tr', { hasText: 'Sommerhus' }).getByText('Anna').first().waitFor();
await bo.reload();
await ready(bo);
const annasRow = bo.locator('tr', { hasText: 'Sommerhus' });
await annasRow.waitFor();
if (await annasRow.getByRole('button').count() !== 0) throw new Error('Bo kan rette Annas udgift');

step('Bo logger ud og ind igen');
await bo.goto(`${BASE}/`);
await ready(bo);
await bo.getByRole('button', { name: /Log ud/ }).click();
await bo.waitForURL(`${BASE}/`);
await bo.getByRole('link', { name: 'Log ind' }).first().waitFor();
await bo.goto(`${BASE}/Account/Login`);
await bo.fill('#email', 'bo@example.com');
await bo.fill('#password', 'forkert-kode');
await bo.click('button[type=submit]');
await bo.getByText('Forkert e-mail eller adgangskode.').waitFor();
await bo.screenshot({ path: `${SHOTS}login.png` });
await bo.fill('#email', 'bo@example.com');
await bo.fill('#password', 'hemmelig-kode');
await bo.click('button[type=submit]');
await bo.waitForURL(`${BASE}/`);
await ready(bo);
await bo.locator('.mud-card', { hasText: 'Sommerhus 2026' }).getByText('Familie B').waitFor();
await bo.locator('.mud-card', { hasText: 'Sommerhus 2026' }).getByText('4.740,50 kr.').waitFor();

// ---------------- Stranger ----------------
const stranger = await newUser();
step('En fremmed kan ikke oprette konto uden invitation');
await stranger.goto(`${BASE}/Account/Register`);
await stranger.getByText('Du skal bruge et gyldigt invitationslink').waitFor();
step('En fremmed sendes til login fra en begivenhed');
await stranger.goto(eventUrl);
await stranger.waitForURL(/Account\/Login/);

step('Brugt link kan ikke genbruges');
await stranger.goto(inviteUrl);
await ready(stranger);
await stranger.getByText(/allerede brugt eller udløbet/).waitFor();

step('Mobilvisning af udgifter');
const mobile = await bo.context().newPage();
await mobile.setViewportSize({ width: 390, height: 844 });
await mobile.goto(`${eventUrl}/expenses`);
await ready(mobile);
await mobile.screenshot({ path: `${SHOTS}expenses-mobile.png`, fullPage: true });

await browser.close();
const relevant = errors.filter(e => !e.includes('favicon'));
if (relevant.length) { console.log('Browser errors:\n' + relevant.join('\n')); process.exit(1); }
console.log('E2E OK');
