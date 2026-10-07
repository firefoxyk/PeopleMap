const wait = (milliseconds = 50) => new Promise(resolve => setTimeout(resolve, milliseconds));
const checks = [];
const check = (name, condition) => checks.push({ name, passed: Boolean(condition) });

await wait(150);

check("stylesheet loaded", [...document.styleSheets].some(sheet => sheet.href?.endsWith("/css/site.css")));
check("application initialized", document.querySelector("[data-current-year]")?.textContent === String(new Date().getFullYear()));
check("demo notice visible", document.querySelector(".demo-notice")?.textContent.includes("no data is sent"));

document.querySelector("[data-cta=hero]").click();
check("anchor navigation", location.hash === "#early-access");

document.querySelector("[data-add-custom-tag]").click();
document.querySelector("[data-apply-tag]").click();
check("hierarchical tags", document.querySelector("[data-apply-confirmation]").textContent.includes("added to Emily"));

document.querySelector("[data-status='On hold']").click();
check("relationship status", document.querySelector("[data-current-status]").textContent.includes("On hold"));

document.querySelector("[data-view='Personal']").click();
check("saved views", document.querySelector("[data-view-title]").textContent === "Personal" && document.querySelectorAll("[data-people-grid] .person-card").length > 0);

const savedViewSort = document.querySelector("[data-sort]");
savedViewSort.value = "oldest";
savedViewSort.dispatchEvent(new Event("change", { bubbles: true }));
check("saved view sorting", savedViewSort.value === "oldest");

const filterLine = document.querySelector(".filter-line");
const resetFilter = document.querySelector("[data-reset-filter]");
const resultCount = () => Number.parseInt(document.querySelector("[data-result-count]").textContent, 10);
const removeFilter = filter => filterLine.querySelector(`[data-filter="${filter}"]`).click();
const setLogic = (index, logic) => {
  const operator = filterLine.querySelectorAll("[data-operator]")[index];
  while (operator.dataset.logic !== logic) operator.click();
};
const filterSequence = () => [...filterLine.children]
  .filter(node => node.matches("[data-filter], [data-operator]"))
  .map(node => node.hasAttribute("data-filter") ? node.dataset.filter : node.dataset.logic);

check("default filter count", resultCount() === 3);
document.querySelector("[data-quick-filter='no-tags']").click();
check("no tags filter count", resultCount() === 3);
document.querySelector("[data-quick-filter='all']").click();

const trustedFilter = filterLine.querySelector('[data-filter="TRUSTED"]');
trustedFilter.previousElementSibling.click();
check("filter removal setup", filterSequence().join(" ") === "WORK AND NEW YORK OR TRUSTED NOT FORMER");
check("filter removal setup count", resultCount() === 9);
trustedFilter.click();
const sequenceAfterRemoval = filterSequence();
check("middle filter removal sequence", sequenceAfterRemoval.join(" ") === "WORK AND NEW YORK NOT FORMER");
check("no adjacent filter operators", !sequenceAfterRemoval.some((item, index) => ["AND", "OR", "NOT"].includes(item) && ["AND", "OR", "NOT"].includes(sequenceAfterRemoval[index + 1])));
check("middle filter removal count", resultCount() === 4);

resetFilter.click();
setLogic(0, "OR");
check("full expression OR count", resultCount() === 6);
setLogic(0, "NOT");
check("full expression NOT count", resultCount() === 2);

resetFilter.click();
removeFilter("TRUSTED");
removeFilter("FORMER");
check("WORK AND NEW YORK count", resultCount() === 6);
setLogic(0, "OR");
check("WORK OR NEW YORK count", resultCount() === 15);
check("filter preview remainder", document.querySelector(".result-faces i")?.textContent === "+12");

resetFilter.click();
removeFilter("NEW YORK");
removeFilter("FORMER");
check("WORK AND TRUSTED count", resultCount() === 7);

resetFilter.click();
removeFilter("TRUSTED");
removeFilter("FORMER");
setLogic(0, "NOT");
check("WORK NOT NEW YORK count", resultCount() === 5);

resetFilter.click();
removeFilter("TRUSTED");
check("WORK AND NEW YORK NOT FORMER count", resultCount() === 4);

resetFilter.click();
removeFilter("WORK");
removeFilter("NEW YORK");
removeFilter("TRUSTED");
removeFilter("FORMER");
filterLine.querySelector("[data-add-filter]").click();
check("re-added filter count", resultCount() === 6);

document.querySelector("[data-graph-filter='work']").click();
check("relationship graph", document.querySelector("[data-graph-filter='work']").classList.contains("active") && document.querySelectorAll("[data-graph] .hidden").length > 0);

document.querySelector("[data-timeline-sort='oldest']").click();
const timelineYears = [...document.querySelector("[data-timeline-years]").children].map(item => Number(item.dataset.year));
check("timeline sorting", timelineYears.every((year, index) => index === 0 || timelineYears[index - 1] <= year));

const form = document.querySelector("[data-signup-form]");
const email = form.elements.email;
const formStatus = form.querySelector("[data-form-status]");
email.value = "invalid";
form.dispatchEvent(new Event("submit", { bubbles: true, cancelable: true }));
check("email validation", formStatus.textContent === "Please enter a valid email address.");
email.value = "yura@example.com";
form.dispatchEvent(new Event("submit", { bubbles: true, cancelable: true }));
check("demo form message", formStatus.textContent === "This is a demo. Your request was not sent, and your email was not saved.");

location.hash = "identities";
document.querySelector("[data-demo-link='LinkedIn']").click();
check("demo social link", location.hash === "#identities" && document.querySelector("[data-demo-link-status]").textContent === "LinkedIn is unavailable in this demo.");

const storageKeys = [...Array(localStorage.length)].map((_, index) => localStorage.key(index));
check("no analytics storage", !storageKeys.some(key => key?.startsWith("pm_")));
check("no API calls", !window.__peopleMapTest.calls.some(url => String(url).includes("/api/")));
check("no JavaScript errors", window.__peopleMapTest.errors.length === 0);
check("responsive navigation visible", getComputedStyle(document.querySelector(".site-header nav")).display !== "none");
check("no horizontal overflow", document.documentElement.scrollWidth <= document.documentElement.clientWidth + 1);

const result = {
  passed: checks.every(item => item.passed),
  viewport: { width: innerWidth, height: innerHeight },
  checks,
  calls: window.__peopleMapTest.calls,
  errors: window.__peopleMapTest.errors
};
document.querySelector("#browser-smoke-result").textContent = JSON.stringify(result);
