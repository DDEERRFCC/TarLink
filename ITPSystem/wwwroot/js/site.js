// Global page transition: fade out on internal link navigation.
(() => {
  const body = document.body;
  if (!body) {
    return;
  }

  const isPlainLeftClick = (event) =>
    event.button === 0 &&
    !event.metaKey &&
    !event.ctrlKey &&
    !event.shiftKey &&
    !event.altKey;

  document.addEventListener("click", (event) => {
    if (!isPlainLeftClick(event)) {
      return;
    }

    const anchor = event.target instanceof Element
      ? event.target.closest("a[href]")
      : null;

    if (!anchor) {
      return;
    }

    const href = anchor.getAttribute("href") || "";
    if (!href || href.startsWith("#") || href.startsWith("javascript:")) {
      return;
    }

    if (anchor.hasAttribute("download") || anchor.target === "_blank") {
      return;
    }

    const targetUrl = new URL(anchor.href, window.location.href);
    if (targetUrl.origin !== window.location.origin) {
      return;
    }

    event.preventDefault();
    body.classList.add("page-leaving");
    window.setTimeout(() => {
      window.location.href = targetUrl.href;
    }, 160);
  });
})();
