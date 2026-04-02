(function () {
  function setupLoginGuard() {
    // Identify the login form by its email field (specific to Login.razor)
    var emailInput = document.getElementById("Input.Email");
    if (!emailInput) return;

    var form = emailInput.closest("form");
    if (!form || form._loginGuardAttached) return;
    form._loginGuardAttached = true;

    var submitted = false;
    form.addEventListener("submit", function (e) {
      if (submitted) {
        console.log(
          "Login form already submitted, preventing duplicate submission.",
        );
        e.preventDefault();
        e.stopImmediatePropagation();
        return false;
      }
      submitted = true;
      var btn = form.querySelector('button[type="submit"]');
      if (btn) {
        console.log("Disabling submit button and showing loading spinner.");
        btn.disabled = true;
        btn.classList.remove("btn-primary");
        btn.classList.add("btn-success");
        btn.innerHTML =
          '<span class="spinner-border spinner-border-sm me-2" role="status" aria-hidden="true"></span>Logging in...';
      }
    });
  }

  function init() {
    setupLoginGuard();
    // Re-attach after Blazor enhanced navigation (navigating to/from login page)
    document.addEventListener("blazor:navigated", setupLoginGuard);
  }

  if (document.readyState === "loading") {
    console.log("Waiting for DOMContentLoaded to initialize login guard...");
    document.addEventListener("DOMContentLoaded", init);
    console.log("Login guard will be initialized on DOMContentLoaded.");
  } else {
    console.log(
      "Document already loaded, initializing login guard immediately.",
    );
    init();
  }
})();
