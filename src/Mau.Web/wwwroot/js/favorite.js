(function () {
  "use strict";

  var button = document.getElementById("favorite-button");
  var status = document.getElementById("favorite-status");
  var csrfMeta = document.querySelector('meta[name="csrf-token"]');

  if (!button || !status || !csrfMeta) {
    return;
  }

  var csrfToken = csrfMeta.getAttribute("content");

  function render(isFavorite) {
    button.setAttribute("data-favorite", isFavorite ? "true" : "false");
    button.setAttribute("aria-pressed", isFavorite ? "true" : "false");
    button.textContent = isFavorite ? "★ In your favorites" : "☆ Add to favorites";
  }

  function showError() {
    status.classList.remove("visually-hidden");
    status.textContent = "Something went wrong. Please try again.";
  }

  button.addEventListener("click", function () {
    var dishId = button.getAttribute("data-dish-id");
    var isFavorite = button.getAttribute("data-favorite") === "true";
    var method = isFavorite ? "DELETE" : "PUT";

    button.disabled = true;
    status.classList.add("visually-hidden");
    status.textContent = "";

    fetch("/api/favorites/" + dishId, {
      method: method,
      headers: {
        "X-CSRF-TOKEN": csrfToken
      }
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

        render(!isFavorite);
        status.textContent = isFavorite
          ? "Removed from favorites."
          : "Added to favorites.";
        return null;
      })
      .catch(function () {
        showError();
      })
      .finally(function () {
        button.disabled = false;
      });
  });
}());
