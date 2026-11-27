(function () {
  "use strict";

  var section = document.getElementById("feedback");
  var status = document.getElementById("feedback-status");
  var mine = document.getElementById("feedback-mine");
  var csrfMeta = document.querySelector('meta[name="csrf-token"]');

  if (!section || !status || !mine || !csrfMeta) {
    return;
  }

  var csrfToken = csrfMeta.getAttribute("content");
  var dishId = Number(section.getAttribute("data-dish-id"));
  var buttons = section.querySelectorAll("button[data-feedback]");

  function setTotal(name, value) {
    var element = section.querySelector('[data-total="' + name + '"]');
    if (element) {
      element.textContent = String(value);
    }
  }

  function render(data) {
    setTotal("cooked", data.cookedCount);
    setTotal("cooked-word", data.cookedCount === 1 ? "time" : "times");
    setTotal("up", data.upCount);
    setTotal("down", data.downCount);

    section.querySelector('[data-feedback="thumbsUp"]')
      .setAttribute("aria-pressed", data.vote === "up" ? "true" : "false");
    section.querySelector('[data-feedback="thumbsDown"]')
      .setAttribute("aria-pressed", data.vote === "down" ? "true" : "false");

    mine.textContent = data.cookedByYou > 0
      ? "You cooked this " + data.cookedByYou + (data.cookedByYou === 1 ? " time." : " times.")
      : "";
  }

  function announce(type, data) {
    if (type === "cooked") {
      return "Thanks! Marked as cooked.";
    }
    if (data.vote === "up") {
      return "You gave this a thumbs up.";
    }
    if (data.vote === "down") {
      return "You gave this a thumbs down.";
    }
    return "Your vote was removed.";
  }

  function setBusy(busy) {
    buttons.forEach(function (button) {
      button.disabled = busy;
    });
  }

  function showError() {
    status.classList.remove("visually-hidden");
    status.textContent = "Something went wrong. Please try again.";
  }

  buttons.forEach(function (button) {
    button.addEventListener("click", function () {
      var type = button.getAttribute("data-feedback");

      setBusy(true);
      status.classList.add("visually-hidden");
      status.textContent = "";

      fetch("/api/feedback", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "X-CSRF-TOKEN": csrfToken
        },
        body: JSON.stringify({ dishId: dishId, type: type })
      })
        .then(function (response) {
          if (response.status === 401) {
            location.href = "/Account/Login?returnUrl=" +
              encodeURIComponent(location.pathname);
            return null;
          }
          if (!response.ok) {
            showError();
            return null;
          }
          return response.json();
        })
        .then(function (data) {
          if (data) {
            render(data);
            status.textContent = announce(type, data);
          }
        })
        .catch(showError)
        .finally(function () {
          setBusy(false);
        });
    });
  });
}());
