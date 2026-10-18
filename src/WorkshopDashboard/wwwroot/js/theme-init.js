// Applies the saved theme before the page is drawn, so a dark theme doesn't flash light first.
// This file loads in <head> without "defer" for that reason. Keep it small.
(function () {
  "use strict";

  try {
    var theme = window.localStorage.getItem("ghaction-workshop:theme");
    if (theme === "light" || theme === "dark") {
      document.documentElement.setAttribute("data-theme", theme);
    }
  } catch (error) {
    // Storage can be blocked, for example by browser privacy settings. The system theme applies instead.
  }
})();
