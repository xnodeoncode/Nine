window.themeManager = {
  setTheme: function (theme) {
    //console.log("Setting theme to:", theme);
    document.documentElement.setAttribute("data-bs-theme", theme);
    localStorage.setItem("theme", theme);

    // Force browser to recalculate CSS custom properties
    // by triggering a reflow on the root element
    document.documentElement.style.display = "none";
    void document.documentElement.offsetHeight; // Trigger reflow
    document.documentElement.style.display = "";

    // console.log(
    //   "Theme set. Current attribute:",
    //   document.documentElement.getAttribute("data-bs-theme")
    // );
  },

  getTheme: function () {
    const theme = localStorage.getItem("theme") || "light";
    //console.log("Getting theme from localStorage:", theme);
    return theme;
  },

  initTheme: function () {
    const savedTheme = this.getTheme();
    this.setTheme(savedTheme);
    const savedBrandTheme = this.getBrandTheme();
    this.setBrandTheme(savedBrandTheme);
    return savedTheme;
  },

  setBrandTheme: function (brandTheme) {
    console.log("setBrandTheme called with:", brandTheme);
    document.documentElement.setAttribute("data-brand-theme", brandTheme);
    localStorage.setItem("brandTheme", brandTheme);
    console.log(
      "Brand theme set. DOM attribute:",
      document.documentElement.getAttribute("data-brand-theme"),
    );
    console.log("localStorage value:", localStorage.getItem("brandTheme"));

    // Force browser to recalculate CSS custom properties
    document.documentElement.style.display = "none";
    void document.documentElement.offsetHeight; // Trigger reflow
    document.documentElement.style.display = "";
  },

  getBrandTheme: function () {
    const brandTheme = localStorage.getItem("brandTheme") || "obsidian";
    return brandTheme;
  },
};

// Initialize theme IMMEDIATELY (before DOMContentLoaded) to prevent flash
if (typeof localStorage !== "undefined") {
  const savedTheme = localStorage.getItem("theme") || "light";
  const savedBrandTheme = localStorage.getItem("brandTheme") || "obsidian";
  console.log("Initial theme load:", savedTheme, "Brand:", savedBrandTheme);
  document.documentElement.setAttribute("data-bs-theme", savedTheme);
  document.documentElement.setAttribute("data-brand-theme", savedBrandTheme);

  // Force multiple reflows to ensure CSS is applied
  // Using requestAnimationFrame to ensure it happens after browser paint
  document.documentElement.style.display = "none";
  void document.documentElement.offsetHeight; // Trigger reflow
  document.documentElement.style.display = "";

  // Double-check on next frame
  requestAnimationFrame(() => {
    if (
      document.documentElement.getAttribute("data-brand-theme") !==
      savedBrandTheme
    ) {
      document.documentElement.setAttribute(
        "data-brand-theme",
        savedBrandTheme,
      );
    }
    console.log(
      "Initial theme applied with reflow, verified:",
      document.documentElement.getAttribute("data-brand-theme"),
    );
  });

  // Watch for Blazor navigation and re-apply theme
  // This handles Interactive Server mode where components persist
  const observer = new MutationObserver(function (mutations) {
    let currentTheme = document.documentElement.getAttribute("data-bs-theme");
    let currentBrandTheme =
      document.documentElement.getAttribute("data-brand-theme");

    // If attributes are missing, restore from localStorage
    if (!currentTheme) {
      currentTheme = localStorage.getItem("theme") || "light";
      document.documentElement.setAttribute("data-bs-theme", currentTheme);
      console.log("Restored theme attribute:", currentTheme);
    }

    if (!currentBrandTheme) {
      currentBrandTheme = localStorage.getItem("brandTheme") || "obsidian";
      document.documentElement.setAttribute(
        "data-brand-theme",
        currentBrandTheme,
      );
      console.log("Restored brand theme attribute:", currentBrandTheme);
    }

    // Re-trigger reflow to ensure CSS variables are applied
    document.documentElement.style.display = "none";
    void document.documentElement.offsetHeight;
    document.documentElement.style.display = "";
    //console.log("Theme re-applied after DOM mutation:", currentTheme, currentBrandTheme);
  });

  // Start observing after a short delay to let Blazor initialize
  setTimeout(() => {
    observer.observe(document.documentElement, {
      attributes: true,
      attributeFilter: ["data-bs-theme", "data-brand-theme"],
    });
  }, 1000);

  // Also ensure theme is applied after DOM is fully loaded
  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", () => {
      const currentTheme =
        document.documentElement.getAttribute("data-bs-theme") ||
        localStorage.getItem("theme") ||
        "light";
      const currentBrandTheme =
        document.documentElement.getAttribute("data-brand-theme") ||
        localStorage.getItem("brandTheme") ||
        "obsidian";

      document.documentElement.setAttribute("data-bs-theme", currentTheme);
      document.documentElement.setAttribute(
        "data-brand-theme",
        currentBrandTheme,
      );

      // Force reflow
      document.documentElement.style.display = "none";
      void document.documentElement.offsetHeight;
      document.documentElement.style.display = "";

      console.log(
        "DOMContentLoaded - Theme re-applied:",
        currentTheme,
        currentBrandTheme,
      );
    });
  }
}
