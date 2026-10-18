// Theme picker, code highlighting, copy buttons, and progress saved in this browser.
// The site works without this script. These features stay hidden until it runs.
(function () {
  "use strict";

  var PROGRESS_KEY = "ghaction-workshop:progress";
  var THEME_KEY = "ghaction-workshop:theme";

  function storageAvailable() {
    try {
      var probe = "ghaction-workshop:probe";
      window.localStorage.setItem(probe, probe);
      window.localStorage.removeItem(probe);
      return true;
    } catch (error) {
      return false;
    }
  }

  var hasStorage = storageAvailable();

  function readItem(key) {
    if (!hasStorage) {
      return null;
    }

    try {
      return window.localStorage.getItem(key);
    } catch (error) {
      return null;
    }
  }

  function writeItem(key, value) {
    if (!hasStorage) {
      return false;
    }

    try {
      if (value === null) {
        window.localStorage.removeItem(key);
      } else {
        window.localStorage.setItem(key, value);
      }
      return true;
    } catch (error) {
      return false;
    }
  }

  function announce(message) {
    var region = document.querySelector("[data-announcer]");
    if (!region) {
      return;
    }

    // Clearing first makes screen readers announce the same message again if it repeats.
    region.textContent = "";
    window.setTimeout(function () {
      region.textContent = message;
    }, 50);
  }

  function readProgress() {
    var raw = readItem(PROGRESS_KEY);
    if (!raw) {
      return {};
    }

    try {
      var parsed = JSON.parse(raw);
      return parsed && typeof parsed === "object" && !Array.isArray(parsed) ? parsed : {};
    } catch (error) {
      return {};
    }
  }

  function isComplete(progress, slug) {
    return Object.prototype.hasOwnProperty.call(progress, slug) && progress[slug] === true;
  }

  function setUpThemePicker() {
    var picker = document.querySelector("[data-theme-picker]");
    var select = picker && picker.querySelector("select");
    if (!select || !hasStorage) {
      return;
    }

    var saved = readItem(THEME_KEY);
    select.value = saved === "light" || saved === "dark" ? saved : "system";

    select.addEventListener("change", function () {
      var theme = select.value;
      if (theme === "light" || theme === "dark") {
        document.documentElement.setAttribute("data-theme", theme);
        writeItem(THEME_KEY, theme);
      } else {
        document.documentElement.removeAttribute("data-theme");
        writeItem(THEME_KEY, null);
      }
    });

    picker.hidden = false;
  }

  function setUpCodeBlocks() {
    var blocks = document.querySelectorAll(".code-block");
    var canCopy = Boolean(navigator.clipboard && window.isSecureContext);

    Array.prototype.forEach.call(blocks, function (block) {
      var pre = block.querySelector("pre");
      var code = block.querySelector("pre > code");
      var header = block.querySelector(".code-block-header");
      if (!pre || !code || !header) {
        return;
      }

      // Long lines scroll sideways, so keyboard users need to be able to focus the block.
      pre.setAttribute("tabindex", "0");

      var match = /(?:^|\s)language-([\w-]+)/.exec(code.className);
      if (window.hljs && match && window.hljs.getLanguage(match[1])) {
        window.hljs.highlightElement(code);
      }

      if (!canCopy) {
        return;
      }

      var label = header.textContent.trim();
      var button = document.createElement("button");
      button.type = "button";
      button.className = "copy-button";
      button.textContent = "Copy";
      button.setAttribute("aria-label", "Copy " + label + " code");

      var resetTimer;
      button.addEventListener("click", function () {
        navigator.clipboard.writeText(code.textContent).then(
          function () {
            button.textContent = "Copied";
            announce("Copied to the clipboard.");
            window.clearTimeout(resetTimer);
            resetTimer = window.setTimeout(function () {
              button.textContent = "Copy";
            }, 2000);
          },
          function () {
            announce("The code couldn't be copied. Select the text and copy it manually.");
          }
        );
      });

      header.appendChild(button);
    });
  }

  function setUpLessonProgress() {
    var section = document.querySelector("[data-progress]");
    var article = document.querySelector("article[data-lesson]");
    var checkbox = section && section.querySelector("[data-progress-checkbox]");
    if (!checkbox || !article || !hasStorage) {
      return;
    }

    var slug = article.getAttribute("data-lesson");
    checkbox.checked = isComplete(readProgress(), slug);

    checkbox.addEventListener("change", function () {
      var progress = readProgress();
      if (checkbox.checked) {
        progress[slug] = true;
      } else {
        delete progress[slug];
      }

      if (writeItem(PROGRESS_KEY, JSON.stringify(progress))) {
        announce(checkbox.checked ? "Marked as complete in this browser." : "No longer marked as complete.");
      } else {
        checkbox.checked = !checkbox.checked;
        announce("Your progress couldn't be saved. Your browser might be blocking site storage.");
      }
    });

    section.hidden = false;
  }

  function showHomeProgress() {
    var tracks = document.querySelectorAll("[data-track]");
    if (!tracks.length || !hasStorage) {
      return;
    }

    var progress = readProgress();
    Array.prototype.forEach.call(tracks, function (track) {
      var cards = track.querySelectorAll("[data-lesson-card]");
      var completed = 0;

      Array.prototype.forEach.call(cards, function (card) {
        if (isComplete(progress, card.getAttribute("data-lesson-card"))) {
          completed += 1;
          var label = card.querySelector("[data-completed-label]");
          if (label) {
            label.hidden = false;
          }
        }
      });

      var summary = track.querySelector("[data-track-progress]");
      if (summary && completed > 0) {
        summary.textContent = completed + " of " + cards.length + " completed";
        summary.hidden = false;
      }
    });
  }

  setUpThemePicker();
  setUpCodeBlocks();
  setUpLessonProgress();
  showHomeProgress();
})();
