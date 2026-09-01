// Minimal, dependency-free interactivity: current-year footer + scroll-aware header.
document.querySelectorAll('[data-year]').forEach((el) => {
  el.textContent = new Date().getFullYear();
});

(function () {
  const header = document.querySelector('[data-header]');
  if (!header) return;
  let lastY = window.scrollY;

  window.addEventListener(
    'scroll',
    () => {
      const y = window.scrollY;
      if (y > lastY && y > 120) {
        header.style.transform = 'translateY(-100%)';
      } else {
        header.style.transform = 'translateY(0)';
      }
      lastY = y;
    },
    { passive: true }
  );
})();
