import { signupContext, track } from "./analytics.js";
import { isDemoMode } from "./mode.js";

const people = [
  { name: "Emily Carter", initials: "EC", kind: "photo-emily", role: "Product designer", location: "London", tags: ["Work", "Northstar", "Design"], status: "Trusted", priority: 4, added: 8 },
  { name: "Daniel Kim", initials: "DK", kind: "blue", role: "Engineering lead", location: "New York", tags: ["Work", "Northstar", "Tech"], status: "Trusted", priority: 5, added: 7 },
  { name: "Olivia Brown", initials: "OB", kind: "gold", role: "Friend", location: "Austin", tags: ["Personal", "Friends"], status: "Trusted", priority: 3, added: 6 },
  { name: "James Wilson", initials: "JW", kind: "green", role: "Consultant", location: "Chicago", tags: ["Work", "Acme"], status: "On hold", priority: 2, added: 5 },
  { name: "Sophia Lee", initials: "SL", kind: "rose", role: "Family", location: "Boston", tags: ["Personal", "Family"], status: "Trusted", priority: 5, added: 4 },
  { name: "Maya Brooks", initials: "MB", kind: "gold", role: "Studio founder", location: "Brooklyn", tags: ["Work", "Design"], status: "Problematic", priority: 1, added: 3 },
  { name: "Alex Morgan", initials: "AM", kind: "blue", role: "University friend", location: "Seattle", tags: [], status: "Unrated", priority: 3, added: 2 },
  { name: "Noah Harris", initials: "NH", kind: "green", role: "Cousin", location: "Portland", tags: [], status: "Unrated", priority: 4, added: 1 }
];

function initHierarchy() {
  const root = document.querySelector("[data-hierarchy]");
  if (!root) return;
  const path = root.querySelector("[data-selected-path]");
  const confirmation = root.querySelector("[data-apply-confirmation]");
  let selected = "INDUSTRY / DESIGN";
  const selectBranch = button => {
    root.querySelectorAll("[data-branch]").forEach(item => { item.classList.toggle("active", item === button); item.setAttribute("aria-selected", String(item === button)); });
    selected = button.dataset.branch;
    const parts = selected.split(" / ");
    path.innerHTML = ["WORK", ...parts].filter((part, index, all) => index === 0 || part !== all[index - 1]).map((part, index, all) => index === all.length - 1 ? `<b>${part}</b>` : `<span>${part}</span><i>›</i>`).join("");
    confirmation.textContent = "";
    track("feature_interaction", { section: "hierarchical_tags", element: "branch", value: selected });
  };
  root.querySelectorAll("[data-branch]").forEach(button => button.addEventListener("click", () => selectBranch(button)));
  root.querySelector("[data-add-custom-tag]").addEventListener("click", () => {
    const custom = root.querySelector(".custom-branch"); custom.hidden = false; selectBranch(custom.querySelector("[data-branch]"));
  });
  root.querySelector("[data-apply-tag]").addEventListener("click", () => {
    confirmation.textContent = `${selected} added to Emily.`;
    track("feature_interaction", { section: "hierarchical_tags", element: "apply", value: selected });
  });
}

function initStatus() {
  const root = document.querySelector("[data-status-selector]");
  if (!root) return;
  const current = root.querySelector("[data-current-status]");
  const options = [...root.querySelectorAll("[data-status]")];
  const statuses = {
    "Trusted": { modifier: "status-trusted" },
    "On hold": { modifier: "status-hold" },
    "Problematic": { modifier: "status-problematic" },
    "Unrated": { modifier: "status-unrated" }
  };
  const selectStatus = (button, moveFocus = false) => {
    const label = button.dataset.status;
    const status = statuses[label];
    if (!status) return;

    options.forEach(item => {
      const selected = item === button;
      item.classList.toggle("active", selected);
      item.setAttribute("aria-pressed", String(selected));
      item.querySelector("em").textContent = selected ? "✓" : "";
    });
    current.className = `status-current ${status.modifier}`;
    current.innerHTML = `<i></i>${label}`;
    if (moveFocus) button.focus();
    track("feature_interaction", { section: "relationship_status", element: "status", value: label });
  };

  options.forEach(button => button.addEventListener("click", () => selectStatus(button)));
  root.querySelector("fieldset").addEventListener("keydown", event => {
    if (!["ArrowDown", "ArrowRight", "ArrowUp", "ArrowLeft", "Home", "End"].includes(event.key)) return;
    event.preventDefault();
    const currentIndex = Math.max(0, options.indexOf(document.activeElement));
    const nextIndex = event.key === "Home" ? 0 : event.key === "End" ? options.length - 1 :
      ["ArrowDown", "ArrowRight"].includes(event.key) ? (currentIndex + 1) % options.length : (currentIndex - 1 + options.length) % options.length;
    selectStatus(options[nextIndex], true);
  });
}

function initFilters() {
  const root = document.querySelector("[data-filter-builder]");
  if (!root) return;
  const count = root.querySelector("[data-result-count]");
  const label = root.querySelector("[data-result-label]");
  let noTags = false;
  const update = () => {
    if (noTags) { count.textContent = "31 people"; label.textContent = "have no tags yet"; return; }
    const active = root.querySelectorAll("[data-filter]:not(.removed)").length;
    count.textContent = `${active ? Math.max(7, 47 - active * 6) : 84} people`; label.textContent = "match your filters";
  };
  root.querySelectorAll("[data-filter]").forEach(token => token.addEventListener("click", () => {
    token.classList.toggle("removed"); token.style.display = token.classList.contains("removed") ? "none" : "";
    update(); track("feature_interaction", { section: "filters", element: "filter", value: token.dataset.filter });
  }));
  root.querySelectorAll("[data-operator]").forEach(operator => operator.addEventListener("click", () => {
    const values = ["AND", "OR", "NOT"]; operator.textContent = values[(values.indexOf(operator.textContent.trim()) + 1) % values.length];
    track("feature_interaction", { section: "filters", element: "operator", value: operator.textContent });
  }));
  root.querySelector("[data-reset-filter]").addEventListener("click", () => {
    noTags = false; root.classList.remove("no-tags-mode");
    root.querySelectorAll("[data-quick-filter]").forEach(button => { const active = button.dataset.quickFilter === "all"; button.classList.toggle("active", active); button.setAttribute("aria-pressed", String(active)); });
    root.querySelectorAll("[data-filter]").forEach(token => { token.classList.remove("removed"); token.style.display = ""; }); update();
  });
  root.querySelector("[data-add-filter]").addEventListener("click", () => {
    const removed = root.querySelector("[data-filter].removed"); if (removed) { removed.classList.remove("removed"); removed.style.display = ""; update(); }
  });
  root.querySelectorAll("[data-quick-filter]").forEach(button => button.addEventListener("click", () => {
    noTags = button.dataset.quickFilter === "no-tags";
    root.classList.toggle("no-tags-mode", noTags);
    root.querySelectorAll("[data-quick-filter]").forEach(item => { const active = item === button; item.classList.toggle("active", active); item.setAttribute("aria-pressed", String(active)); });
    update(); track("feature_interaction", { section: "filters", element: "quick_filter", value: button.dataset.quickFilter });
  }));
}

function initPeopleBrowser() {
  const root = document.querySelector("[data-people-browser]");
  if (!root) return;
  let view = "Business"; let noTagsOnly = false;
  const grid = root.querySelector("[data-people-grid]"); const sort = root.querySelector("[data-sort]");
  const title = root.querySelector("[data-view-title]"); const rule = root.querySelector("[data-view-rule]"); const status = root.querySelector("[data-view-status]");
  const configs = {
    "Business": { rule: "WORK / COMPANY", status: "TRUSTED", filter: person => person.tags.includes("Work") && person.status === "Trusted" },
    "Personal": { rule: "PERSONAL", status: "ALL STATUS", filter: person => person.tags.includes("Personal") },
    "By area": { rule: "LOCATION", status: "ALL STATUS", filter: () => true },
    "Problematic": { rule: "ALL TAGS", status: "PROBLEMATIC", filter: person => person.status === "Problematic" },
    "Follow up": { rule: "PRIORITY", status: "ON HOLD", filter: person => person.priority >= 4 || person.status === "On hold" },
    "Unsorted": { rule: "NO TAGS", status: "UNRATED", filter: person => person.tags.length === 0 }
  };
  const render = () => {
    const config = configs[view];
    let result = people.filter(person => noTagsOnly ? person.tags.length === 0 : config.filter(person));
    result = [...result].sort((a,b) => sort.value === "alpha" ? a.name.localeCompare(b.name) : sort.value === "priority" ? b.priority-a.priority : sort.value === "oldest" ? a.added-b.added : b.added-a.added);
    title.textContent = noTagsOnly ? "No tags" : view; rule.textContent = noTagsOnly ? "NO TAGS" : config.rule; status.textContent = noTagsOnly ? "NEEDS REVIEW" : config.status;
    grid.innerHTML = result.length ? result.map(person => `<article class="person-card"><div class="person-card-top"><span class="avatar ${person.kind}">${person.initials}</span><span class="mini-status status-${person.status.toLowerCase().replace(" ", "-")}"><i></i>${person.status}</span></div><b>${person.name}</b><small>${view === "By area" ? person.location : `${person.role} · ${person.location}`}</small><div class="card-tags">${person.tags.length ? person.tags.join(" / ").toUpperCase() : "NO TAGS"}</div></article>`).join("") : `<p class="empty-message">No people in this view yet.</p>`;
  };
  root.querySelectorAll("[data-view]").forEach(button => button.addEventListener("click", () => {
    root.querySelectorAll("[data-view]").forEach(item => { item.classList.remove("active"); item.setAttribute("aria-selected", "false"); });
    button.classList.add("active"); button.setAttribute("aria-selected", "true"); view = button.dataset.view; noTagsOnly = false; root.querySelector("[data-no-tags-filter]").classList.remove("active"); render();
    track("feature_interaction", { section: "saved_views", element: "view", value: view });
  }));
  root.querySelector("[data-no-tags-filter]").addEventListener("click", event => { noTagsOnly = !noTagsOnly; event.currentTarget.classList.toggle("active", noTagsOnly); event.currentTarget.setAttribute("aria-pressed", String(noTagsOnly)); render(); track("feature_interaction", { section: "saved_views", element: "no_tags", value: noTagsOnly }); });
  sort.addEventListener("change", () => { render(); track("feature_interaction", { section: "saved_views", element: "sort", value: sort.value }); });
  render();
}

function initGraph() {
  const graph = document.querySelector("[data-graph]"); if (!graph) return;
  document.querySelectorAll("[data-graph-filter]").forEach(button => button.addEventListener("click", () => {
    const type = button.dataset.graphFilter;
    document.querySelectorAll("[data-graph-filter]").forEach(item => item.classList.toggle("active", item === button));
    graph.querySelectorAll("[data-type]").forEach(item => item.classList.toggle("hidden", type !== "all" && !["all", type].includes(item.dataset.type)));
    track("feature_interaction", { section: "relationships", element: "graph", value: type });
  }));
}

function initTimeline() {
  const root = document.querySelector("[data-timeline]"); if (!root) return;
  document.querySelectorAll("[data-timeline-sort]").forEach(button => button.addEventListener("click", () => {
    const oldest = button.dataset.timelineSort === "oldest"; const years = root.querySelector("[data-timeline-years]");
    [...years.children].sort((a,b) => oldest ? a.dataset.year-b.dataset.year : b.dataset.year-a.dataset.year).forEach(item => years.append(item));
    document.querySelectorAll("[data-timeline-sort]").forEach(item => item.classList.toggle("active", item === button));
    track("feature_interaction", { section: "timeline", element: "sort", value: button.dataset.timelineSort });
  }));
}

function initSignup() {
  const form = document.querySelector("[data-signup-form]"); if (!form) return;
  const input = form.elements.email; const status = form.querySelector("[data-form-status]");
  let started = false;
  input.addEventListener("input", () => { if (!started) { started = true; track("email_form_started", { section: "final_cta" }); } });
  form.addEventListener("submit", async event => {
    event.preventDefault(); status.textContent = "";
    track("early_access_clicked", { section: "final_cta", element: "submit" });
    if (!input.validity.valid) { input.focus(); status.textContent = "Please enter a valid email address."; track("email_form_failed", { section: "final_cta", value: "validation" }); return; }
    if (isDemoMode) { status.textContent = "This is a demo. Your request was not sent, and your email was not saved."; return; }
    const button = form.querySelector("button"); button.disabled = true; button.textContent = "Joining…";
    try {
      const response = await fetch("/api/early-access", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ email: input.value, ...signupContext("final_cta") }) });
      if (!response.ok) throw new Error("request_failed");
      form.classList.add("success"); status.textContent = "You're in. We'll let you know when PeopleMap is ready."; track("email_form_submitted", { section: "final_cta" });
    } catch { status.textContent = "Something went wrong. Please try again."; button.disabled = false; button.innerHTML = "Join early access <span aria-hidden=\"true\">→</span>"; track("email_form_failed", { section: "final_cta", value: "network" }); }
  });
}

function initDemoMode() {
  if (!isDemoMode) return;
  document.documentElement.classList.add("demo-mode");
  const notice = document.createElement("aside");
  notice.className = "demo-notice";
  notice.setAttribute("role", "status");
  notice.textContent = "Demo mode — no data is sent or saved.";
  document.body.append(notice);

  const status = document.querySelector("[data-demo-link-status]");
  document.querySelectorAll("[data-demo-link]").forEach(link => link.addEventListener("click", event => {
    event.preventDefault();
    status.textContent = `${link.dataset.demoLink} is unavailable in this demo.`;
  }));
}

export function startInteractions() { initDemoMode(); initHierarchy(); initStatus(); initFilters(); initPeopleBrowser(); initGraph(); initTimeline(); initSignup(); }
