import { isDemoMode } from "./mode.js";

const storageKeys = { visitor: "pm_visitor_id", session: "pm_session" };
const experiment = { experimentId: "hero-copy-001", variantId: "A" };
const queue = [];

function uuid() {
  return crypto.randomUUID?.() ?? `${Date.now()}-${Math.random().toString(16).slice(2)}`;
}

function visitorId() {
  let value = localStorage.getItem(storageKeys.visitor);
  if (!value) { value = uuid(); localStorage.setItem(storageKeys.visitor, value); }
  return value;
}

function sessionId() {
  const now = Date.now();
  const saved = JSON.parse(sessionStorage.getItem(storageKeys.session) || "null");
  if (saved?.id && now - saved.lastSeen < 30 * 60 * 1000) {
    sessionStorage.setItem(storageKeys.session, JSON.stringify({ id: saved.id, lastSeen: now }));
    return saved.id;
  }
  const id = uuid();
  sessionStorage.setItem(storageKeys.session, JSON.stringify({ id, lastSeen: now }));
  return id;
}

const visitor = isDemoMode ? uuid() : visitorId();
const session = isDemoMode ? uuid() : sessionId();
const params = new URLSearchParams(location.search);

function deviceType() {
  if (matchMedia("(max-width: 620px)").matches) return "mobile";
  if (matchMedia("(max-width: 900px)").matches) return "tablet";
  return "desktop";
}

export function track(eventName, details = {}) {
  if (isDemoMode) return;
  queue.push({
    eventId: uuid(), visitorId: visitor, sessionId: session, eventName,
    occurredAtUtc: new Date().toISOString(), page: location.pathname,
    section: details.section ?? null, element: details.element ?? null,
    value: details.value == null ? null : String(details.value), referrer: document.referrer || null,
    landingUrl: location.href, utmSource: params.get("utm_source"), utmMedium: params.get("utm_medium"),
    utmCampaign: params.get("utm_campaign"), utmContent: params.get("utm_content"), utmTerm: params.get("utm_term"),
    country: null, language: navigator.language, deviceType: deviceType(),
    os: navigator.platform || null, browser: navigator.userAgentData?.brands?.[0]?.brand || null,
    screenWidth: screen.width, screenHeight: screen.height, ...experiment
  });
  if (queue.length >= 10) flush();
}

export async function flush(useBeacon = false) {
  if (isDemoMode) return;
  if (!queue.length) return;
  const events = queue.splice(0, 100);
  const body = JSON.stringify({ events });
  try {
    if (useBeacon && navigator.sendBeacon) {
      navigator.sendBeacon("/api/analytics/events", new Blob([body], { type: "application/json" }));
      return;
    }
    const response = await fetch("/api/analytics/events", { method: "POST", headers: { "Content-Type": "application/json" }, body, keepalive: true });
    if (!response.ok) queue.unshift(...events);
  } catch { queue.unshift(...events); }
}

export function signupContext(sourceSection) {
  return {
    visitorId: visitor, sessionId: session, sourceSection,
    utmSource: params.get("utm_source"), utmMedium: params.get("utm_medium"),
    utmCampaign: params.get("utm_campaign"), ...experiment
  };
}

export function startAnalytics() {
  if (isDemoMode) return;
  track("session_started"); track("page_view");
  const seenSections = new Set();
  const observer = new IntersectionObserver(entries => entries.forEach(entry => {
    const section = entry.target.dataset.section;
    if (entry.isIntersecting && !seenSections.has(section)) {
      seenSections.add(section); track("section_viewed", { section });
    }
  }), { threshold: .35 });
  document.querySelectorAll("[data-section]").forEach(section => observer.observe(section));

  const reached = new Set();
  addEventListener("scroll", () => {
    const available = document.documentElement.scrollHeight - innerHeight;
    if (available <= 0) return;
    const depth = Math.round(scrollY / available * 100);
    [25, 50, 75, 90, 100].forEach(mark => {
      if (depth >= mark && !reached.has(mark)) { reached.add(mark); track("scroll_depth", { value: mark }); }
    });
  }, { passive: true });
  setInterval(() => { if (!document.hidden) { track("engagement_heartbeat", { value: 30 }); flush(); } }, 30000);
  setInterval(() => flush(), 8000);
  addEventListener("pagehide", () => flush(true));
}
