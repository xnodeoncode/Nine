// Hands external http(s) links to the system browser (Electron build) instead of opening an app window.
// In a normal browser the endpoint returns 404 and the link opens as usual.
(function () {
  function externalLink(event) {
    if (event.defaultPrevented) return null;
    if (event.type === "click" && event.button !== 0) return null;
    if (event.type === "auxclick" && event.button !== 1) return null;

    var anchor = event.target && event.target.closest && event.target.closest("a[href]");
    if (!anchor) return null;

    var url;
    try {
      url = new URL(anchor.href, window.location.href);
    } catch (e) {
      return null;
    }

    if (url.protocol !== "http:" && url.protocol !== "https:") return null;
    if (url.origin === window.location.origin) return null;
    return url.href;
  }

  function handle(event) {
    var url = externalLink(event);
    if (!url) return;

    event.preventDefault();
    fetch("/api/external/open", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ url: url }),
    })
      .then(function (response) {
        if (response.status === 404) {
          window.open(url, "_blank", "noopener");
        }
      })
      .catch(function () {});
  }

  document.addEventListener("click", handle, true);
  document.addEventListener("auxclick", handle, true);
})();
