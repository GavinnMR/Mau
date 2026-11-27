(function () {
  "use strict";

  var input = document.getElementById("ingredient-input");
  var hintList = document.getElementById("hint-list");
  var chipList = document.getElementById("chip-list");
  var statusMessage = document.getElementById("status-message");
  var resultList = document.getElementById("result-list");
  var pantryCheckbox = document.getElementById("use-pantry");
  var maxTimeSelect = document.getElementById("max-time");
  var preferencesCheckbox = document.getElementById("use-preferences");
  var csrfMeta = document.querySelector('meta[name="csrf-token"]');

  if (!input || !hintList || !chipList || !statusMessage || !resultList) {
    return;
  }

  var csrfToken = csrfMeta ? csrfMeta.getAttribute("content") : "";

  var committed = [];
  var excludedIds = new Set();
  var requestCounter = 0;
  var hintCounter = 0;
  var debounceTimer = null;
  var hints = [];
  var activeHintIndex = -1;

  function normalizeKey(value) {
    return value.trim().toLowerCase().replace(/\s+/g, " ");
  }

  function commitPiece(piece) {
    var trimmed = piece.trim();
    if (!trimmed) {
      return;
    }

    var key = normalizeKey(trimmed);
    if (committed.some(function (entry) { return normalizeKey(entry) === key; })) {
      return;
    }

    committed.push(trimmed);
  }

  function buildText() {
    return committed.join(", ") +
      (committed.length ? ", " : "") +
      input.value;
  }

  function scheduleSearch() {
    if (debounceTimer !== null) {
      clearTimeout(debounceTimer);
    }

    debounceTimer = setTimeout(function () {
      debounceTimer = null;
      runSearch();
      runHints();
    }, 150);
  }

  function usesPantry() {
    return pantryCheckbox !== null &&
      pantryCheckbox.checked &&
      !pantryCheckbox.disabled;
  }

  function setStatus(mainText, unknownTokens, showRemoveButton, pantryApplied, notes) {
    statusMessage.textContent = "";

    var main = document.createElement("p");
    main.className = "mb-1";
    main.textContent = mainText;
    statusMessage.appendChild(main);

    if (pantryApplied) {
      var pantryLine = document.createElement("p");
      pantryLine.className = "mb-1";
      pantryLine.textContent = "Your pantry is counted as ingredients you already have.";
      statusMessage.appendChild(pantryLine);
    }

    (notes || []).forEach(function (note) {
      var noteLine = document.createElement("p");
      noteLine.className = "mb-1";
      noteLine.textContent = note;
      statusMessage.appendChild(noteLine);
    });

    if (showRemoveButton) {
      var removeButton = document.createElement("button");
      removeButton.type = "button";
      removeButton.className = "btn btn-sm btn-outline-secondary mb-1";
      removeButton.textContent = "Remove last ingredient";
      removeButton.addEventListener("click", function () {
        if (committed.length === 0) {
          return;
        }

        committed.pop();
        runSearch();
      });
      statusMessage.appendChild(removeButton);
    }

    (unknownTokens || []).forEach(function (token) {
      var line = document.createElement("p");
      line.className = "mb-1";
      line.textContent = "\u201C" + token.raw + "\u201D isn\u2019t a known ingredient yet.";
      statusMessage.appendChild(line);
    });
  }

  function renderChips(tokens) {
    chipList.textContent = "";

    tokens.forEach(function (token) {
      var chip = document.createElement("span");
      var label;
      var isUnknown = token.ingredientId === null || token.ingredientId === undefined;

      if (isUnknown) {
        chip.className = "badge text-bg-warning chip";
        label = token.raw;
      } else if (token.isExcluded) {
        chip.className = "badge text-bg-secondary chip";
        label = "no " + token.name;
      } else {
        chip.className = "badge text-bg-success chip";
        label = token.raw.toLowerCase() === token.name.toLowerCase()
          ? token.raw
          : token.raw + " \u2192 " + token.name;
      }

      var text = document.createElement("span");
      text.className = "chip-text";
      text.textContent = label;
      if (!isUnknown && token.isExcluded) {
        text.classList.add("text-decoration-line-through");
      }
      chip.appendChild(text);

      if (!isUnknown) {
        var toggle = document.createElement("button");
        toggle.type = "button";
        toggle.className = "btn btn-sm btn-link chip-button";
        toggle.textContent = token.isExcluded ? "+" : "\u2212";
        toggle.setAttribute(
          "aria-label",
          token.isExcluded
            ? "Include " + token.name + " again"
            : "Leave out " + token.name);
        toggle.addEventListener("click", function () {
          if (token.isExcluded) {
            excludedIds.delete(token.ingredientId);
          } else {
            excludedIds.add(token.ingredientId);
          }
          runSearch();
        });
        chip.appendChild(toggle);
      }

      var remove = document.createElement("button");
      remove.type = "button";
      remove.className = "btn btn-sm btn-link chip-button";
      remove.textContent = "\u00D7";
      remove.setAttribute("aria-label", "Remove " + token.raw);
      remove.addEventListener("click", function () {
        committed.splice(token.index, 1);
        runSearch();
      });
      chip.appendChild(remove);

      chipList.appendChild(chip);
    });
  }

  function renderDishes(dishes, searchToken, maxMinutes) {
    resultList.textContent = "";
    var shownLimitHeading = false;

    dishes.forEach(function (dish) {
      if (dish.overTimeLimit === true && !shownLimitHeading) {
        shownLimitHeading = true;
        var heading = document.createElement("h2");
        heading.className = "h6 text-muted mt-4 mb-2 over-limit-heading";
        heading.textContent = "Takes longer than your " + maxMinutes + "-minute limit";
        resultList.appendChild(heading);
      }

      var card = document.createElement("div");
      card.className = dish.overTimeLimit === true ? "card mb-2 over-limit" : "card mb-2";

      var body = document.createElement("div");
      body.className = "card-body";

      var title = document.createElement("h2");
      title.className = "h5 card-title mb-2";
      var link = document.createElement("a");
      link.className = "dish-link";
      link.href = "/Dish/Details/" + dish.id +
        (searchToken ? "?search=" + encodeURIComponent(searchToken) : "");
      link.textContent = dish.name;
      title.appendChild(link);
      body.appendChild(title);

      var meta = document.createElement("p");
      meta.className = "mb-2";
      var badge = document.createElement("span");
      badge.className = "badge text-bg-secondary me-2";
      badge.textContent = dish.cookingMethod;
      meta.appendChild(badge);
      var time = document.createElement("span");
      time.className = "text-muted";
      time.textContent = dish.cookTimeMinutes + " min";
      meta.appendChild(time);
      (Array.isArray(dish.preferenceBadges) ? dish.preferenceBadges : []).forEach(function (text) {
        var preference = document.createElement("span");
        preference.className = "badge preference-badge ms-2";
        preference.textContent = "\u2713 " + text;
        meta.appendChild(preference);
      });
      body.appendChild(meta);

      var needed = document.createElement("p");
      needed.className = "mb-0";
      needed.textContent = dish.missingCount === 0
        ? "You have everything!"
        : "Still needed: " + (dish.stillNeeded || []).join(", ");
      body.appendChild(needed);

      card.appendChild(body);
      resultList.appendChild(card);
    });
  }

  function renderSearch(data) {
    var tokens = Array.isArray(data.tokens) ? data.tokens : [];
    var dishes = Array.isArray(data.dishes) ? data.dishes : [];
    var unknownTokens = tokens.filter(function (token) {
      return token.ingredientId === null || token.ingredientId === undefined;
    });
    var hasUsableIngredient = tokens.some(function (token) {
      return token.ingredientId !== null &&
        token.ingredientId !== undefined &&
        token.isExcluded === false;
    });

    renderChips(tokens);
    renderDishes(
      dishes,
      typeof data.searchToken === "string" ? data.searchToken : null,
      data.maxCookTimeMinutes);

    var pantryApplied = data.pantryApplied === true;
    var overLimit = typeof data.overTimeLimitCount === "number" ? data.overTimeLimitCount : 0;
    var notes = [];

    var anyPreferenceMatch = dishes.some(function (dish) {
      return Array.isArray(dish.preferenceBadges) && dish.preferenceBadges.length > 0;
    });
    if (anyPreferenceMatch && dishes.length > 1) {
      notes.push("Ranked using your preferences.");
    }
    var neverHidden = typeof data.neverListHiddenCount === "number" ? data.neverListHiddenCount : 0;
    var neverBlocking = Array.isArray(data.neverListBlocking) ? data.neverListBlocking : [];
    var neverOverridden = Array.isArray(data.neverListOverridden) ? data.neverListOverridden : [];

    if (neverHidden > 0) {
      notes.push((neverHidden === 1 ? "1 dish hidden: it contains " : neverHidden + " dishes hidden: they contain ") +
        neverBlocking.join(", ") + " (on your Never list).");
    }
    if (neverOverridden.length > 0) {
      notes.push("Showing " + neverOverridden.join(", ") +
        " because you typed it, even though it is on your Never list.");
    }

    if (overLimit > 0 && overLimit === dishes.length) {
      notes.push("None fit your " + data.maxCookTimeMinutes + "-minute limit, but you can still pick one.");
    } else if (overLimit > 0) {
      notes.push((overLimit === 1 ? "1 dish takes" : overLimit + " dishes take") +
        " longer than your " + data.maxCookTimeMinutes + "-minute limit; shown last.");
    }

    if (!hasUsableIngredient) {
      setStatus(
        "Add at least one known ingredient to see dishes.",
        unknownTokens, false, pantryApplied);
    } else if (dishes.length === 0 && neverHidden > 0) {
      setStatus(
        "Every matching dish contains something on your Never list.",
        unknownTokens, false, pantryApplied, notes);
    } else if (dishes.length === 0) {
      setStatus(
        "No dish contains all of these ingredients.",
        unknownTokens, true, pantryApplied, notes);
    } else {
      setStatus(
        dishes.length + (dishes.length === 1 ? " dish found." : " dishes found."),
        unknownTokens,
        false,
        pantryApplied,
        notes);
    }
  }

  function logHistory(event) {
    var target = event.target;
    if (!target || typeof target.closest !== "function") {
      return;
    }

    var link = target.closest("a.dish-link");
    if (!link || committed.length === 0) {
      return;
    }

    fetch("/api/search-log", {
      method: "POST",
      keepalive: true,
      headers: {
        "Content-Type": "application/json",
        "X-CSRF-TOKEN": csrfToken
      },
      body: JSON.stringify({
        text: buildText(),
        excludedIds: Array.from(excludedIds)
      })
    }).catch(function () {
      // Ignore logging errors; navigation must not be affected.
    });
  }

  function prefillFromQuery() {
    var query = new URLSearchParams(location.search).get("q");
    if (!query) {
      return;
    }

    var seen = new Set();
    query.split(",").forEach(function (piece) {
      var trimmed = piece.trim();
      if (!trimmed || trimmed.length > 100) {
        return;
      }

      var key = normalizeKey(trimmed);
      if (seen.has(key)) {
        return;
      }

      seen.add(key);
      if (committed.length < 20) {
        committed.push(trimmed);
      }
    });
  }

  function runSearch() {
    var counter = ++requestCounter;

    if (committed.length === 0) {
      chipList.textContent = "";
      resultList.textContent = "";
      setStatus("Type an ingredient and press comma or Enter to begin.", [], false);
      return;
    }

    fetch("/api/search", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "X-CSRF-TOKEN": csrfToken
      },
      body: JSON.stringify({
        text: buildText(),
        excludedIds: Array.from(excludedIds),
        usePantry: usesPantry(),
        usePreferences: preferencesCheckbox === null || preferencesCheckbox.checked,
        overrideMaxCookTime: maxTimeSelect !== null,
        maxCookTimeMinutes: maxTimeSelect && maxTimeSelect.value ? Number(maxTimeSelect.value) : null
      })
    })
      .then(function (response) {
        if (counter !== requestCounter) {
          return null;
        }
        if (!response.ok) {
          setStatus("Something went wrong. Please try again.", [], false);
          return null;
        }
        return response.json();
      })
      .then(function (data) {
        if (data === null || counter !== requestCounter) {
          return;
        }
        renderSearch(data);
      })
      .catch(function () {
        if (counter !== requestCounter) {
          return;
        }
        setStatus("Something went wrong. Please try again.", [], false);
      });
  }

  function hideHints() {
    hintList.hidden = true;
    hintList.textContent = "";
    hints = [];
    activeHintIndex = -1;
    input.setAttribute("aria-expanded", "false");
    input.removeAttribute("aria-activedescendant");
  }

  function updateActiveHint() {
    var options = hintList.children;

    for (var i = 0; i < options.length; i++) {
      var isActive = i === activeHintIndex;
      options[i].setAttribute("aria-selected", isActive ? "true" : "false");
      options[i].classList.toggle("active", isActive);
    }

    if (activeHintIndex >= 0) {
      input.setAttribute("aria-activedescendant", "hint-" + activeHintIndex);
    } else {
      input.removeAttribute("aria-activedescendant");
    }
  }

  function chooseHint(index) {
    var hint = hints[index];
    if (!hint) {
      return;
    }

    input.value = hint.text;
    hideHints();
    input.focus();
  }

  function renderHints(items) {
    hints = items.slice(0, 8);
    activeHintIndex = -1;
    hintList.textContent = "";

    if (hints.length === 0) {
      hideHints();
      return;
    }

    hints.forEach(function (hint, index) {
      var option = document.createElement("li");
      option.id = "hint-" + index;
      option.className = "hint-option";
      option.setAttribute("role", "option");
      option.setAttribute("aria-selected", "false");

      var text = document.createElement("span");
      text.textContent = hint.text;
      option.appendChild(text);

      if (hint.ingredient && hint.ingredient !== hint.text) {
        var ingredient = document.createElement("span");
        ingredient.className = "text-muted ms-2";
        ingredient.textContent = "(" + hint.ingredient + ")";
        option.appendChild(ingredient);
      }

      option.addEventListener("click", function () {
        chooseHint(index);
      });

      hintList.appendChild(option);
    });

    hintList.hidden = false;
    input.setAttribute("aria-expanded", "true");
    updateActiveHint();
  }

  function runHints() {
    var prefix = input.value.trim();
    var counter = ++hintCounter;

    if (!prefix) {
      hideHints();
      return;
    }

    fetch("/api/hints?prefix=" + encodeURIComponent(prefix))
      .then(function (response) {
        if (counter !== hintCounter) {
          return null;
        }
        if (!response.ok) {
          hideHints();
          return null;
        }
        return response.json();
      })
      .then(function (data) {
        if (data === null || counter !== hintCounter) {
          return;
        }
        renderHints(Array.isArray(data.hints) ? data.hints : []);
      })
      .catch(function () {
        if (counter !== hintCounter) {
          return;
        }
        hideHints();
      });
  }

  if (pantryCheckbox) {
    pantryCheckbox.addEventListener("change", scheduleSearch);
  }

  if (maxTimeSelect) {
    maxTimeSelect.addEventListener("change", scheduleSearch);
  }

  if (preferencesCheckbox) {
    preferencesCheckbox.addEventListener("change", scheduleSearch);
  }

  resultList.addEventListener("click", logHistory);
  resultList.addEventListener("auxclick", logHistory);

  input.addEventListener("input", function () {
    var value = input.value;

    if (/[,\n]/.test(value)) {
      var pieces = value.split(/[,\n]/);
      var last = pieces.pop();
      pieces.forEach(commitPiece);
      input.value = last;
    }

    scheduleSearch();
  });

  input.addEventListener("keydown", function (event) {
    if (event.key === "Enter") {
      if (activeHintIndex >= 0) {
        chooseHint(activeHintIndex);
        event.preventDefault();
        return;
      }

      var value = input.value.trim();
      if (value) {
        commitPiece(value);
        input.value = "";
        hideHints();
        event.preventDefault();
        runSearch();
      }
      return;
    }

    if (event.key === "Backspace" && input.value === "" && committed.length > 0) {
      input.value = committed.pop();
      event.preventDefault();
      runSearch();
      return;
    }

    if (event.key === "ArrowDown" || event.key === "ArrowUp") {
      if (hintList.hidden || hints.length === 0) {
        return;
      }

      var step = event.key === "ArrowDown" ? 1 : -1;
      if (activeHintIndex < 0) {
        activeHintIndex = step > 0 ? 0 : hints.length - 1;
      } else {
        activeHintIndex = (activeHintIndex + step + hints.length) % hints.length;
      }

      updateActiveHint();
      event.preventDefault();
      return;
    }

    if (event.key === "Escape") {
      hideHints();
    }
  });

  prefillFromQuery();
  runSearch();
}());
