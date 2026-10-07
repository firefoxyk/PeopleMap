import { startAnalytics, track } from "./analytics.js";
import { startInteractions } from "./interactions.js";

document.documentElement.classList.add("js");
document.querySelector("[data-current-year]").textContent = new Date().getFullYear();

const header = document.querySelector("[data-header]");
const updateHeader = () => header.classList.toggle("scrolled", scrollY > 24);
addEventListener("scroll", updateHeader, { passive: true });
updateHeader();

const reducedMotion = matchMedia("(prefers-reduced-motion: reduce)").matches;
if (reducedMotion) document.querySelectorAll(".reveal").forEach(item => item.classList.add("visible"));
else {
  const revealObserver = new IntersectionObserver(entries => entries.forEach(entry => {
    if (entry.isIntersecting) { entry.target.classList.add("visible"); revealObserver.unobserve(entry.target); }
  }), { threshold: .12 });
  document.querySelectorAll(".reveal").forEach(item => revealObserver.observe(item));
}

document.querySelectorAll("[data-cta]").forEach(link => link.addEventListener("click", () => {
  const source = link.dataset.cta; track(source === "hero" ? "hero_cta_clicked" : "early_access_clicked", { section: source, element: "cta" });
}));

startInteractions();
startAnalytics();

// Re-apply deep links after layout and module initialization so section offsets
// remain reliable when the page is opened directly at an anchor.
if (location.hash) {
  addEventListener("load", () => setTimeout(() => document.querySelector(location.hash)?.scrollIntoView({ block: "start" }), 50), { once: true });
}
