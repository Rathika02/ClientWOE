"use strict";

/* ============================================================================
   Client WOE entry - frontend logic (no framework, no build step)

   API_BASE must point at the running backend:
     dotnet run                          -> http://localhost:5080/api
     dotnet run --launch-profile https   -> https://localhost:7080/api
   ============================================================================ */
const API_BASE = "http://localhost:5080/api";

const SEARCH_DEBOUNCE_MS = 250;
const MOBILE_PATTERN = /^[6-9]\d{9}$/;   // same rule as the API
const GENDER_VALUE = { Male: 0, Female: 1, Other: 2 };

// Colour dot = cap colour of the usual sample tube for that test category.
const TUBE_COLORS = {
  Haematology: "#8f7bd1",
  Biochemistry: "#d6a02e",
  Endocrinology: "#2f9e8f",
  Diabetes: "#7d8a95",
  Vitamins: "#e58a2f",
  Urine: "#c9b83a",
  Immunology: "#c2415a",
};
const DEFAULT_TUBE_COLOR = "#9aa7b1";

const inr = new Intl.NumberFormat("en-IN", { style: "currency", currency: "INR" });
const toPaise = (rate) => Math.round(Number(rate) * 100);   // integer maths avoids 0.1 + 0.2 issues
const money = (paise) => inr.format(paise / 100);
const clamp = (n, min, max) => Math.min(max, Math.max(min, n));

/* ---------------------------------------------------------------- DOM refs */
const $ = (id) => document.getElementById(id);

const els = {
  apiStatus: $("apiStatus"),
  globalBanner: $("globalBanner"),
  globalBannerText: $("globalBannerText"),
  retryBtn: $("retryBtn"),
  entry: $("entryView"),
  result: $("resultView"),

  client: $("clientId"),
  patientName: $("patientName"),
  suggestions: $("patientSuggestions"),
  existingChip: $("existingChip"),
  existingCode: $("existingCode"),
  clearPatientBtn: $("clearPatientBtn"),
  age: $("age"),
  gender: $("gender"),
  mobile: $("mobileNumber"),

  testSearch: $("testSearch"),
  testList: $("testList"),
  testEmpty: $("testEmpty"),

  slipMeta: $("slipMeta"),
  slipItems: $("slipItems"),
  slipEmpty: $("slipEmpty"),
  total: $("totalAmount"),
  submitError: $("submitError"),
  saveBtn: $("saveBtn"),
};

const FIELD_INPUTS = {
  clientId: els.client,
  patientName: els.patientName,
  age: els.age,
  gender: els.gender,
  mobileNumber: els.mobile,
};

/* ------------------------------------------------------------------- state */
const state = {
  clients: [],
  tests: [],               // tests currently shown in the catalogue list
  selected: new Map(),     // testId -> { test, qty }   (kept even when a search hides the test)
  patient: null,           // existing patient chosen from suggestions, or null for a new patient
  suggestions: [],
  saving: false,
};

/* ----------------------------------------------------------------- helpers */
function esc(value) {
  return String(value ?? "").replace(/[&<>"']/g, (c) => (
    { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]
  ));
}

function debounce(fn, ms) {
  let timer;
  return (...args) => {
    clearTimeout(timer);
    timer = setTimeout(() => fn(...args), ms);
  };
}

function tubeColor(category) {
  return TUBE_COLORS[category] || DEFAULT_TUBE_COLOR;
}

/* --------------------------------------------------------------------- API */
class ApiError extends Error {
  constructor(messages, status) {
    super(messages.join(" "));
    this.messages = messages;
    this.status = status;
  }
}

// Handles both error shapes the API returns:
//   400 -> { errors: { Field: ["message", ...] } }   (ASP.NET validation problem)
//   404/409/500 -> { status, message }
function messagesFrom(body, status) {
  if (body && body.errors && typeof body.errors === "object") {
    const list = Object.values(body.errors).flat().filter(Boolean);
    if (list.length) return list;
  }
  if (body && body.message) return [body.message];
  if (body && (body.detail || body.title)) return [body.detail || body.title];
  return [`The request failed (HTTP ${status}).`];
}

function errorTexts(err) {
  return err instanceof ApiError ? err.messages : [err && err.message ? err.message : "Something went wrong."];
}

async function api(path, options = {}) {
  const headers = { Accept: "application/json" };
  if (options.body) headers["Content-Type"] = "application/json";

  let response;
  try {
    response = await fetch(API_BASE + path, { ...options, headers });
  } catch {
    throw new ApiError([`Cannot reach the API at ${API_BASE}. Start the backend and check API_BASE in app.js.`], 0);
  }

  let body = null;
  const text = await response.text();
  if (text) {
    try { body = JSON.parse(text); } catch { body = null; }
  }

  if (!response.ok) throw new ApiError(messagesFrom(body, response.status), response.status);
  return body;
}

/* --------------------------------------------------------- status / banners */
function setApiStatus(ok) {
  els.apiStatus.className = "api-status " + (ok ? "ok" : "down");
  els.apiStatus.textContent = ok ? "API connected" : "API unreachable";
}

function showGlobalBanner(text) {
  els.globalBannerText.textContent = text;
  els.globalBanner.hidden = false;
}

function hideGlobalBanner() {
  els.globalBanner.hidden = true;
}

function showSubmitError(messages) {
  els.submitError.innerHTML = messages.length === 1
    ? esc(messages[0])
    : `<ul>${messages.map((m) => `<li>${esc(m)}</li>`).join("")}</ul>`;
  els.submitError.hidden = false;
}

function hideSubmitError() {
  els.submitError.hidden = true;
  els.submitError.textContent = "";
}

/* ------------------------------------------------------------------ clients */
async function loadClients() {
  try {
    state.clients = await api("/clients");
    els.client.innerHTML =
      '<option value="">Select a client</option>' +
      state.clients
        .map((c) => `<option value="${c.clientId}">${esc(c.clientName)} (${esc(c.clientCode)})</option>`)
        .join("");
    setApiStatus(true);
    hideGlobalBanner();
    return true;
  } catch (err) {
    setApiStatus(false);
    els.client.innerHTML = '<option value="">Clients unavailable</option>';
    showGlobalBanner(errorTexts(err)[0]);
    return false;
  }
}

/* -------------------------------------------------------------- test catalog */
let testRequestSeq = 0;

async function loadTests(term = "") {
  const seq = ++testRequestSeq;
  try {
    const tests = await api("/testmaster?search=" + encodeURIComponent(term));
    if (seq !== testRequestSeq) return;            // a newer search already started
    state.tests = tests;
    els.testEmpty.textContent = "No tests match that search.";
    renderTests();
  } catch (err) {
    if (seq !== testRequestSeq) return;
    state.tests = [];
    els.testEmpty.textContent = errorTexts(err)[0];
    renderTests();
  }
}

function renderTests() {
  els.testEmpty.hidden = state.tests.length > 0;
  els.testList.hidden = state.tests.length === 0;

  els.testList.innerHTML = state.tests.map((t) => {
    const on = state.selected.has(t.testId);
    return `
      <li>
        <button type="button" class="test-row${on ? " is-selected" : ""}" data-id="${t.testId}"
                aria-pressed="${on}" style="--tube:${tubeColor(t.category)}">
          <span class="tube" aria-hidden="true"></span>
          <span class="test-main">
            <span class="test-name">${esc(t.testName)}</span>
            <span class="test-sub"><span>${esc(t.testCode)}</span>${t.category ? `<span>${esc(t.category)}</span>` : ""}</span>
          </span>
          <span class="test-rate">${money(toPaise(t.rate))}</span>
          <span class="test-action">${on ? "Added" : "Add"}</span>
        </button>
      </li>`;
  }).join("");
}

function toggleTest(testId) {
  if (state.selected.has(testId)) {
    state.selected.delete(testId);
  } else {
    const test = state.tests.find((t) => t.testId === testId);
    if (!test) return;
    state.selected.set(testId, { test, qty: 1 });
  }
  setError("tests", "");
  hideSubmitError();
  renderTests();
  renderSlip();
}

/* ---------------------------------------------------------------- order slip */
function renderSlip() {
  const items = [...state.selected.values()];
  els.slipEmpty.hidden = items.length > 0;

  els.slipItems.innerHTML = items.map(({ test, qty }) => {
    const name = esc(test.testName);
    return `
      <li class="slip-item" data-id="${test.testId}">
        <div class="slip-line">
          <span class="slip-name">${name}</span>
          <span class="slip-amt">${money(toPaise(test.rate) * qty)}</span>
        </div>
        <div class="slip-line sub">
          <span class="slip-rate">${money(toPaise(test.rate))} each</span>
          <span class="qty">
            <button type="button" class="qty-btn" data-act="dec" aria-label="Decrease quantity of ${name}">&minus;</button>
            <input class="qty-input" type="number" min="1" max="99" value="${qty}" inputmode="numeric" aria-label="Quantity of ${name}">
            <button type="button" class="qty-btn" data-act="inc" aria-label="Increase quantity of ${name}">+</button>
          </span>
          <button type="button" class="link-btn" data-act="remove" aria-label="Remove ${name}">Remove</button>
        </div>
      </li>`;
  }).join("");

  updateTotals();
}

function updateTotals() {
  const items = [...state.selected.values()];
  const paise = items.reduce((sum, { test, qty }) => sum + toPaise(test.rate) * qty, 0);
  els.total.textContent = money(paise);

  const client = state.clients.find((c) => String(c.clientId) === els.client.value);
  const count = items.length;
  const parts = [client ? client.clientName : "No client selected"];
  if (count) parts.push(`${count} test${count === 1 ? "" : "s"}`);
  els.slipMeta.textContent = parts.join(", ");
}

// Update one slip row in place (keeps keyboard focus on the +/- buttons).
function updateLine(li) {
  const item = state.selected.get(Number(li.dataset.id));
  if (!item) return;
  li.querySelector(".qty-input").value = item.qty;
  li.querySelector(".slip-amt").textContent = money(toPaise(item.test.rate) * item.qty);
  updateTotals();
}

els.slipItems.addEventListener("click", (e) => {
  const btn = e.target.closest("[data-act]");
  if (!btn) return;
  const li = btn.closest(".slip-item");
  const id = Number(li.dataset.id);
  const item = state.selected.get(id);
  if (!item) return;

  if (btn.dataset.act === "remove") {
    state.selected.delete(id);
    renderSlip();
    renderTests();
    return;
  }

  item.qty = clamp(item.qty + (btn.dataset.act === "inc" ? 1 : -1), 1, 99);
  updateLine(li);
});

// Typing a quantity updates the total live; blur/enter normalises the field.
els.slipItems.addEventListener("input", (e) => {
  if (!e.target.classList.contains("qty-input")) return;
  const li = e.target.closest(".slip-item");
  const item = state.selected.get(Number(li.dataset.id));
  const n = parseInt(e.target.value, 10);
  if (!item || Number.isNaN(n) || n < 1 || n > 99) return;
  item.qty = n;
  li.querySelector(".slip-amt").textContent = money(toPaise(item.test.rate) * item.qty);
  updateTotals();
});

els.slipItems.addEventListener("change", (e) => {
  if (!e.target.classList.contains("qty-input")) return;
  const li = e.target.closest(".slip-item");
  const item = state.selected.get(Number(li.dataset.id));
  if (!item) return;
  const n = parseInt(e.target.value, 10);
  if (!Number.isNaN(n)) item.qty = clamp(n, 1, 99);
  updateLine(li);
});

els.testList.addEventListener("click", (e) => {
  const row = e.target.closest(".test-row");
  if (row) toggleTest(Number(row.dataset.id));
});

els.testSearch.addEventListener("input", debounce(() => loadTests(els.testSearch.value.trim()), SEARCH_DEBOUNCE_MS));

/* ------------------------------------------------- existing patient look-up */
function setPatientFieldsLocked(locked) {
  els.patientName.readOnly = locked;
  els.age.readOnly = locked;
  els.mobile.readOnly = locked;
  els.gender.disabled = locked;
}

function hideSuggestions() {
  els.suggestions.hidden = true;
  els.suggestions.innerHTML = "";
  state.suggestions = [];
}

function renderSuggestions(list) {
  if (!list.length) {
    hideSuggestions();
    return;
  }
  state.suggestions = list;
  els.suggestions.innerHTML = list.map((p, i) => `
    <li role="option">
      <button type="button" data-i="${i}">
        <span class="s-name">${esc(p.patientName)}</span>
        <span class="s-meta">${esc(p.patientCode)}, ${esc(p.age)} y, ${esc(p.gender)}, ${esc(p.mobileNumber)}</span>
      </button>
    </li>`).join("");
  els.suggestions.hidden = false;
}

const searchPatients = debounce(async () => {
  const term = els.patientName.value.trim();
  const clientId = els.client.value;
  if (state.patient || !clientId || term.length < 2) {
    hideSuggestions();
    return;
  }
  try {
    const list = await api(`/patients?clientId=${encodeURIComponent(clientId)}&search=${encodeURIComponent(term)}`);
    if (state.patient || els.patientName.value.trim() !== term) return;   // input moved on
    renderSuggestions(list);
  } catch {
    hideSuggestions();   // look-up is a convenience; typing a new patient still works
  }
}, SEARCH_DEBOUNCE_MS);

function selectPatient(p) {
  state.patient = p;
  els.patientName.value = p.patientName;
  els.age.value = p.age;
  els.gender.value = String(GENDER_VALUE[p.gender] ?? "");
  els.mobile.value = p.mobileNumber;
  setPatientFieldsLocked(true);
  els.existingCode.textContent = p.patientCode;
  els.existingChip.hidden = false;
  hideSuggestions();
  clearErrors(["patientName", "age", "gender", "mobileNumber"]);
}

function clearPatient(focus = true) {
  state.patient = null;
  setPatientFieldsLocked(false);
  els.existingChip.hidden = true;
  els.patientName.value = "";
  els.age.value = "";
  els.gender.value = "";
  els.mobile.value = "";
  if (focus) els.patientName.focus();
}

els.patientName.addEventListener("input", searchPatients);
els.patientName.addEventListener("keydown", (e) => {
  if (e.key === "Escape") hideSuggestions();
});
els.suggestions.addEventListener("click", (e) => {
  const btn = e.target.closest("button[data-i]");
  if (btn) selectPatient(state.suggestions[Number(btn.dataset.i)]);
});
els.clearPatientBtn.addEventListener("click", () => clearPatient(true));
document.addEventListener("click", (e) => {
  if (!e.target.closest(".lookup")) hideSuggestions();
});

els.client.addEventListener("change", () => {
  if (state.patient) clearPatient(false);   // a patient belongs to exactly one client
  hideSuggestions();
  setError("clientId", "");
  updateTotals();
});

// Digits only in the mobile field.
els.mobile.addEventListener("input", () => {
  els.mobile.value = els.mobile.value.replace(/\D/g, "");
});

/* --------------------------------------------------------------- validation */
function setError(field, message) {
  const p = document.querySelector(`.err[data-for="${field}"]`);
  if (p) p.textContent = message || "";
  const input = FIELD_INPUTS[field];
  if (!input) return;
  if (message) {
    input.setAttribute("aria-invalid", "true");
    input.setAttribute("aria-describedby", "err-" + field);
  } else {
    input.removeAttribute("aria-invalid");
    input.removeAttribute("aria-describedby");
  }
}

function clearErrors(fields = [...Object.keys(FIELD_INPUTS), "tests"]) {
  fields.forEach((f) => setError(f, ""));
}

Object.entries(FIELD_INPUTS).forEach(([field, input]) => {
  input.addEventListener("input", () => setError(field, ""));
  input.addEventListener("change", () => setError(field, ""));
});

function validate() {
  const errors = {};

  if (!els.client.value) errors.clientId = "Select a client.";

  if (!state.patient) {
    const name = els.patientName.value.trim();
    if (name.length < 2) errors.patientName = "Enter the patient's name (at least 2 characters).";

    const age = els.age.value.trim();
    if (!/^\d+$/.test(age) || Number(age) > 120) errors.age = "Enter an age from 0 to 120.";

    if (els.gender.value === "") errors.gender = "Select a gender.";

    if (!MOBILE_PATTERN.test(els.mobile.value.trim())) {
      errors.mobileNumber = "Enter a 10-digit mobile number starting with 6, 7, 8 or 9.";
    }
  }

  if (state.selected.size === 0) errors.tests = "Add at least one test.";

  return errors;
}

/* ------------------------------------------------------------------- submit */
function buildPayload() {
  const payload = {
    clientId: Number(els.client.value),
    tests: [...state.selected.values()].map(({ test, qty }) => ({ testId: test.testId, quantity: qty })),
  };

  if (state.patient) {
    payload.patientId = state.patient.patientId;
  } else {
    payload.patientName = els.patientName.value.trim();
    payload.age = Number(els.age.value);
    payload.gender = Number(els.gender.value);
    payload.mobileNumber = els.mobile.value.trim();
  }
  return payload;
}

function setSaving(saving) {
  state.saving = saving;
  els.saveBtn.disabled = saving;
  els.saveBtn.textContent = saving ? "Saving…" : "Save work order";
}

async function submitOrder() {
  if (state.saving) return;
  hideSubmitError();
  clearErrors();

  const errors = validate();
  const fields = Object.keys(errors);
  if (fields.length) {
    fields.forEach((f) => setError(f, errors[f]));
    const firstInput = fields.map((f) => FIELD_INPUTS[f]).find(Boolean);
    if (firstInput) firstInput.focus();
    return;
  }

  setSaving(true);
  try {
    const order = await api("/workorders", { method: "POST", body: JSON.stringify(buildPayload()) });
    showResult(order);
  } catch (err) {
    showSubmitError(errorTexts(err));
  } finally {
    setSaving(false);
  }
}

els.saveBtn.addEventListener("click", submitOrder);

/* ------------------------------------------------------------- saved screen */
function showResult(o) {
  const when = new Date(o.orderDate).toLocaleString("en-IN", { dateStyle: "medium", timeStyle: "short" });

  const rows = (o.tests || []).map((t) => `
      <tr>
        <td>${esc(t.testName)}</td>
        <td class="num">${money(toPaise(t.rate))}</td>
        <td class="num">${esc(t.quantity)}</td>
        <td class="num">${money(toPaise(t.amount))}</td>
      </tr>`).join("");

  els.result.innerHTML = `
    <article class="receipt" aria-labelledby="receiptTitle">
      <h2 id="receiptTitle" tabindex="-1">Work order saved</h2>
      <p class="woe-number">${esc(o.woeNumber)}</p>

      <dl class="facts">
        <div><dt>Client</dt><dd>${esc(o.clientName)}</dd></div>
        <div><dt>Patient</dt><dd>${esc(o.patientName)} <span class="nowrap">(${esc(o.patientCode)})</span></dd></div>
        <div><dt>Age and gender</dt><dd>${esc(o.age)} years, ${esc(o.gender)}</dd></div>
        <div><dt>Mobile</dt><dd>${esc(o.mobileNumber)}</dd></div>
        <div><dt>Saved on</dt><dd><span class="nowrap">${esc(when)}</span></dd></div>
        <div><dt>Status</dt><dd>${esc(o.status)}</dd></div>
      </dl>

      <table>
        <thead>
          <tr>
            <th scope="col">Test</th>
            <th scope="col" class="num">Rate</th>
            <th scope="col" class="num">Qty</th>
            <th scope="col" class="num">Amount</th>
          </tr>
        </thead>
        <tbody>${rows}</tbody>
      </table>

      <div class="receipt-total"><span>Total</span><strong>${money(toPaise(o.totalAmount))}</strong></div>

      <div class="actions">
        <button type="button" class="btn primary" data-act="new">New work order</button>
        <button type="button" class="btn" data-act="print">Print</button>
      </div>
    </article>`;

  els.entry.hidden = true;
  els.result.hidden = false;
  window.scrollTo(0, 0);
  $("receiptTitle").focus();
}

els.result.addEventListener("click", (e) => {
  const btn = e.target.closest("[data-act]");
  if (!btn) return;
  if (btn.dataset.act === "new") resetAll();
  if (btn.dataset.act === "print") window.print();
});

function resetAll() {
  state.selected.clear();
  clearPatient(false);
  hideSuggestions();
  clearErrors();
  hideSubmitError();

  els.client.value = "";
  els.testSearch.value = "";

  renderSlip();
  loadTests("");

  els.result.hidden = true;
  els.result.innerHTML = "";
  els.entry.hidden = false;
  window.scrollTo(0, 0);
  els.client.focus();
}

/* --------------------------------------------------------------------- init */
async function init() {
  document.querySelectorAll(".err").forEach((p) => { p.id = "err-" + p.dataset.for; });
  await Promise.all([loadClients(), loadTests("")]);
  updateTotals();
}

els.retryBtn.addEventListener("click", init);
renderSlip();
init();
