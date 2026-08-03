(() => {
  let ventagramFlashMessage = "";
  let mapLibreSdkPromise = null;
  let openPublicationPreview = null;
  let mapSelectionLayoutObserver = null;
  let mapSelectionLayoutResizeHandler = null;
  const favoriteLastListStorageKey = "ventagram:last-favorite-list-id";
  const likedPublicationsStorageKey = "ventagram:liked-publications";
  const NAVIGATION_LOCALITY_COOKIE = "ventagram_nav_locality_id";
  const chatConfig = window.__VENTAGRAM_CHAT_CONFIG || {};
  const supportedMapBounds = [[-73.6, -56.5], [-52.0, -19.0]];
  const supportedMapCenter = [-60.5, -31.5];

  document.addEventListener("DOMContentLoaded", async () => {
    wirePhoneMasks(document);
    wireHeaderLocality(document);
    wireRegisterLocalityDetection(document);
    wireReportModal();
    wireAuthRequiredModal();
    wireSuggestionModal();
    wirePublicationPreviewModal();
    wireDetailMediaOverlay();
    wireDetailGalleryLayout();
    wireFavoriteModal();
    wireFavoriteListModal();
    wireFavoriteActions(document);
    initFavoritesPage();
    await initRealtimeChat();
    await loadApiPage();
  });

  async function loadApiPage() {
    const host = document.getElementById("api-page");
    if (!host) return;

    const response = await fetch(host.dataset.apiEndpoint, {
      headers: { "X-Requested-With": "fetch" }
    });

    host.innerHTML = await response.text();
    wirePhoneMasks(host);

    if (ventagramFlashMessage) {
      const banner = document.createElement("div");
      banner.className = "status-banner";
      banner.textContent = ventagramFlashMessage;
      host.prepend(banner);
      ventagramFlashMessage = "";
    }

    try {
      await initContentMaps();
    } catch (error) {
      console.error(error);
    }
    await wireInfiniteGalleryFeeds(host);
    wireGalleryCards();
    wireGalleryActionMenus();
    wireDynamicGalleryCards();
    wireFavoriteActions(host);
    wireReportForm();
    wireCreateForm();
    wireBrowseSearchFilters(host);
    wireChatExperience(host);
    setupMapSelectionLayoutSync(host);
    scrollToSearchPanelIfRequested();
  }

  function setupMapSelectionLayoutSync(root = document) {
    mapSelectionLayoutObserver?.disconnect?.();
    mapSelectionLayoutObserver = null;

    if (mapSelectionLayoutResizeHandler) {
      window.removeEventListener("resize", mapSelectionLayoutResizeHandler);
      mapSelectionLayoutResizeHandler = null;
    }

    const layout = root.querySelector?.("[data-map-layout]");
    const mapCanvas = layout?.querySelector(".map-canvas");
    const panel = layout?.querySelector("[data-map-selection-panel]");
    const card = layout?.querySelector("[data-map-selection-card]");

    if (!layout || !mapCanvas || !panel) return;

    const sync = () => {
      if (window.matchMedia("(max-width: 780px)").matches) {
        layout.style.removeProperty("--map-desktop-shared-height");
        mapCanvas.style.height = "";
        mapCanvas.style.minHeight = "";
        mapCanvas.style.maxHeight = "";
        panel.style.height = "";
        panel.style.minHeight = "";
        panel.style.maxHeight = "";
        return;
      }

      const contentHeight = Math.max(
        420,
        Math.ceil(panel.scrollHeight),
        Math.ceil(card?.scrollHeight || 0)
      );
      const viewportMax = Math.max(
        420,
        Math.floor(window.innerHeight - 96)
      );
      const sharedHeight = Math.min(contentHeight, viewportMax, 720);

      layout.style.setProperty("--map-desktop-shared-height", `${sharedHeight}px`);
      mapCanvas.style.height = `${sharedHeight}px`;
      mapCanvas.style.minHeight = `${sharedHeight}px`;
      mapCanvas.style.maxHeight = `${sharedHeight}px`;
      panel.style.height = `${sharedHeight}px`;
      panel.style.minHeight = `${sharedHeight}px`;
      panel.style.maxHeight = `${sharedHeight}px`;

      const mapInstance =
        mapCanvas?._map ||
        mapCanvas?.map ||
        window.map ||
        window.contentMap;

      mapInstance?.resize?.();
    };

    mapSelectionLayoutObserver = new ResizeObserver(sync);
    mapSelectionLayoutObserver.observe(panel);

    if (card) {
      mapSelectionLayoutObserver.observe(card);
    }

    mapSelectionLayoutResizeHandler = () => sync();
    window.addEventListener("resize", mapSelectionLayoutResizeHandler);

    window.requestAnimationFrame(sync);
    window.setTimeout(sync, 100);
    window.setTimeout(sync, 400);
  }

  function scrollToSearchPanelIfRequested() {
    if (window.location.hash !== "#search-panel") return;

    const target = document.getElementById("search-panel");
    if (!target) return;

    window.requestAnimationFrame(() => {
      target.scrollIntoView({ behavior: "smooth", block: "start" });
    });
  }

  function wireReportModal() {
    const reportModal = document.getElementById("reportModal");
    if (!reportModal) return;

    const closeReport = () => {
      reportModal.hidden = true;
      reportModal.classList.remove("is-open");
      document.body.classList.remove("preview-open");
    };

    reportModal.addEventListener("click", event => {
      const closeTrigger = event.target.closest("[data-report-close='true']");
      if (!closeTrigger) return;
      event.preventDefault();
      event.stopPropagation();
      closeReport();
    });

    document.addEventListener("keydown", event => {
      if (event.key === "Escape" && reportModal.classList.contains("is-open")) {
        closeReport();
      }
    });

    document.addEventListener("click", event => {
      const trigger = event.target.closest(".report-trigger");
      if (!trigger) return;

      event.preventDefault();
      event.stopPropagation();

      const reportModalAllowed = document.body?.dataset.reportModalAllowed === "true";
      const reportBlockMessage = String(document.body?.dataset.reportBlockMessage || "").trim();
      const isAuthenticated = document.body?.dataset.userAuthenticated === "true";

      if (!isAuthenticated) {
        showAuthRequiredModal({
          title: "Debes iniciar sesión para denunciar",
          message: "Para denunciar una publicación debes ingresar con tu usuario.",
          showRegister: true,
          showLogin: true
        });
        return;
      }

      if (!reportModalAllowed) {
        showAuthRequiredModal({
          title: "No puedes denunciar por el momento",
          message: reportBlockMessage || "Tu cuenta no cumple los requisitos para denunciar publicaciones.",
          showRegister: false,
          showLogin: false
        });
        return;
      }

      const publicationId = trigger.getAttribute("data-publication-id");
      const publicationCode = trigger.getAttribute("data-publication-code");
      const title = stripOpportunitySuffix(trigger.getAttribute("data-publication-title"));
      const idInput = reportModal.querySelector('input[name="publicationId"]');
      const titleNode = reportModal.querySelector("#reportModalTitle");
      const defaultReason = reportModal.querySelector('input[name="reason"]:checked')
        || reportModal.querySelector('input[name="reason"]');

      if (idInput) idInput.value = publicationId || "0";
      if (titleNode) {
        const titlePrefix = publicationCode ? `${publicationCode} · ` : "";
        titleNode.textContent = title ? `${titlePrefix}Denunciar: ${title}` : "Selecciona un motivo";
      }
      if (defaultReason) defaultReason.checked = true;

      reportModal.hidden = false;
      reportModal.classList.add("is-open");
      document.body.classList.add("preview-open");
    });
  }

  function wireAuthRequiredModal() {
    const modal = document.getElementById("authRequiredModal");
    if (!modal) return;
    if (modal.dataset.bound === "true") return;
    modal.dataset.bound = "true";

    const close = () => {
      modal.hidden = true;
      modal.classList.remove("is-open");
      document.body.classList.remove("preview-open");
    };

    modal.addEventListener("click", event => {
      const closeTrigger = event.target.closest("[data-auth-required-close='true']");
      if (!closeTrigger) return;
      event.preventDefault();
      close();
    });

    document.addEventListener("keydown", event => {
      if (event.key === "Escape" && modal.classList.contains("is-open")) {
        close();
      }
    });

    document.querySelectorAll("[data-auth-required-favorites='true']").forEach(link => {
      if (link.dataset.authRequiredBound === "true") return;
      link.dataset.authRequiredBound = "true";
      link.addEventListener("click", event => {
        event.preventDefault();
        showAuthRequiredModal({
          title: "Debes iniciar sesión",
          message: "Puedes crear listas de anuncios favoritos para hacer seguimiento solo con una cuenta registrada.",
          showRegister: true,
          showLogin: true
        });
      });
    });
  }

  function showAuthRequiredModal(options = {}) {
    const modal = document.getElementById("authRequiredModal");
    if (!modal) return;
    const title = modal.querySelector("[data-auth-required-title]");
    const message = modal.querySelector("[data-auth-required-message]");
    const loginLink = modal.querySelector("[data-auth-required-login]");
    const registerLink = modal.querySelector("[data-auth-required-register]");
    const actions = modal.querySelector("[data-auth-required-actions]");
    const loginUrl = document.body?.dataset.reportLoginUrl || "/Account/Login";
    const showLogin = options.showLogin !== false;
    const showRegister = options.showRegister !== false;

    if (title) {
      title.textContent = options.title || "Acción no disponible";
    }

    if (message) {
      message.textContent = options.message || "Debes iniciar sesión para continuar.";
    }

    if (loginLink) {
      loginLink.hidden = !showLogin;
      loginLink.setAttribute("href", options.loginUrl || loginUrl);
    }

    if (registerLink) {
      registerLink.hidden = !showRegister;
    }

    if (actions) {
      actions.hidden = !showLogin && !showRegister;
    }

    modal.hidden = false;
    modal.classList.add("is-open");
    document.body.classList.add("preview-open");
  }

  function wireSuggestionModal() {
    const modal = document.getElementById("suggestionModal");
    const form = document.getElementById("suggestionForm");
    if (!modal || !form) return;
    if (modal.dataset.bound === "true") return;
    modal.dataset.bound = "true";

    const textarea = form.querySelector('textarea[name="message"]');
    const feedback = form.querySelector("[data-suggestion-feedback]");

    const close = () => {
      modal.hidden = true;
      modal.classList.remove("is-open");
      document.body.classList.remove("preview-open");
      if (feedback) {
        feedback.hidden = true;
        feedback.className = "status-banner";
        feedback.textContent = "";
      }
    };

    modal.addEventListener("click", event => {
      const closeTrigger = event.target.closest("[data-suggestion-close='true']");
      if (!closeTrigger) return;
      event.preventDefault();
      close();
    });

    document.addEventListener("keydown", event => {
      if (event.key === "Escape" && modal.classList.contains("is-open")) {
        close();
      }
    });

    document.querySelectorAll("[data-suggestion-open='true']").forEach(button => {
      if (button.dataset.suggestionBound === "true") return;
      button.dataset.suggestionBound = "true";
      button.addEventListener("click", event => {
        event.preventDefault();
        if (textarea) {
          textarea.value = "";
        }
        if (feedback) {
          feedback.hidden = true;
          feedback.className = "status-banner";
          feedback.textContent = "";
        }
        modal.hidden = false;
        modal.classList.add("is-open");
        document.body.classList.add("preview-open");
        textarea?.focus();
      });
    });

    form.addEventListener("submit", async event => {
      event.preventDefault();

      const message = String(textarea?.value || "").trim();
      if (!message) {
        if (feedback) {
          feedback.hidden = false;
          feedback.className = "status-banner warning";
          feedback.textContent = "Escribe una sugerencia antes de enviarla.";
        }
        return;
      }

      const submitButton = form.querySelector('button[type="submit"]');
      if (submitButton) {
        submitButton.disabled = true;
      }

      try {
        const response = await fetch(form.getAttribute("action") || "/api/content/suggestions", {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
            "X-Requested-With": "fetch"
          },
          body: JSON.stringify({ message })
        });

        const payload = await response.json().catch(() => ({}));
        if (!response.ok) {
          throw new Error(payload.message || "No se pudo enviar la sugerencia.");
        }

        if (feedback) {
          feedback.hidden = false;
          feedback.className = "status-banner success-banner";
          feedback.textContent = payload.message || "Sugerencia enviada.";
        }

        if (textarea) {
          textarea.value = "";
        }

        window.setTimeout(() => close(), 1200);
      } catch (error) {
        if (feedback) {
          feedback.hidden = false;
          feedback.className = "status-banner danger-banner";
          feedback.textContent = error?.message || "No se pudo enviar la sugerencia.";
        }
      } finally {
        if (submitButton) {
          submitButton.disabled = false;
        }
      }
    });
  }

  function wirePhoneMasks(root) {
    const inputs = root.querySelectorAll('input[data-phone-mask="ar"]');
    inputs.forEach(input => {
      if (input.dataset.phoneMaskBound === "true") return;
      input.dataset.phoneMaskBound = "true";

      const form = input.closest("form");
      const country = form?.querySelector("[data-phone-country]");
      const formatPhone = () => {
        if (country?.value === "AR") {
          const digits = extractArgPhoneDigits(input.value);
          input.value = formatArgPhoneDigits(digits);
        }
      };

      const syncPhoneMode = () => {
        if (country?.value === "AR") {
          input.placeholder = "+54 9 1145666454";
          input.inputMode = "numeric";
          input.autocomplete = "tel-national";
          if (!input.value.trim()) {
            input.value = "+54 9 ";
          } else {
            formatPhone();
          }
        } else {
          input.placeholder = "+1 212 555 0101";
          input.inputMode = "tel";
          input.autocomplete = "tel";
          if (input.value.startsWith("+54 9 ")) {
            input.value = input.value.replace(/^\+54 9\s*/, "");
          }
        }
      };

      country?.addEventListener("change", syncPhoneMode);
      input.addEventListener("focus", () => {
        if (country?.value === "AR" && !input.value.trim()) {
          input.value = "+54 9 ";
        }
      });

      input.addEventListener("input", formatPhone);
      input.addEventListener("blur", formatPhone);
      syncPhoneMode();
    });
  }

  function wireHeaderLocality(root = document) {
    const form = root.querySelector("[data-header-locality-form]");
    if (!form || form.dataset.bound === "true") return;

    const input = form.querySelector("[data-header-locality-input]");
    const status = form.querySelector("[data-header-locality-status]");
    const current = form.querySelector("[data-header-locality-current]");
    const detectButton = form.querySelector("[data-header-locality-detect]");
    const datalist = root.getElementById("header-locality-options");
    if (!input || !status || !current || !detectButton || !datalist) return;

    form.dataset.bound = "true";
    const options = Array.from(datalist.options).map(option => ({
      id: Number(option.dataset.localityId || 0),
      label: option.value || "",
      locality: option.dataset.locality || "",
      province: option.dataset.province || "",
      latitude: Number(option.dataset.latitude),
      longitude: Number(option.dataset.longitude)
    }));

    const setStatus = (message, isError = false) => {
      status.textContent = message;
      status.classList.toggle("text-danger", isError);
      status.classList.toggle("is-visible", Boolean(message));
    };

    const applyLocality = locality => {
      writeCookie(NAVIGATION_LOCALITY_COOKIE, String(locality.id), 365);
      current.textContent = `Anuncios cerca de ${locality.label}`;
      input.value = "";
      setStatus(`Buscando cerca de ${locality.label}. Recargando resultados...`);
      window.location.reload();
    };

    const applyMatchedLocality = () => {
      const selected = matchHeaderLocality(options, input.value);
      if (!selected) return false;
      applyLocality(selected);
      return true;
    };

    const detectNearestLocality = ({ silent = false } = {}) => {
      if (!navigator.geolocation) {
        if (!silent) {
          setStatus("Tu navegador no permite detectar ubicacion automaticamente.", true);
        }
        return;
      }

      const originalLabel = detectButton.textContent;
      detectButton.disabled = true;
      detectButton.textContent = "Detectando...";
      if (!silent) {
        setStatus("Esperando permiso para acceder a tu ubicacion.");
      }

      navigator.geolocation.getCurrentPosition(position => {
        const nearest = findNearestLocalityFromCollection(options, position.coords.latitude, position.coords.longitude);
        detectButton.disabled = false;
        detectButton.textContent = originalLabel;

        if (!nearest) {
          if (!silent) {
            setStatus("No encontramos una localidad cercana en la lista disponible.", true);
          }
          return;
        }

        applyLocality(nearest);
      }, error => {
        detectButton.disabled = false;
        detectButton.textContent = originalLabel;
        if (!silent) {
          setStatus(mapRegisterGeolocationError(error), true);
        }
      }, {
        enableHighAccuracy: true,
        timeout: 10000,
        maximumAge: 300000
      });
    };

    detectButton.addEventListener("click", () => {
      detectNearestLocality();
    });

    input.addEventListener("keydown", event => {
      if (event.key !== "Enter") return;
      event.preventDefault();
      if (!applyMatchedLocality()) {
        setStatus("Escribe una localidad valida de la lista para usarla en las busquedas.", true);
      }
    });

    input.addEventListener("input", () => {
      status.classList.remove("text-danger");
      status.classList.remove("is-visible");
      status.textContent = "";
    });

    input.addEventListener("change", () => {
      applyMatchedLocality();
    });

    input.addEventListener("blur", () => {
      applyMatchedLocality();
    });

    if (!readCookie(NAVIGATION_LOCALITY_COOKIE) && !input.value.trim() && navigator.permissions?.query) {
      navigator.permissions.query({ name: "geolocation" }).then(result => {
        if (result.state === "granted") {
          detectNearestLocality({ silent: true });
        }
      }).catch(() => {});
    }
  }

  function wireRegisterLocalityDetection(root = document) {
    const form = root.querySelector(".register-form");
    if (!form || form.dataset.localityDetectionBound === "true") return;

    const button = form.querySelector("[data-detect-locality]");
    const select = form.querySelector("[data-register-locality-select]");
    const status = form.querySelector("[data-register-locality-status]");
    if (!button || !select || !status) return;

    form.dataset.localityDetectionBound = "true";

    const setStatus = (message, isError = false) => {
      status.textContent = message;
      status.classList.toggle("text-danger", isError);
    };

    button.addEventListener("click", () => {
      if (!navigator.geolocation) {
        setStatus("Tu navegador no permite detectar ubicacion automaticamente.", true);
        return;
      }

      const originalLabel = button.textContent;
      button.disabled = true;
      button.textContent = "Detectando...";
      setStatus("Esperando permiso para acceder a tu ubicacion.");

      navigator.geolocation.getCurrentPosition(position => {
        const nearest = findNearestRegisterLocality(select, position.coords.latitude, position.coords.longitude);
        button.disabled = false;
        button.textContent = originalLabel;

        if (!nearest) {
          setStatus("No encontramos una localidad cercana en la lista disponible.", true);
          return;
        }

        select.value = nearest.value;
        select.dispatchEvent(new Event("change", { bubbles: true }));
        setStatus(`Ubicacion detectada. Seleccionamos ${nearest.locality}, ${nearest.province}.`);
      }, error => {
        button.disabled = false;
        button.textContent = originalLabel;
        setStatus(mapRegisterGeolocationError(error), true);
      }, {
        enableHighAccuracy: true,
        timeout: 10000,
        maximumAge: 300000
      });
    });
  }

  function findNearestRegisterLocality(select, latitude, longitude) {
    const localities = Array.from(select.options)
      .map(option => ({
        value: option.value,
        locality: option.textContent?.trim() || "",
        province: option.dataset.province || "",
        latitude: Number(option.dataset.latitude),
        longitude: Number(option.dataset.longitude)
      }))
      .filter(option => option.value && Number.isFinite(option.latitude) && Number.isFinite(option.longitude));

    if (!localities.length) return null;
    return findNearestLocalityFromCollection(localities, latitude, longitude);
  }

  function findNearestLocalityFromCollection(localities, latitude, longitude) {
    let nearest = null;
    for (const locality of localities) {
      const distance = haversineDistanceKm(latitude, longitude, locality.latitude, locality.longitude);
      if (!nearest || distance < nearest.distance) {
        nearest = { ...locality, distance };
      }
    }

    return nearest;
  }

  function matchHeaderLocality(localities, rawValue) {
    const normalized = normalizeLocalityText(rawValue);
    if (!normalized) return null;

    return localities.find(option =>
      normalizeLocalityText(option.label) === normalized
      || normalizeLocalityText(option.locality) === normalized) || null;
  }

  function haversineDistanceKm(lat1, lng1, lat2, lng2) {
    const toRadians = degrees => degrees * (Math.PI / 180);
    const earthRadiusKm = 6371;
    const deltaLat = toRadians(lat2 - lat1);
    const deltaLng = toRadians(lng2 - lng1);
    const a = Math.sin(deltaLat / 2) ** 2
      + Math.cos(toRadians(lat1)) * Math.cos(toRadians(lat2)) * Math.sin(deltaLng / 2) ** 2;
    const c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
    return earthRadiusKm * c;
  }

  function mapRegisterGeolocationError(error) {
    switch (error?.code) {
      case error.PERMISSION_DENIED:
        return "No diste permiso para detectar tu ubicacion.";
      case error.POSITION_UNAVAILABLE:
        return "No pudimos obtener tu ubicacion actual.";
      case error.TIMEOUT:
        return "La deteccion de ubicacion tardo demasiado. Intenta otra vez.";
      default:
        return "No se pudo detectar tu ubicacion.";
    }
  }

  function normalizeLocalityText(value) {
    return String(value || "")
      .normalize("NFD")
      .replace(/[\u0300-\u036f]/g, "")
      .trim()
      .toLowerCase();
  }

  function writeCookie(name, value, maxAgeDays) {
    const maxAgeSeconds = Math.max(1, Math.floor(maxAgeDays * 24 * 60 * 60));
    document.cookie = `${name}=${encodeURIComponent(value)}; path=/; max-age=${maxAgeSeconds}; SameSite=Lax`;
  }

  function readCookie(name) {
    const prefix = `${name}=`;
    return document.cookie
      .split(";")
      .map(item => item.trim())
      .find(item => item.startsWith(prefix))
      ?.slice(prefix.length) || "";
  }

  function eraseCookie(name) {
    document.cookie = `${name}=; path=/; max-age=0; SameSite=Lax`;
  }

  function extractArgPhoneDigits(value) {
    let digits = String(value || "").replace(/\D/g, "");
    if (digits.startsWith("549")) {
      digits = digits.slice(3);
    } else if (digits.startsWith("54")) {
      digits = digits.slice(2);
    }

    if (digits.startsWith("9")) {
      digits = digits.slice(1);
    }

    return digits.slice(0, 10);
  }

  function formatArgPhoneDigits(digits) {
    const value = String(digits || "").slice(0, 10);
    if (!value) {
      return "";
    }

    return `+54 9 ${value}`.trim();
  }

  function wireReportForm() {
    const form = document.getElementById("reportForm");
    if (!form || form.dataset.bound === "true") return;

    form.dataset.bound = "true";
    form.addEventListener("submit", async event => {
      event.preventDefault();

      const payload = {
        publicationId: Number(form.querySelector('input[name="publicationId"]').value),
        reasonId: Number(form.querySelector('input[name="reason"]:checked')?.value || 0),
        comment: String(form.querySelector('textarea[name="comment"]')?.value || "").trim()
      };

      const response = await fetch("/api/content/report", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "X-Requested-With": "fetch"
        },
        body: JSON.stringify(payload)
      });

      const result = await response.json().catch(() => ({}));
      if (!response.ok) {
        if (response.status === 401) {
          showAuthRequiredModal({
            title: "Debes iniciar sesión para denunciar",
            message: result?.message || "Para denunciar una publicación debes ingresar con tu usuario.",
            showRegister: true,
            showLogin: true
          });
          return;
        }

        ventagramFlashMessage = result?.message || "No se pudo enviar la denuncia.";
        await loadApiPage();
        return;
      }

      ventagramFlashMessage = result.message || "La denuncia fue enviada.";

      const modal = document.getElementById("reportModal");
      if (modal) {
        modal.hidden = true;
        modal.classList.remove("is-open");
      }
      document.body.classList.remove("preview-open");
      const commentField = form.querySelector('textarea[name="comment"]');
      if (commentField) commentField.value = "";
      await loadApiPage();
    });
  }

  function wireFavoriteActions(root = document) {
    root.querySelectorAll("[data-favorite-toggle='true']").forEach(button => {
      if (button.dataset.bound === "true") return;
      button.dataset.bound = "true";
      button.addEventListener("click", async event => {
        event.preventDefault();
        event.stopPropagation();
        await openFavoriteModal(button);
      });
    });

    root.querySelectorAll("[data-favorite-list-open='true']").forEach(button => {
      if (button.dataset.bound === "true") return;
      button.dataset.bound = "true";
      button.addEventListener("click", async event => {
        event.preventDefault();
        await openFavoriteListModal(button.getAttribute("data-list-id"), button.getAttribute("data-list-name"));
      });
    });

    root.querySelectorAll("[data-like-toggle='true']").forEach(button => {
      if (button.dataset.bound === "true") return;
      button.dataset.bound = "true";
      button.addEventListener("click", event => {
        event.preventDefault();
        event.stopPropagation();
        const publicationId = button.getAttribute("data-publication-id");
        togglePublicationLike(publicationId);
      });
    });
  }

  function initFavoritesPage() {
    const container = document.querySelector("[data-favorites-page-results]");
    if (!container || container.dataset.bound === "true") return;

    container.dataset.bound = "true";
    const buttons = Array.from(document.querySelectorAll("[data-favorite-list-open='true'][data-list-id]"));
    if (!buttons.length) return;

    const lastListId = readLastFavoriteList();
    const initialButton = buttons.find(button => String(button.getAttribute("data-list-id")) === String(lastListId))
      || buttons[0];
    initialButton?.click();
  }

  function wireGalleryActionMenus(root = document) {
    root.querySelectorAll("[data-gallery-menu-toggle='true']").forEach(button => {
      if (button.dataset.bound === "true") return;
      button.dataset.bound = "true";
      button.addEventListener("click", event => {
        event.preventDefault();
        event.stopPropagation();

        const menu = button.closest(".card-image-wrap")?.querySelector("[data-gallery-menu]");
        if (!menu) return;
        const willOpen = menu.hidden;

        closeAllGalleryActionMenus();
        menu.hidden = !willOpen;
        button.setAttribute("aria-expanded", willOpen ? "true" : "false");
      });
    });

    root.querySelectorAll("[data-gallery-menu] button").forEach(button => {
      if (button.dataset.menuBound === "true") return;
      button.dataset.menuBound = "true";
      button.addEventListener("click", () => {
        closeAllGalleryActionMenus();
      });
    });
  }

  function closeAllGalleryActionMenus() {
    document.querySelectorAll("[data-gallery-menu]").forEach(menu => {
      menu.hidden = true;
    });
    document.querySelectorAll("[data-gallery-menu-toggle='true']").forEach(button => {
      button.setAttribute("aria-expanded", "false");
    });
  }

  function wireFavoriteModal() {
    const modal = document.getElementById("favoriteModal");
    const form = document.getElementById("favoriteForm");
    if (!modal || !form) return;
    if (modal.dataset.bound === "true") return;
    modal.dataset.bound = "true";

    const close = () => {
      modal.hidden = true;
      modal.classList.remove("is-open");
      document.body.classList.remove("preview-open");
    };
    const syncNewListFieldVisibility = () => {
      const select = form.querySelector("[data-favorite-list-select]");
      const newListField = form.querySelector("[data-favorite-new-list-field]");
      const newListInput = form.querySelector('input[name="newListName"]');
      if (!select || !newListField || !newListInput) return;

      const creatingNew = !select.value;
      newListField.hidden = !creatingNew;
      newListInput.disabled = !creatingNew;
    };

    modal.addEventListener("click", event => {
      const closeTrigger = event.target.closest("[data-favorite-close='true']");
      if (!closeTrigger) return;
      event.preventDefault();
      close();
    });

    form.querySelector("[data-favorite-list-select]")?.addEventListener("change", syncNewListFieldVisibility);

    form.addEventListener("submit", async event => {
      event.preventDefault();
      const payload = {
        publicationId: Number(form.querySelector('input[name="publicationId"]')?.value || 0),
        listId: numberOrNull(form.querySelector('[name="listId"]')?.value || ""),
        newListName: String(form.querySelector('input[name="newListName"]')?.value || "").trim() || null,
        suggestedListName: String(form.querySelector('input[name="suggestedListName"]')?.value || "").trim() || null
      };

      const response = await fetch("/api/content/favorites", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "X-Requested-With": "fetch"
        },
        body: JSON.stringify(payload)
      });

      const result = await response.json().catch(() => ({}));
      if (!response.ok) {
        if (response.status === 401) {
          close();
          showAuthRequiredModal({
            title: "Debes iniciar sesión",
            message: "Puedes crear listas de anuncios favoritos para hacer seguimiento solo con una cuenta registrada.",
            showRegister: true,
            showLogin: true
          });
          return;
        }
        ventagramFlashMessage = result?.message || "No se pudo guardar en favoritos.";
        close();
        await loadApiPage();
        return;
      }

      if (result?.listId) {
        rememberLastFavoriteList(result.listId);
      }
      markPublicationFavorite(payload.publicationId);
      await refreshFavoriteSummaries();
      ventagramFlashMessage = result?.message || "Guardado en favoritos.";
      close();
      await loadApiPage();
    });

    syncNewListFieldVisibility();
  }

  async function openFavoriteModal(trigger) {
    const modal = document.getElementById("favoriteModal");
    const form = document.getElementById("favoriteForm");
    if (!modal || !form || !trigger) return;

    const publicationId = trigger.getAttribute("data-publication-id") || "0";
    const publicationTitle = stripOpportunitySuffix(trigger.getAttribute("data-publication-title") || "Publicacion");
    const suggestedListName = trigger.getAttribute("data-suggested-list-name") || "Inmuebles";
    const select = form.querySelector("[data-favorite-list-select]");
    const newListInput = form.querySelector('input[name="newListName"]');
    form.querySelector('input[name="publicationId"]').value = publicationId;
    form.querySelector('input[name="suggestedListName"]').value = suggestedListName;
    if (newListInput) {
      newListInput.value = suggestedListName;
      newListInput.placeholder = suggestedListName;
    }

    const response = await fetch("/api/content/favorite-lists", {
      headers: { "X-Requested-With": "fetch" }
    });
    const result = await response.json().catch(() => ({}));
    if (!response.ok) {
      if (response.status === 401) {
        showAuthRequiredModal({
          title: "Debes iniciar sesión",
          message: "Puedes crear listas de anuncios favoritos para hacer seguimiento solo con una cuenta registrada.",
          showRegister: true,
          showLogin: true
        });
        return;
      }
      ventagramFlashMessage = result?.message || "Tenes que iniciar sesion para usar favoritos.";
      await loadApiPage();
      return;
    }

    const lists = Array.isArray(result?.lists) ? result.lists : [];
    if (select) {
      select.innerHTML = `<option value="">Crear una nueva</option>${lists.map(list => `<option value="${escapeAttribute(list.id)}">${escapeHtml(list.name)} (${escapeHtml(String(list.itemCount || 0))})</option>`).join("")}`;
      const defaultListId = resolveDefaultFavoriteListId(lists, suggestedListName);
      select.value = defaultListId ? String(defaultListId) : "";
    }
    const newListField = form.querySelector("[data-favorite-new-list-field]");
    if (newListField && newListInput) {
      const creatingNew = !select?.value;
      newListField.hidden = !creatingNew;
      newListInput.disabled = !creatingNew;
    }

    const titleNode = document.getElementById("favoriteModalTitle");
    if (titleNode) {
      titleNode.textContent = publicationTitle ? `Guardar: ${publicationTitle}` : "Guardar en favoritos";
    }

    modal.hidden = false;
    modal.classList.add("is-open");
    document.body.classList.add("preview-open");
  }

  function wireFavoriteListModal() {
    const modal = document.getElementById("favoriteListModal");
    if (!modal) return;
    if (modal.dataset.bound === "true") return;
    modal.dataset.bound = "true";

    const close = () => {
      modal.hidden = true;
      modal.classList.remove("is-open");
      document.body.classList.remove("preview-open");
    };

    modal.addEventListener("click", event => {
      const closeTrigger = event.target.closest("[data-favorite-list-close='true']");
      if (!closeTrigger) return;
      event.preventDefault();
      close();
    });
  }

  async function openFavoriteListModal(listId, fallbackName = "Mi lista") {
    if (!listId) return;
    rememberLastFavoriteList(listId);

    const inlineContainer = document.querySelector("[data-favorites-page-results]");
    if (inlineContainer) {
      await renderFavoriteListInline(inlineContainer, listId, fallbackName);
      return;
    }

    const modal = document.getElementById("favoriteListModal");
    const body = document.getElementById("favoriteListModalBody");
    const title = document.getElementById("favoriteListModalTitle");
    if (!modal || !body || !title) return;

    title.textContent = `Favoritos: ${fallbackName}`;
    body.innerHTML = `<div class="preview-modal-loading">Cargando favoritos...</div>`;
    modal.hidden = false;
    modal.classList.add("is-open");
    document.body.classList.add("preview-open");

    const response = await fetch(`/api/content/favorite-lists/${encodeURIComponent(listId)}`, {
      headers: { "X-Requested-With": "fetch" }
    });
    const result = await response.json().catch(() => ({}));
    if (!response.ok) {
      title.textContent = fallbackName;
      body.innerHTML = `<section class="empty-state"><h2>No se pudo abrir la lista</h2><p>${escapeHtml(result?.message || "Intenta nuevamente.")}</p></section>`;
      return;
    }

    const items = Array.isArray(result?.items) ? result.items : [];
    title.textContent = `Favoritos: ${result?.list?.name || fallbackName}`;
    body.innerHTML = items.length
      ? `<section class="favorites-modal-grid">${items.map(item => buildGalleryCard(item, false, { showReportButton: false })).join("")}</section>`
      : `<section class="empty-state"><h2>La lista esta vacia</h2><p>Guarda publicaciones con la estrella para verlas aca.</p></section>`;
    wireGalleryCards();
    wireFavoriteActions(body);
  }

  async function renderFavoriteListInline(container, listId, fallbackName) {
    const title = container.querySelector("[data-favorites-page-title]");
    const loading = container.querySelector("[data-favorites-page-loading]");
    const empty = container.querySelector("[data-favorites-page-empty]");
    const gallery = container.querySelector("[data-favorites-page-gallery]");
    if (!title || !loading || !empty || !gallery) return;

    syncFavoriteListSelection(listId);
    container.hidden = false;
    title.textContent = `Favoritos: ${fallbackName}`;
    loading.hidden = false;
    empty.hidden = true;
    gallery.innerHTML = "";

    const response = await fetch(`/api/content/favorite-lists/${encodeURIComponent(listId)}`, {
      headers: { "X-Requested-With": "fetch" }
    });
    const result = await response.json().catch(() => ({}));
    loading.hidden = true;

    if (!response.ok) {
      title.textContent = fallbackName;
      empty.innerHTML = `<h2>No se pudo abrir la lista</h2><p>${escapeHtml(result?.message || "Intenta nuevamente.")}</p>`;
      empty.hidden = false;
      return;
    }

    const items = Array.isArray(result?.items) ? result.items : [];
    title.textContent = `Favoritos: ${result?.list?.name || fallbackName}`;

    if (!items.length) {
      empty.innerHTML = `<h2>La lista esta vacia</h2><p>Guarda publicaciones con la estrella para verlas aca.</p>`;
      empty.hidden = false;
      return;
    }

    gallery.innerHTML = items.map(item => buildGalleryCard(item, false, { showReportButton: false })).join("");
    wireGalleryCards();
    wireFavoriteActions(gallery);
    container.scrollIntoView({ behavior: "smooth", block: "start" });
  }

  function syncFavoriteListSelection(activeListId) {
    document.querySelectorAll("[data-favorite-list-open='true'][data-list-id]").forEach(button => {
      const isActive = String(button.getAttribute("data-list-id")) === String(activeListId);
      button.classList.toggle("active", isActive);
      button.setAttribute("aria-pressed", isActive ? "true" : "false");
    });
  }

  async function refreshFavoriteSummaries() {
    const summary = document.querySelector("[data-favorites-summary]");
    const actions = document.querySelector("[data-favorites-summary-actions]");
    if (!summary || !actions) return;

    const response = await fetch("/api/content/favorite-lists", {
      headers: { "X-Requested-With": "fetch" }
    });
    const result = await response.json().catch(() => ({}));
    if (!response.ok) return;

    const lists = Array.isArray(result?.lists) ? result.lists : [];
    actions.innerHTML = lists.length
      ? lists.map(list => `<button type="button" class="view-pill" data-favorite-list-open="true" data-list-id="${escapeAttribute(list.id)}" data-list-name="${escapeAttribute(list.name)}">${escapeHtml(list.name)} (${escapeHtml(String(list.itemCount || 0))})</button>`).join("")
      : `<span class="field-hint">Todavia no guardaste publicaciones en tus listas.</span>`;
    wireFavoriteActions(summary);
  }

  function markPublicationFavorite(publicationId) {
    document.querySelectorAll(`[data-favorite-toggle='true'][data-publication-id='${publicationId}']`).forEach(button => {
      button.classList.add("is-active");
      button.innerHTML = renderFavoriteIcon(true);
    });
  }

  function togglePublicationLike(publicationId) {
    if (!publicationId) return;
    const liked = readLikedPublicationIds();
    const key = String(publicationId);
    if (liked.has(key)) {
      liked.delete(key);
    } else {
      liked.add(key);
    }

    writeLikedPublicationIds(liked);
    syncLikeButtonsForPublication(key);
  }

  function syncLikeButtonsForPublication(publicationId) {
    const liked = readLikedPublicationIds().has(String(publicationId));
    document.querySelectorAll(`[data-like-toggle='true'][data-publication-id='${publicationId}']`).forEach(button => {
      button.classList.toggle("is-active", liked);
      button.innerHTML = renderLikeIcon(liked);
      button.setAttribute("aria-label", liked ? "Quitar me gusta" : "Marcar como me gusta");
      button.setAttribute("title", liked ? "Quitar me gusta" : "Marcar como me gusta");
    });
  }

  function renderFavoriteIcon(isActive) {
    return `<i class="fa-${isActive ? "solid" : "regular"} fa-star" aria-hidden="true"></i>`;
  }

  function renderLikeIcon(isActive) {
    return `<i class="fa-${isActive ? "solid" : "regular"} fa-heart" aria-hidden="true"></i>`;
  }

  function renderPreviewIcon() {
    return `<i class="fa-regular fa-eye" aria-hidden="true"></i>`;
  }

  function readLikedPublicationIds() {
    try {
      const raw = localStorage.getItem(likedPublicationsStorageKey);
      const parsed = raw ? JSON.parse(raw) : [];
      return new Set(Array.isArray(parsed) ? parsed.map(String) : []);
    } catch {
      return new Set();
    }
  }

  function writeLikedPublicationIds(ids) {
    try {
      localStorage.setItem(likedPublicationsStorageKey, JSON.stringify([...ids]));
    } catch {
      // Ignore storage failures and keep in-memory behavior only.
    }
  }

  function resolveDefaultFavoriteListId(lists, suggestedListName) {
    const normalizedSuggestedName = normalizeFavoriteListName(suggestedListName);
    const exactGroupMatch = lists.find(list => normalizeFavoriteListName(list?.name) === normalizedSuggestedName);
    if (exactGroupMatch?.id) {
      return exactGroupMatch.id;
    }

    const lastListId = readLastFavoriteList();
    if (!lastListId) {
      return null;
    }

    const lastSelected = lists.find(list => String(list?.id) === String(lastListId));
    return lastSelected?.id || null;
  }

  function normalizeFavoriteListName(value) {
    return String(value || "").trim().toLowerCase();
  }

  function rememberLastFavoriteList(listId) {
    try {
      localStorage.setItem(favoriteLastListStorageKey, String(listId));
    } catch {
      // Ignore storage failures and keep default behavior.
    }
  }

  function readLastFavoriteList() {
    try {
      return localStorage.getItem(favoriteLastListStorageKey);
    } catch {
      return null;
    }
  }

  async function initContentMaps() {
    const homeMap = document.getElementById("map");
    const publicationMap = document.querySelector("[data-publication-map]");
    const createMap = document.querySelector("[data-create-map]");
    if (!homeMap && !publicationMap && !createMap) return;

    const sdk = await loadMapLibreSdk();
    if (homeMap) {
      await initHomeMap(homeMap, sdk);
    }

    if (publicationMap) {
      await initHomeMap(publicationMap, sdk);
      wireDetailMapFullscreen(publicationMap, sdk);
    }

    if (createMap) {
      await initCreateMap(createMap, sdk);
    }
  }

  function wireDetailGalleryLayout() {
    const galleries = Array.from(document.querySelectorAll(".detail-gallery"));
    if (!galleries.length) return;

    const applyLayout = gallery => {
      const styles = window.getComputedStyle(gallery);
      const gap = Number.parseFloat(styles.columnGap || styles.gap || "16") || 16;
      const minCardWidth = 180;
      const availableWidth = gallery.clientWidth || gallery.parentElement?.clientWidth || window.innerWidth;
      const columnCount = Math.max(1, Math.floor((availableWidth + gap) / (minCardWidth + gap)));
      gallery.style.gridTemplateColumns = `repeat(${columnCount}, minmax(0, 1fr))`;
    };

    galleries.forEach(gallery => {
      if (gallery.dataset.layoutBound === "true") return;
      gallery.dataset.layoutBound = "true";
      applyLayout(gallery);

      const observer = new ResizeObserver(() => applyLayout(gallery));
      observer.observe(gallery);
    });
  }

  function loadMapLibreSdk() {
    if (mapLibreSdkPromise) {
      return mapLibreSdkPromise;
    }

    mapLibreSdkPromise = new Promise((resolve, reject) => {
      if (!document.querySelector('link[data-maplibre-css="true"]')) {
        const link = document.createElement("link");
        link.dataset.maplibreCss = "true";
        link.rel = "stylesheet";
        link.href = "https://unpkg.com/maplibre-gl@4.7.1/dist/maplibre-gl.css";
        document.head.appendChild(link);
      }

      if (window.maplibregl) {
        resolve(window.maplibregl);
        return;
      }

      const existingScript = document.querySelector('script[data-maplibre-sdk="true"]');
      if (existingScript) {
        existingScript.addEventListener("load", () => resolve(window.maplibregl), { once: true });
        existingScript.addEventListener("error", () => reject(new Error("No se pudo cargar MapLibre.")), { once: true });
        return;
      }

      const script = document.createElement("script");
      script.dataset.maplibreSdk = "true";
      script.src = "https://unpkg.com/maplibre-gl@4.7.1/dist/maplibre-gl.js";
      script.onload = () => resolve(window.maplibregl);
      script.onerror = () => reject(new Error("No se pudo cargar MapLibre."));
      document.head.appendChild(script);
    });

    return mapLibreSdkPromise;
  }

  async function initHomeMap(mapElement, sdk) {
    if (mapElement.dataset.mapInitialized === "true") return;
    const styleUrl = String(mapElement.dataset.mapStyleUrl || "").trim();
    const tilesUrlTemplate = String(mapElement.dataset.mapTilesUrl || "").trim();
    if (!styleUrl && !tilesUrlTemplate) return;
    const attribution = String(mapElement.dataset.mapAttribution || "").trim();
    const mapMode = mapElement.dataset.mapMode || "home";
    const initialLat = Number.parseFloat(mapElement.dataset.mapInitialLat || "");
    const initialLng = Number.parseFloat(mapElement.dataset.mapInitialLng || "");
    const hasInitialCenter = Number.isFinite(initialLat) && Number.isFinite(initialLng);

    const markers = JSON.parse(mapElement.dataset.markers || "[]");
    if (!markers.length) return;

    mapElement.innerHTML = "";

    const instance = new sdk.Map({
      container: mapElement,
      style: buildMapStyle(styleUrl, tilesUrlTemplate, attribution),
      maxBounds: supportedMapBounds,
      center: hasInitialCenter ? [initialLng, initialLat] : [markers[0].lng, markers[0].lat],
      zoom: mapMode === "detail" ? 17.5 : (hasInitialCenter ? 11 : 5)
    });
    const selectionPanel = mapElement.parentElement?.querySelector("[data-map-selection-card]");
    const selectionViewToggle = mapElement.parentElement?.querySelector("[data-map-selection-view-toggle]");
    const hoverPopup = new sdk.Popup({
      closeButton: false,
      closeOnClick: false,
      offset: 18,
      className: "map-hover-popup"
    });
    const mobileTapPopup = new sdk.Popup({
      closeButton: false,
      closeOnClick: true,
      offset: 20,
      className: "map-tap-popup"
    });
    let selectedMarker = null;
    let selectedMarkerView = selectionViewToggle?.dataset.currentView === "text" ? "text" : "gallery";

    const syncSelectionViewButtons = () => {
      selectionViewToggle?.querySelectorAll("[data-map-selection-view]").forEach(button => {
        const isActive = button.dataset.mapSelectionView === selectedMarkerView;
        button.classList.toggle("is-active", isActive);
        button.setAttribute("aria-pressed", isActive ? "true" : "false");
      });
    };

    const renderSelectedMarker = () => {
      if (!selectionPanel || !selectedMarker) return;

      selectionPanel.innerHTML = selectedMarkerView === "text"
        ? buildMapSelectionTextCard(selectedMarker)
        : buildGalleryCard({
            id: selectedMarker.id,
            title: selectedMarker.title,
            galleryTitle: String(selectedMarker.title || "").split(" - oportunidad")[0],
            publicationCode: selectedMarker.code,
            price: selectedMarker.price,
            detailsUrl: selectedMarker.detailsUrl,
            videoUrl: selectedMarker.videoUrl,
            images: Array.isArray(selectedMarker.images) && selectedMarker.images.length
              ? selectedMarker.images
              : [selectedMarker.image || "/images/logo4.png"],
            isFavorite: Boolean(selectedMarker.isFavorite),
            groupName: selectedMarker.groupName || "Inmuebles"
          }, false);

      wireGalleryCards();
      wireFavoriteActions(selectionPanel);
      wireReportForm();
      syncMobileGalleryVideoAutoplay(selectionPanel);
    };

    const setSelectedMarker = marker => {
      if (!selectionPanel || !marker) return;
      selectedMarker = marker;
      renderSelectedMarker();
    };

    if (selectionViewToggle && selectionViewToggle.dataset.bound !== "true") {
      selectionViewToggle.dataset.bound = "true";
      selectionViewToggle.querySelectorAll("[data-map-selection-view]").forEach(button => {
        button.addEventListener("click", event => {
          event.preventDefault();
          const nextView = button.dataset.mapSelectionView === "text" ? "text" : "gallery";
          if (nextView === selectedMarkerView) return;

          selectedMarkerView = nextView;
          selectionViewToggle.dataset.currentView = selectedMarkerView;
          syncSelectionViewButtons();
          renderSelectedMarker();
        });
      });
    }

    syncSelectionViewButtons();
    const handleMarkerSelection = marker => {
      if (isMobileMapInteractionContext()) {
        hoverPopup.remove();
        mobileTapPopup
          .setLngLat([marker.lng, marker.lat])
          .setHTML(buildMapMarkerTapCard(marker))
          .addTo(instance);
        return;
      }

      setSelectedMarker(marker);
    };

    const bounds = new sdk.LngLatBounds();
    markers.forEach(marker => {
      const markerInstance = new sdk.Marker({ color: "#ff5a5f" })
        .setLngLat([marker.lng, marker.lat])
        .addTo(instance);
      const markerElement = markerInstance.getElement();
      let lastTouchSelectionAt = 0;
      markerElement.addEventListener("click", event => {
        if (Date.now() - lastTouchSelectionAt < 500) {
          event.preventDefault();
          event.stopPropagation();
          return;
        }

        event.preventDefault();
        event.stopPropagation();
        handleMarkerSelection(marker);
      });
      markerElement.addEventListener("touchend", event => {
        lastTouchSelectionAt = Date.now();
        event.preventDefault();
        event.stopPropagation();
        handleMarkerSelection(marker);
      }, { passive: false });
      markerElement.addEventListener("pointerup", event => {
        if (event.pointerType !== "touch") return;
        lastTouchSelectionAt = Date.now();
        event.preventDefault();
        event.stopPropagation();
        handleMarkerSelection(marker);
      });
      markerElement.addEventListener("mouseenter", () => {
        hoverPopup
          .setLngLat([marker.lng, marker.lat])
          .setHTML(buildMapMarkerHoverCard(marker))
          .addTo(instance);
      });
      markerElement.addEventListener("mouseleave", () => {
        hoverPopup.remove();
      });

      bounds.extend([marker.lng, marker.lat]);
    });

    if (!isMobileMapInteractionContext()) {
      setSelectedMarker(markers[0]);
    }

    if (mapMode === "home" && hasInitialCenter) {
      instance.flyTo({ center: [initialLng, initialLat], zoom: 11 });
    } else if (markers.length > 1) {
      instance.fitBounds(bounds, { padding: 60 });
    } else if (mapMode === "detail") {
      instance.flyTo({ center: [markers[0].lng, markers[0].lat], zoom: 17.5 });
    }

    mapElement._mapInstance = instance;
    mapElement.dataset.mapInitialized = "true";
    return instance;
  }

  function wireDetailMapFullscreen(mapElement, sdk) {
    if (!mapElement || mapElement.dataset.fullscreenBound === "true") return;
    mapElement.dataset.fullscreenBound = "true";

    const panel = mapElement.closest(".detail-panel");
    if (!panel) return;
    if (!mapElement._mapInstance) return;

    const actions = document.createElement("div");
    actions.className = "detail-panel-actions";
    actions.innerHTML = `
      <button type="button" class="ghost-pill compact map-fullscreen-trigger" data-map-fullscreen-trigger>
        Ver mapa a pantalla completa
      </button>
    `;

    mapElement.insertAdjacentElement("afterend", actions);

    const trigger = actions.querySelector("[data-map-fullscreen-trigger]");
    const syncTriggerLabel = () => {
      const isFullscreen = document.fullscreenElement === mapElement || mapElement.classList.contains("is-map-fullscreen");
      if (trigger) {
        trigger.textContent = isFullscreen ? "Salir de pantalla completa" : "Ver mapa a pantalla completa";
      }
      document.body.classList.toggle("map-fullscreen-open", mapElement.classList.contains("is-map-fullscreen"));
    };

    const toggleFullscreen = async () => {
      const mapInstance = mapElement._mapInstance;
      if (!mapInstance) return;

      if (document.fullscreenElement === mapElement) {
        await document.exitFullscreen?.();
        syncTriggerLabel();
        return;
      }

      if (mapElement.requestFullscreen) {
        await mapElement.requestFullscreen();
        mapInstance.resize?.();
        syncTriggerLabel();
        return;
      }

      mapElement.classList.toggle("is-map-fullscreen");
      syncTriggerLabel();
      mapInstance.resize?.();
    };

    trigger?.addEventListener("click", event => {
      event.preventDefault();
      toggleFullscreen().catch(() => {
        mapElement.classList.toggle("is-map-fullscreen");
        syncTriggerLabel();
        mapElement._mapInstance?.resize?.();
      });
    });

    document.addEventListener("fullscreenchange", () => {
      syncTriggerLabel();
      mapElement._mapInstance?.resize?.();
    });

    syncTriggerLabel();
  }

  async function initCreateMap(mapElement, sdk) {
    if (mapElement.dataset.mapInitialized === "true") return;
    const styleUrl = String(mapElement.dataset.mapStyleUrl || "").trim();
    const tilesUrlTemplate = String(mapElement.dataset.mapTilesUrl || "").trim();
    if (!styleUrl && !tilesUrlTemplate) return;
    const attribution = String(mapElement.dataset.mapAttribution || "").trim();
    const geocodingSearchUrlTemplate = String(mapElement.dataset.mapGeocodingSearchUrl || "").trim();
    const reverseGeocodingUrlTemplate = String(mapElement.dataset.mapReverseGeocodingUrl || "").trim();

    const form = mapElement.closest("form");
    if (!form) return;

    const latitudeInput = form.querySelector('input[name="latitude"]');
    const longitudeInput = form.querySelector('input[name="longitude"]');
    const localityInput = form.querySelector('input[name="locality"]');
    const addressInput = form.querySelector('input[name="address"]');
    const searchInput = form.querySelector('input[name="locationSearch"]');
    const noLocationInput = form.querySelector("[data-create-no-location]");
    const searchButton = form.querySelector("[data-create-address-search]");
    const summary = form.querySelector("[data-create-location-summary]");
    let noLocationMode = Boolean(noLocationInput?.checked);
    mapElement.innerHTML = "";

    const defaultCenter = getCreateMapCenter(latitudeInput?.value, longitudeInput?.value);
    const instance = new sdk.Map({
      container: mapElement,
      style: buildMapStyle(styleUrl, tilesUrlTemplate, attribution),
      maxBounds: supportedMapBounds,
      center: defaultCenter.center,
      zoom: defaultCenter.zoom
    });

    const marker = new sdk.Marker({ color: "#ff4b5f", draggable: true })
      .setLngLat(defaultCenter.center)
      .addTo(instance);

    const syncLocation = ({ lat, lng, locality, address, searchValue, flyTo = true }) => {
      if (noLocationMode) {
        return;
      }

      if (latitudeInput) latitudeInput.value = String(lat);
      if (longitudeInput) longitudeInput.value = String(lng);
      if (localityInput) localityInput.value = locality || "";
      if (addressInput) addressInput.value = address || locality || "";
      if (searchInput && searchValue) searchInput.value = searchValue;
      if (summary) {
        summary.textContent = formatCreateLocationSummary({ locality, address, lat, lng });
      }
      syncCreateTitle(form);
      if (flyTo) {
        instance.flyTo({ center: [lng, lat], zoom: 15 });
      }
      marker.setLngLat([lng, lat]);
    };

    const setNoLocationMode = enabled => {
      noLocationMode = enabled;
      mapElement.classList.toggle("is-disabled", enabled);

      if (searchInput) {
        searchInput.disabled = enabled || !geocodingSearchUrlTemplate;
      }

      if (searchButton) {
        searchButton.disabled = enabled || !geocodingSearchUrlTemplate;
      }

      if (enabled) {
        if (latitudeInput) latitudeInput.value = "";
        if (longitudeInput) longitudeInput.value = "";
        if (localityInput) localityInput.value = "";
        if (addressInput) addressInput.value = "";
        if (searchInput) searchInput.value = "";
        if (summary) {
          summary.textContent = "Sin ubicaciÃ³n disponible";
        }
        syncCreateTitle(form);
        return;
      }

      const currentLat = numberOrNull(latitudeInput?.value || "");
      const currentLng = numberOrNull(longitudeInput?.value || "");
      if (currentLat !== null && currentLng !== null) {
        syncLocation({
          lat: currentLat,
          lng: currentLng,
          locality: localityInput?.value || "",
          address: addressInput?.value || "",
          searchValue: addressInput?.value || localityInput?.value || "",
          flyTo: false
        });
        return;
      }

      if (summary) {
        summary.textContent = "ElegÃ­ una ubicaciÃ³n en el mapa o buscala por direcciÃ³n.";
      }
    };

    if (searchInput) {
      searchInput.disabled = !geocodingSearchUrlTemplate || noLocationMode;
    }

    if (searchButton) {
      searchButton.disabled = !geocodingSearchUrlTemplate || noLocationMode;
    }

    const initialLat = numberOrNull(latitudeInput?.value || "");
    const initialLng = numberOrNull(longitudeInput?.value || "");
    if (noLocationMode) {
      setNoLocationMode(true);
    } else if (initialLat !== null && initialLng !== null) {
      syncLocation({
        lat: initialLat,
        lng: initialLng,
        locality: localityInput?.value || "",
        address: addressInput?.value || "",
        searchValue: addressInput?.value || localityInput?.value || "",
        flyTo: false
      });
    } else if (summary) {
      summary.textContent = "ElegÃ­ una ubicaciÃ³n en el mapa o buscala por direcciÃ³n.";
    }

    marker.on("dragend", async () => {
      const position = marker.getLngLat();
      await resolveCreateLocationFromCoordinates(reverseGeocodingUrlTemplate, position.lng, position.lat, syncLocation);
    });

    instance.on("click", async event => {
      await resolveCreateLocationFromCoordinates(reverseGeocodingUrlTemplate, event.lngLat.lng, event.lngLat.lat, syncLocation);
    });

    const runSearch = async () => {
      if (noLocationMode) {
        return;
      }

      const query = String(searchInput?.value || "").trim();
      if (!query) return;

      if (!geocodingSearchUrlTemplate) {
        if (summary) summary.textContent = "La búsqueda por dirección no está configurada en este entorno.";
        return;
      }

      const results = await geocodeCreateLocation(geocodingSearchUrlTemplate, query);
      const feature = results?.[0];
      if (!feature) {
        if (summary) summary.textContent = "No encontramos esa direcciÃ³n. ProbÃ¡ con otra bÃºsqueda.";
        return;
      }

      if (!isWithinSupportedRegion(feature.lng, feature.lat)) {
        if (summary) summary.textContent = "Por ahora solo admitimos ubicaciones en Argentina, Paraguay y Uruguay.";
        return;
      }

      syncLocation({
        lat: Number(feature.lat),
        lng: Number(feature.lng),
        locality: extractLocalityFromFeature(feature),
        address: feature.address,
        searchValue: feature.address
      });
    };

    noLocationInput?.addEventListener("change", () => {
      setNoLocationMode(Boolean(noLocationInput.checked));
    });

    searchButton?.addEventListener("click", event => {
      event.preventDefault();
      runSearch();
    });

    searchInput?.addEventListener("keydown", event => {
      if (event.key === "Enter") {
        event.preventDefault();
        runSearch();
      }
    });

    mapElement.dataset.mapInitialized = "true";
  }

  function renderMapPlaceholder(mapElement, title, message) {
    mapElement.innerHTML = `
      <div class="map-placeholder">
        <h2>${escapeHtml(title)}</h2>
        <p>${escapeHtml(message)}</p>
      </div>
    `;
  }

  function enableCreateMapFallback(mapElement, noLocationInput, searchInput, searchButton, summary, message) {
    mapElement.classList.add("is-disabled");
    renderMapPlaceholder(
      mapElement,
      "Mapa no disponible",
      message || "No se pudo cargar el mapa para esta publicacion."
    );

    if (noLocationInput) {
      noLocationInput.checked = true;
    }

    if (searchInput) {
      searchInput.disabled = true;
      searchInput.value = "";
    }

    if (searchButton) {
      searchButton.disabled = true;
    }

    if (summary) {
      summary.textContent = "Mapa no disponible. La publicacion se guardara sin ubicacion.";
    }
  }

  function getCreateMapCenter(latValue, lngValue) {
    const lat = numberOrNull(latValue);
    const lng = numberOrNull(lngValue);
    if (lat !== null && lng !== null && isWithinSupportedRegion(lng, lat)) {
      return { center: [lng, lat], zoom: 15 };
    }

    return { center: supportedMapCenter, zoom: 4.8 };
  }

  async function resolveCreateLocationFromCoordinates(reverseGeocodingUrlTemplate, lng, lat, syncLocation) {
    if (!isWithinSupportedRegion(lng, lat)) {
      return;
    }

    const feature = await reverseGeocodeCreateLocation(reverseGeocodingUrlTemplate, lng, lat);
    syncLocation({
      lat,
      lng,
      locality: feature ? extractLocalityFromFeature(feature) : "",
      address: feature?.address || "",
      searchValue: feature?.address || ""
    });
  }

  async function geocodeCreateLocation(geocodingSearchUrlTemplate, query) {
    const endpoint = geocodingSearchUrlTemplate.replace("{query}", encodeURIComponent(query));
    const response = await fetch(endpoint, {
      headers: {
        Accept: "application/json"
      }
    });
    if (!response.ok) return [];
    const payload = await response.json();
    return Array.isArray(payload) ? payload.map(normalizeNominatimFeature) : [];
  }

  async function reverseGeocodeCreateLocation(reverseGeocodingUrlTemplate, lng, lat) {
    if (!reverseGeocodingUrlTemplate) return null;

    const endpoint = reverseGeocodingUrlTemplate
      .replace("{lng}", encodeURIComponent(String(lng)))
      .replace("{lat}", encodeURIComponent(String(lat)));
    const response = await fetch(endpoint, {
      headers: {
        Accept: "application/json"
      }
    });
    if (!response.ok) return null;
    const payload = await response.json();
    return normalizeNominatimFeature(payload);
  }

  function extractLocalityFromFeature(feature) {
    const address = feature?.rawAddress || feature?.addressParts || {};
    return address.city
      || address.town
      || address.village
      || address.municipality
      || address.suburb
      || address.county
      || feature?.displayName
      || feature?.address
      || "";
  }

  function normalizeNominatimFeature(feature) {
    if (!feature) return null;

    const latitude = Number.parseFloat(feature.lat);
    const longitude = Number.parseFloat(feature.lon);
    if (!Number.isFinite(latitude) || !Number.isFinite(longitude)) {
      return null;
    }

    return {
      lat: latitude,
      lng: longitude,
      address: feature.display_name || "",
      displayName: feature.display_name || "",
      rawAddress: feature.address || {},
      addressParts: feature.address || {}
    };
  }

  function buildMapStyle(styleUrl, tilesUrlTemplate, attribution) {
    if (styleUrl) {
      return styleUrl;
    }

    return {
      version: 8,
      sources: {
        "osm-raster": {
          type: "raster",
          tiles: [tilesUrlTemplate],
          tileSize: 256,
          attribution
        }
      },
      layers: [
        {
          id: "osm-raster",
          type: "raster",
          source: "osm-raster"
        }
      ]
    };
  }

  function isWithinSupportedRegion(lng, lat) {
    return lng >= supportedMapBounds[0][0]
      && lng <= supportedMapBounds[1][0]
      && lat >= supportedMapBounds[0][1]
      && lat <= supportedMapBounds[1][1];
  }

  function formatCreateLocationSummary(location) {
    const pieces = [];
    if (location.locality) {
      pieces.push(location.locality);
    }
    if (location.address && location.address !== location.locality) {
      pieces.push(location.address);
    }
    if (!pieces.length) {
      pieces.push(`Lat ${Number(location.lat).toFixed(5)} Â· Lng ${Number(location.lng).toFixed(5)}`);
    }

    return `UbicaciÃ³n seleccionada: ${pieces.join(" Â· ")}`;
  }

  function syncCreateTitle(form) {
    const category = String(form.querySelector('[name="category"]')?.selectedOptions?.[0]?.textContent || "").trim();
    const locality = String(form.querySelector('input[name="locality"]')?.value || "").trim();
    const titleInput = form.querySelector('input[name="title"]');
    if (!titleInput) return;

    if (category && locality) {
      titleInput.value = `${category} en ${locality}`;
      return;
    }

    titleInput.value = category || "Nueva publicaciÃ³n";
  }

  function wireBrowseSearchFilters(root = document) {
    root.querySelectorAll("[data-price-range-filter]").forEach(wirePriceRangeFilter);
    root.querySelectorAll("[data-group-aware-search-form='true']").forEach(wireGroupAwareSearchPlaceholder);

    const form = root.querySelector?.("[data-required-filter-form='true']");
    if (!form || form.dataset.requiredFiltersBound === "true") return;

    const groupSelect = form.querySelector('select[name="group"]');
    const endpoint = form.dataset.requiredFilterFieldsEndpoint || "";
    const panel = form.querySelector("[data-required-filter-panel]");
    const list = form.querySelector("[data-required-filter-list]");
    const fieldsScript = form.querySelector("[data-required-filter-fields-json]");
    form.dataset.requiredFiltersBound = "true";

    let fields = parseRequiredFilterFields(fieldsScript?.textContent);

    const renderFields = () => {
      if (!panel || !list) return;

      panel.hidden = fields.length === 0;
      list.innerHTML = fields.map(field => buildRequiredFilterRow(field)).join("");
    };

    groupSelect?.addEventListener("change", async () => {
      if (!endpoint) return;

      const response = await fetch(`${endpoint}?group=${encodeURIComponent(groupSelect.value)}`, {
        headers: { "X-Requested-With": "fetch" }
      });
      fields = response.ok ? parseRequiredFilterFields(await response.text()) : [];
      renderFields();
    });
  }

  function wireGroupAwareSearchPlaceholder(form) {
    if (!form || form.dataset.groupAwarePlaceholderBound === "true") return;

    const groupSelect = form.querySelector('select[name="group"]');
    const queryInput = form.querySelector("[data-group-aware-query-input='true']");
    if (!groupSelect || !queryInput) return;

    form.dataset.groupAwarePlaceholderBound = "true";

    const placeholders = {
      inmuebles: groupSelect.dataset.placeholderInmuebles || "Ej. departamento, casa con patio, lote",
      rodados: groupSelect.dataset.placeholderRodados || "Ej. Ford Fiesta, moto, camioneta",
      embarcaciones: groupSelect.dataset.placeholderEmbarcaciones || "Ej. lancha, velero, semirrígido",
      generales: groupSelect.dataset.placeholderGenerales || "Ej. iPhone, bicicleta, heladera",
      todos: groupSelect.dataset.placeholderTodos || "Ej. departamento, Ford Fiesta, iPhone"
    };

    const syncPlaceholder = () => {
      const key = String(groupSelect.value || "Todos").trim().toLowerCase();
      queryInput.placeholder = placeholders[key] || placeholders.todos;
    };

    syncPlaceholder();
    groupSelect.addEventListener("change", syncPlaceholder);
  }

  function wirePriceRangeFilter(wrapper) {
    if (!wrapper || wrapper.dataset.priceRangeBound === "true") return;

    const max = Number(wrapper.dataset.priceMax || 0) || 100000;
    const fromRange = wrapper.querySelector("[data-price-from-range]");
    const toRange = wrapper.querySelector("[data-price-to-range]");
    const fromValue = wrapper.querySelector("[data-price-from-value]");
    const toValue = wrapper.querySelector("[data-price-to-value]");
    const fromLabel = wrapper.querySelector("[data-price-from-label]");
    const toLabel = wrapper.querySelector("[data-price-to-label]");
    const fill = wrapper.querySelector("[data-price-range-fill]");
    if (!fromRange || !toRange || !fromValue || !toValue) return;

    wrapper.dataset.priceRangeBound = "true";

    const formatPrice = value => new Intl.NumberFormat("es-AR", {
      maximumFractionDigits: 0
    }).format(Math.max(0, Number(value || 0)));

    const sync = source => {
      let from = Number(fromRange.value || 0);
      let to = Number(toRange.value || max);

      if (from > to) {
        if (source === "from") {
          to = from;
          toRange.value = String(to);
        } else {
          from = to;
          fromRange.value = String(from);
        }
      }

      fromValue.value = String(from);
      toValue.value = String(to);
      if (fromLabel) fromLabel.textContent = formatPrice(from);
      if (toLabel) toLabel.textContent = formatPrice(to);

      if (fill) {
        fill.style.left = `${Math.max(0, Math.min(100, (from / max) * 100))}%`;
        fill.style.right = `${Math.max(0, Math.min(100, 100 - ((to / max) * 100)))}%`;
      }
    };

    fromRange.addEventListener("input", () => sync("from"));
    toRange.addEventListener("input", () => sync("to"));
    sync();
  }

  function buildRequiredFilterRow(field) {
    return `
      <label data-required-filter-row>
        <span>${escapeHtml(field.label || "")}</span>
        <input type="hidden" name="filterFieldId" value="${escapeAttribute(field.id)}" />
        ${buildRequiredFilterControl(field)}
      </label>
    `;
  }

  function buildRequiredFilterControl(field) {
    if (field.dataType === "lista" && Array.isArray(field.options) && field.options.length) {
      const options = field.options
        .map(option => `<option value="${escapeAttribute(option)}">${escapeHtml(option)}</option>`)
        .join("");
      return `<select name="filterValue"><option value="">Todos</option>${options}</select>`;
    }

    if (field.dataType === "booleano") {
      return `
        <select name="filterValue">
          <option value="">Todos</option>
          <option value="true">Si</option>
          <option value="false">No</option>
        </select>
      `;
    }

    const type = field.dataType === "numero" ? "number" : "text";
    const step = field.dataType === "numero" ? ` step="0.01"` : "";
    return `<input name="filterValue" type="${type}"${step} placeholder="Todos" />`;
  }

  function parseRequiredFilterFields(raw) {
    if (!raw) return [];

    try {
      const parsed = JSON.parse(raw);
      return Array.isArray(parsed)
        ? parsed.map(field => ({
          id: Number(field.id || 0),
          label: String(field.label || ""),
          dataType: String(field.dataType || "texto").toLowerCase(),
          options: Array.isArray(field.options) ? field.options.map(option => String(option || "")).filter(Boolean) : []
        })).filter(field => field.id > 0 && field.label)
        : [];
    } catch {
      return [];
    }
  }

  function buildMapGalleryPopup(marker) {
    const title = escapeHtml(marker.title || "");
    const code = escapeHtml(marker.code || "");
    const price = escapeHtml(marker.price || "");
    const detailsUrl = escapeAttribute(marker.detailsUrl || "#");
    const publicationId = escapeAttribute(marker.id || "");
    const videoUrl = escapeAttribute(marker.videoUrl || "");
    const images = Array.isArray(marker.images) && marker.images.length
      ? marker.images
      : [marker.image || "/images/logo4.png"];
    const escapedImages = images.map(image => escapeAttribute(image || "/images/logo4.png"));
    const firstImage = escapedImages[0];
    const galleryTitle = escapeHtml(String(marker.title || "").split(" - oportunidad")[0]);
    const mediaCount = escapedImages.length + (videoUrl ? 1 : 0);
    const navButtons = mediaCount > 1
      ? `
          <button type="button" class="gallery-nav gallery-nav-prev" data-direction="-1" aria-label="Foto anterior">&#8249;</button>
          <button type="button" class="gallery-nav gallery-nav-next" data-direction="1" aria-label="Foto siguiente">&#8250;</button>
        `
      : "";

    return `
      <article class="map-popup-card listing-card listing-card-compact">
        <a href="${detailsUrl}" class="card-image-wrap map-popup-image-wrap publication-preview-trigger" data-publication-id="${publicationId}" data-details-url="/api/content/details/${publicationId}" data-images="${escapedImages.join("|||")}" data-video-url="${videoUrl}" data-media-index="0">
          ${videoUrl
            ? `<video src="${videoUrl}" class="gallery-carousel-video" preload="metadata" muted playsinline></video><button type="button" class="gallery-play-toggle" data-gallery-play-toggle="true" aria-label="Reproducir video"></button><button type="button" class="gallery-audio-toggle" data-gallery-audio-toggle="true" aria-label="Activar audio">Activar audio</button>`
            : `<img src="${firstImage}" alt="${title}" class="gallery-carousel-image" />`}
          <span class="gallery-badge">${price}</span>
          ${navButtons}
          <button type="button" class="gallery-flag report-trigger" data-publication-id="${publicationId}" data-publication-code="${code}" data-publication-title="${title}" aria-label="Denunciar ${title}">Denunciar</button>
          <span class="gallery-title-overlay">${galleryTitle}</span>
        </a>
      </article>
    `;
  }

  function buildMapMarkerHoverCard(marker) {
    const title = escapeHtml(stripOpportunitySuffix(marker?.title || ""));
    const price = escapeHtml(marker?.price || "");

    return `
      <div class="map-hover-card">
        <strong>${title || "Publicacion"}</strong>
        <span>${price || "Precio sin informar"}</span>
      </div>
    `;
  }

  function buildMapMarkerTapCard(marker) {
    const title = escapeHtml(stripOpportunitySuffix(marker?.title || ""));
    const price = escapeHtml(marker?.price || "");
    const detailsUrl = escapeAttribute(`/api/content/details/${marker?.id || ""}`);

    return `
      <div class="map-tap-card">
        <strong>${title || "Publicacion"}</strong>
        <span>${price || "Precio sin informar"}</span>
        <button type="button" class="primary-pill compact" data-map-open-preview="true" data-details-url="${detailsUrl}">
          Mostrar anuncio
        </button>
      </div>
    `;
  }

  function buildMapSelectionTextCard(marker) {
    const rawTitle = stripOpportunitySuffix(marker?.title || "");
    const title = escapeHtml(rawTitle);
    const price = escapeHtml(marker?.price || "");
    const location = escapeHtml(marker?.locality || "Ubicación no informada");
    const shortDescription = escapeHtml(marker?.shortDescription || "Sin descripción breve.");
    const detailsUrl = escapeAttribute(marker?.detailsUrl || "#");
    const publicationId = escapeAttribute(marker?.id || "");
    const publicationCode = escapeAttribute(marker?.code || "");
    const isFavorite = Boolean(marker?.isFavorite);
    const suggestedListName = escapeAttribute(marker?.groupName || "Inmuebles");

    return `
      <article class="map-selection-text-card">
        <div class="map-selection-text-meta">
          <p class="map-selection-text-location">${location}</p>
          <span class="map-selection-text-price">${price || "Precio sin informar"}</span>
        </div>
        <h3 class="map-selection-text-title">${title || "Publicación"}</h3>
        <p class="map-selection-text-description">${shortDescription}</p>
        <div class="map-selection-text-actions">
          <a href="${detailsUrl}" class="primary-pill compact map-selection-text-link publication-preview-trigger" data-publication-id="${publicationId}" data-details-url="/api/content/details/${publicationId}">
            Ver anuncio
          </a>
          <button type="button" class="favorite-toggle ghost-pill compact ${isFavorite ? "is-active" : ""}" data-favorite-toggle="true" data-publication-id="${publicationId}" data-publication-title="${title}" data-suggested-list-name="${suggestedListName}" title="Añadir a mi lista de favoritos" aria-label="Añadir a mi lista de favoritos">
            ${renderFavoriteIcon(isFavorite)}
          </button>
          <button type="button" class="ghost-pill compact map-selection-text-report report-trigger" data-publication-id="${publicationId}" data-publication-code="${publicationCode}" data-publication-title="${title}">
            Denunciar
          </button>
        </div>
      </article>
    `;
  }

  function escapeHtml(value) {
    return String(value)
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll('"', "&quot;")
      .replaceAll("'", "&#39;");
  }

  function createClientId() {
    const cryptoApi = globalThis.crypto;
    if (cryptoApi?.randomUUID) {
      return cryptoApi.randomUUID();
    }

    if (cryptoApi?.getRandomValues) {
      const bytes = new Uint8Array(16);
      cryptoApi.getRandomValues(bytes);
      return Array.from(bytes, value => value.toString(16).padStart(2, "0")).join("");
    }

    return `upload-${Date.now()}-${Math.random().toString(16).slice(2, 10)}`;
  }

  function uploadImagesRequest(formData) {
    return new Promise((resolve, reject) => {
      const request = new XMLHttpRequest();
      request.open("POST", "/api/content/upload-images", true);
      request.setRequestHeader("X-Requested-With", "fetch");

      request.onload = () => {
        let payload = {};
        try {
          payload = request.responseText ? JSON.parse(request.responseText) : {};
        } catch {
          payload = {};
        }

        resolve({
          ok: request.status >= 200 && request.status < 300,
          status: request.status,
          message: payload?.message,
          urls: payload?.urls
        });
      };

      request.onerror = () => {
        reject(new Error("Fallo la conexion al subir imagenes desde este navegador."));
      };

      request.ontimeout = () => {
        reject(new Error("La subida de imagenes agoto el tiempo de espera."));
      };

      request.send(formData);
    });
  }

  function uploadVideoRequest(formData) {
    return new Promise((resolve, reject) => {
      const request = new XMLHttpRequest();
      request.open("POST", "/api/content/upload-video", true);
      request.setRequestHeader("X-Requested-With", "fetch");

      request.onload = () => {
        let payload = {};
        try {
          payload = request.responseText ? JSON.parse(request.responseText) : {};
        } catch {
          payload = {};
        }

        resolve({
          ok: request.status >= 200 && request.status < 300,
          status: request.status,
          message: payload?.message,
          url: payload?.url
        });
      };

      request.onerror = () => {
        reject(new Error("Fallo la conexion al subir el video desde este navegador."));
      };

      request.ontimeout = () => {
        reject(new Error("La subida del video agoto el tiempo de espera."));
      };

      request.send(formData);
    });
  }

  function escapeAttribute(value) {
    return escapeHtml(value);
  }

  function wirePublicationPreviewModal() {
    if (document.body.dataset.previewModalBound === "true") return;

    document.body.dataset.previewModalBound = "true";
    const modalElement = document.getElementById("publicationPreviewModal");
    const body = document.getElementById("publicationPreviewBody");
    const title = document.getElementById("publicationPreviewTitle");
    if (!modalElement || !body || !title) return;

    const closePreview = () => {
      modalElement.hidden = true;
      modalElement.classList.remove("is-open");
      document.body.classList.remove("preview-open");
      title.textContent = "Detalle de publicaciÃ³n";
      body.innerHTML = "";
    };

    modalElement.addEventListener("click", event => {
      const closeTrigger = event.target.closest("[data-preview-close='true']");
      if (!closeTrigger) return;
      event.preventDefault();
      event.stopPropagation();
      closePreview();
    });

    document.addEventListener("keydown", event => {
      if (event.key === "Escape" && modalElement.classList.contains("is-open")) {
        closePreview();
      }
    });

    openPublicationPreview = async detailsUrl => {
      if (!detailsUrl) return;

      title.textContent = "Cargando publicaciÃƒÂ³n";
      body.innerHTML = `<div class="preview-modal-loading">Cargando detalleÃ¢â‚¬Â¦</div>`;

      let response;
      try {
        response = await fetch(detailsUrl, {
          headers: { "X-Requested-With": "fetch" }
        });
      } catch {
        title.textContent = "No se pudo cargar";
        body.innerHTML = `<section class="empty-state"><h2>Error al abrir la publicaciÃƒÂ³n</h2><p>RevisÃƒÂ¡ la conexiÃƒÂ³n e intentÃƒÂ¡ nuevamente.</p></section>`;
        modalElement.hidden = false;
        modalElement.classList.add("is-open");
        document.body.classList.add("preview-open");
        return;
      }

      if (!response.ok) {
        title.textContent = "No se pudo cargar";
        body.innerHTML = `<section class="empty-state"><h2>Error al abrir la publicaciÃƒÂ³n</h2><p>IntentÃƒÂ¡ nuevamente en unos segundos.</p></section>`;
        modalElement.hidden = false;
        modalElement.classList.add("is-open");
        document.body.classList.add("preview-open");
        return;
      }

      body.innerHTML = await response.text();
      wireDetailGalleryLayout();
      const publicationTitle = body.querySelector(".detail-hero h1")?.textContent?.trim();
      title.textContent = stripOpportunitySuffix(publicationTitle) || "Detalle de publicaciÃƒÂ³n";
      modalElement.hidden = false;
      modalElement.classList.add("is-open");
      document.body.classList.add("preview-open");
      await initContentMaps();
    };

    document.addEventListener("click", async event => {
      const mapPreviewTrigger = event.target.closest("[data-map-open-preview='true']");
      if (mapPreviewTrigger) {
        event.preventDefault();
        event.stopPropagation();
        const detailsUrl = mapPreviewTrigger.getAttribute("data-details-url");
        await openPublicationPreview(detailsUrl);
        return;
      }

      const trigger = event.target.closest(".publication-preview-trigger");
      if (!trigger) return;
      if (event.target.closest(".gallery-nav") || event.target.closest(".report-trigger") || event.target.closest("[data-gallery-play-toggle='true']") || event.target.closest("[data-gallery-audio-toggle='true']") || event.target.closest("[data-favorite-toggle='true']") || event.target.closest("[data-like-toggle='true']") || event.target.closest("[data-gallery-menu-toggle='true']") || event.target.closest("[data-gallery-menu]")) {
        event.preventDefault();
        return;
      }

      event.preventDefault();

      const detailsUrl = trigger.getAttribute("data-details-url") || trigger.getAttribute("href");
      await openPublicationPreview(detailsUrl);
      return;

      title.textContent = "Cargando publicaciÃ³n";
      body.innerHTML = `<div class="preview-modal-loading">Cargando detalleâ€¦</div>`;

      let response;
      try {
        response = await fetch(detailsUrl, {
          headers: { "X-Requested-With": "fetch" }
        });
      } catch {
        title.textContent = "No se pudo cargar";
        body.innerHTML = `<section class="empty-state"><h2>Error al abrir la publicaciÃ³n</h2><p>RevisÃ¡ la conexiÃ³n e intentÃ¡ nuevamente.</p></section>`;
        modalElement.hidden = false;
        modalElement.classList.add("is-open");
        document.body.classList.add("preview-open");
        return;
      }

      if (!response.ok) {
        title.textContent = "No se pudo cargar";
        body.innerHTML = `<section class="empty-state"><h2>Error al abrir la publicaciÃ³n</h2><p>IntentÃ¡ nuevamente en unos segundos.</p></section>`;
        modalElement.hidden = false;
        modalElement.classList.add("is-open");
        document.body.classList.add("preview-open");
        return;
      }

      body.innerHTML = await response.text();
      const publicationTitle = body.querySelector(".detail-hero h1")?.textContent?.trim();
      title.textContent = stripOpportunitySuffix(publicationTitle) || "Detalle de publicaciÃ³n";
      modalElement.hidden = false;
      modalElement.classList.add("is-open");
      document.body.classList.add("preview-open");
      await initContentMaps();
    });
  }

  function wireDetailMediaOverlay() {
    if (document.body.dataset.detailMediaOverlayBound === "true") return;

    document.body.dataset.detailMediaOverlayBound = "true";
    const overlayState = {
      items: [],
      index: 0
    };

    const closeOverlay = overlay => {
      if (!overlay) return;
      const body = overlay.querySelector("[data-detail-media-body]");
      if (body) {
        body.innerHTML = "";
      }
      overlayState.items = [];
      overlayState.index = 0;
      overlay.hidden = true;
      overlay.classList.remove("is-open");
      document.body.classList.remove("preview-open");
    };

    const renderOverlayItem = (overlay, index) => {
      const body = overlay?.querySelector("[data-detail-media-body]");
      const caption = overlay?.querySelector("[data-detail-media-caption]");
      const prevButton = overlay?.querySelector("[data-detail-media-nav='prev']");
      const nextButton = overlay?.querySelector("[data-detail-media-nav='next']");
      const item = overlayState.items[index];
      if (!overlay || !body || !item) return;

      overlayState.index = index;
      body.innerHTML = item.type === "video"
        ? `<video src="${escapeAttribute(item.src)}" controls autoplay playsinline preload="metadata"></video>`
        : `<img src="${escapeAttribute(item.src)}" alt="${escapeAttribute(item.title)}" />`;
      if (caption) {
        caption.textContent = `${item.type === "video" ? "Video" : "Imagen"} ${index + 1} de ${overlayState.items.length}`;
      }
      if (prevButton) {
        prevButton.hidden = overlayState.items.length <= 1;
      }
      if (nextButton) {
        nextButton.hidden = overlayState.items.length <= 1;
      }
    };

    const syncDetailVideoPlayButton = frame => {
      const video = frame?.querySelector("video");
      const button = frame?.querySelector("[data-detail-video-play='true']");
      if (!video || !button) return;

      const isPlaying = !video.paused && !video.ended;
      button.innerHTML = isPlaying
        ? `<i class="fa-solid fa-pause" aria-hidden="true"></i>`
        : `<i class="fa-solid fa-play" aria-hidden="true"></i>`;
      button.setAttribute("aria-label", isPlaying ? "Pausar video" : "Reproducir video");
      button.setAttribute("title", isPlaying ? "Pausar video" : "Reproducir video");
    };

    document.addEventListener("click", event => {
      const playTrigger = event.target.closest("[data-detail-video-play='true']");
      if (playTrigger) {
        const frame = playTrigger.closest("[data-detail-media-item='true']");
        const video = frame?.querySelector("video");
        if (!video) return;
        event.preventDefault();
        event.stopPropagation();
        if (video.paused || video.ended) {
          video.play?.().catch(() => {});
        } else {
          video.pause?.();
        }
        syncDetailVideoPlayButton(frame);
        return;
      }

      const closeTrigger = event.target.closest("[data-detail-media-close='true']");
      if (closeTrigger) {
        const overlay = closeTrigger.closest("[data-detail-media-overlay]");
        event.preventDefault();
        event.stopPropagation();
        closeOverlay(overlay);
        return;
      }

      const navTrigger = event.target.closest("[data-detail-media-nav]");
      if (navTrigger) {
        const overlay = navTrigger.closest("[data-detail-media-overlay]");
        if (!overlayState.items.length) return;
        event.preventDefault();
        event.stopPropagation();
        const direction = navTrigger.getAttribute("data-detail-media-nav") === "next" ? 1 : -1;
        const nextIndex = (overlayState.index + direction + overlayState.items.length) % overlayState.items.length;
        renderOverlayItem(overlay, nextIndex);
        return;
      }

      const trigger = event.target.closest("[data-detail-media-item='true']");
      if (!trigger) return;
      if (!trigger.closest(".detail-gallery")) return;

      event.preventDefault();

      const detailGrid = trigger.closest(".detail-grid");
      const siblingOverlay = detailGrid?.nextElementSibling?.matches?.("[data-detail-media-overlay]")
        ? detailGrid.nextElementSibling
        : null;
      const overlay = siblingOverlay
        || trigger.closest("#publicationPreviewBody")?.querySelector("[data-detail-media-overlay]")
        || document.querySelector("[data-detail-media-overlay]");
      const mediaType = trigger.getAttribute("data-detail-media-type") || "image";
      const mediaSrc = trigger.getAttribute("data-detail-media-src") || trigger.getAttribute("src") || "";
      const mediaTitle = stripOpportunitySuffix(trigger.getAttribute("data-detail-media-title") || trigger.getAttribute("alt") || "Vista ampliada");

      if (!overlay || !mediaSrc) return;

      if (mediaType === "video") {
        const frame = trigger.closest(".detail-gallery-video-frame");
        const video = frame?.querySelector("video");
        if (video && !video.paused) {
          video.pause?.();
          syncDetailVideoPlayButton(frame);
        }
      }

      const gallery = trigger.closest(".detail-gallery");
      const items = Array.from(gallery?.querySelectorAll("[data-detail-media-item='true']") || []).map(el => ({
        type: el.getAttribute("data-detail-media-type") || "image",
        src: el.getAttribute("data-detail-media-src") || el.getAttribute("src") || "",
        title: stripOpportunitySuffix(el.getAttribute("data-detail-media-title") || el.getAttribute("alt") || mediaTitle || "Vista ampliada")
      })).filter(item => item.src);

      overlayState.items = items.length ? items : [{ type: mediaType, src: mediaSrc, title: mediaTitle }];
      overlayState.index = Math.max(0, overlayState.items.findIndex(item => item.src === mediaSrc));
      if (overlayState.index < 0) overlayState.index = 0;

      renderOverlayItem(overlay, overlayState.index);

      overlay.hidden = false;
      overlay.classList.add("is-open");
      document.body.classList.add("preview-open");
    });

    document.addEventListener("play", event => {
      const video = event.target.closest?.(".detail-gallery-video-frame video");
      if (!video) return;
      syncDetailVideoPlayButton(video.closest(".detail-gallery-video-frame"));
    }, true);

    document.addEventListener("pause", event => {
      const video = event.target.closest?.(".detail-gallery-video-frame video");
      if (!video) return;
      syncDetailVideoPlayButton(video.closest(".detail-gallery-video-frame"));
    }, true);

    document.addEventListener("ended", event => {
      const video = event.target.closest?.(".detail-gallery-video-frame video");
      if (!video) return;
      syncDetailVideoPlayButton(video.closest(".detail-gallery-video-frame"));
    }, true);

    document.addEventListener("keydown", event => {
      if (event.key === "ArrowLeft" && overlayState.items.length) {
        const overlay = document.querySelector("[data-detail-media-overlay].is-open");
        if (overlay) {
          event.preventDefault();
          renderOverlayItem(overlay, (overlayState.index - 1 + overlayState.items.length) % overlayState.items.length);
        }
        return;
      }

      if (event.key === "ArrowRight" && overlayState.items.length) {
        const overlay = document.querySelector("[data-detail-media-overlay].is-open");
        if (overlay) {
          event.preventDefault();
          renderOverlayItem(overlay, (overlayState.index + 1) % overlayState.items.length);
        }
        return;
      }

      if (event.key !== "Escape") return;
      document.querySelectorAll("[data-detail-media-overlay].is-open").forEach(overlay => {
        closeOverlay(overlay);
      });
    });
  }

  function wireCreateForm() {
    const form = document.getElementById("createPublicationForm");
    if (!form || form.dataset.bound === "true") return;

    form.dataset.bound = "true";
    syncCreateTitle(form);
    wireCreateSectionToggles(form);

    const groupInput = form.querySelector('[name="group"]');
    const categorySelect = form.querySelector("[data-category-select]");
    const localityInput = form.querySelector('input[name="locality"]');
    const addressInput = form.querySelector('input[name="address"]');
    [categorySelect, localityInput, addressInput].forEach(input => {
      input?.addEventListener("input", () => syncCreateTitle(form));
      input?.addEventListener("change", () => syncCreateTitle(form));
    });

    categorySelect?.addEventListener("change", async () => {
      categorySelect.dataset.selectedCategoryId = categorySelect.value || "";
      await reloadDynamicCategoryFields(form);
      enhanceSearchableSelects(form);
      syncCreateTitle(form);
    });

    groupInput?.addEventListener("change", async () => {
      await reloadCategoryOptions(form);
      await reloadDynamicCategoryFields(form);
      enhanceSearchableSelects(form);
      syncCreateTitle(form);
    });

    const uploader = wireCreateImageUploader(form);
    const videoUploader = wireCreateVideoUploader(form);

    form.addEventListener("submit", async event => {
      event.preventDefault();
      clearCreateFormErrors(form);

      await uploader.waitForUploads();
      await videoUploader.waitForUploads();
      const payload = serializeCreateForm(form);
      const feedback = document.getElementById("create-feedback");

      if (uploader.hasPendingFiles() || videoUploader.hasPendingFiles()) {
        if (feedback) {
          feedback.innerHTML = `<div class="status-banner warning">Espera a que terminen las subidas de imagenes y video.</div>`;
        }
        return;
      }

      const submitEndpoint = form.dataset.submitEndpoint || "/api/content/create";
      const response = await fetch(submitEndpoint, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "X-Requested-With": "fetch"
        },
        body: JSON.stringify(payload)
      });

      const result = await readJsonResponse(response);
      if (!response.ok) {
        console.log("create publication failed", {
          status: response.status,
          result,
          payload
        });
        const fieldErrors = normalizeCreateFieldErrors(result.errors);
        renderCreateFormErrors(form, fieldErrors);

        if (feedback) {
          const errorMessage = result.message || "No se pudo crear la publicacion.";
          feedback.innerHTML = `<div class="status-banner warning">${escapeHtml(errorMessage)}</div>`;
        }

        focusFirstCreateError(form, fieldErrors);
        return;
      }

      if (feedback) {
        feedback.innerHTML = "";
      }

      if (result.redirectUrl) {
        window.location.href = result.redirectUrl;
      }
    });

    reloadCategoryOptions(form, true)
      .then(() => reloadDynamicCategoryFields(form))
      .then(() => enhanceSearchableSelects(form));
  }

  function wireCreateSectionToggles(form) {
    const toggles = form.querySelectorAll("[data-section-toggle]");
    toggles.forEach(toggle => {
      const section = toggle.closest("section");
      const sectionKey = toggle.dataset.sectionToggle || "";
      const heading = section?.querySelector("[data-section-heading]");
      const body = section?.querySelector(`[data-section-body="${sectionKey}"]`);
      const technicalPanel = form.querySelector("[data-technical-panel]");
      if (!body) return;

      const sync = () => {
        if (heading) {
          heading.hidden = false;
          heading.querySelectorAll(":scope > *").forEach(node => {
            node.hidden = false;
          });
        }
        body.hidden = !toggle.checked;
      };

      toggle.addEventListener("change", sync);
      sync();
    });
  }

  function normalizeCreateFieldErrors(errors) {
    if (!errors) return [];

    if (Array.isArray(errors)) {
      return errors
        .map(item => ({
          field: typeof item?.field === "string" ? item.field.trim() : "",
          message: typeof item?.message === "string" ? item.message.trim() : ""
        }))
        .filter(item => item.field && item.message);
    }

    if (typeof errors === "object") {
      return Object.entries(errors)
        .flatMap(([field, value]) => {
          const normalizedField = normalizeCreateFieldName(field);
          const messages = Array.isArray(value) ? value : [value];

          return messages
            .map(message => ({
              field: normalizedField,
              message: typeof message === "string" ? message.trim() : String(message ?? "").trim()
            }))
            .filter(item => item.field && item.message);
        });
    }

    return [];
  }

  function normalizeCreateFieldName(field) {
    const raw = String(field || "").trim();
    if (!raw) return "";

    const bare = raw
      .replace(/^\$\./, "")
      .split(".")
      .pop()
      .trim();

    const map = {
      group: "group",
      categoryid: "category",
      category: "category",
      price: "price",
      currency: "currency",
      locality: "locationSearch",
      latitude: "locationSearch",
      longitude: "locationSearch",
      shortdescription: "shortDescription",
      longdescription: "longDescription",
      imagescsv: "imagesCsv",
      videourl: "videoUrl"
    };

    const mapped = map[bare.toLowerCase()];
    if (mapped) return mapped;
    if (bare.length === 1) return bare.toLowerCase();

    return bare[0].toLowerCase() + bare.slice(1);
  }

  function stripOpportunitySuffix(value) {
    return String(value || "").split(" - oportunidad")[0].trim();
  }

  async function readJsonResponse(response) {
    const text = await response.text();
    if (!text) {
      return {};
    }

    try {
      return JSON.parse(text);
    } catch {
      return { message: text };
    }
  }

  function clearCreateFormErrors(form) {
    form.querySelectorAll("[data-field-container]").forEach(node => {
      node.classList.remove("field-invalid");
    });

    form.querySelectorAll(".input-invalid").forEach(node => {
      node.classList.remove("input-invalid");
    });

    form.querySelectorAll("[data-field-error]").forEach(node => {
      node.textContent = "";
    });
  }

  function renderCreateFormErrors(form, errors) {
    const groupedErrors = new Map();
    errors.forEach(({ field, message }) => {
      if (!field || !message) return;
      if (!groupedErrors.has(field)) {
        groupedErrors.set(field, []);
      }

      const messages = groupedErrors.get(field);
      if (!messages.includes(message)) {
        messages.push(message);
      }
    });

    groupedErrors.forEach((messages, field) => {
      const container = form.querySelector(`[data-field-container="${field}"]`);
      const errorNode = form.querySelector(`[data-field-error="${field}"]`);
      container?.classList.add("field-invalid");
      container?.querySelectorAll("input, select, textarea").forEach(node => {
        node.classList.add("input-invalid");
      });
      container?.querySelectorAll(".searchable-select-input").forEach(node => {
        node.classList.add("input-invalid");
      });
      if (errorNode) {
        errorNode.textContent = messages.join(" ");
      }
    });
  }

  function focusFirstCreateError(form, errors) {
    const firstError = errors[0];
    if (!firstError) return;

    const container = form.querySelector(`[data-field-container="${firstError.field}"]`);
    if (!container) return;

    container.scrollIntoView({ behavior: "smooth", block: "center" });

    const target =
      container.querySelector(".searchable-select-input")
      || container.querySelector("input, select, textarea, button")
      || container;
    if (typeof target.focus === "function") {
      target.focus({ preventScroll: true });
    }
  }

  async function reloadCategoryOptions(form, preserveCurrentSelection = false) {
    const groupInput = form.querySelector('[name="group"]');
    const categorySelect = form.querySelector("[data-category-select]");
    if (!groupInput || !categorySelect) return;

    const endpoint = categorySelect.dataset.categoryEndpoint;
    if (!endpoint) return;

    const selectedCategoryId = String(categorySelect.dataset.selectedCategoryId || "").trim();
    const currentValue = preserveCurrentSelection
      ? (selectedCategoryId && selectedCategoryId !== "0" ? selectedCategoryId : "")
      : (categorySelect.value || "").trim();

    const response = await fetch(`${endpoint}?group=${encodeURIComponent(groupInput.value)}`, {
      headers: { "X-Requested-With": "fetch" }
    });

    if (!response.ok) {
      categorySelect.innerHTML = `<option value="">No se pudieron cargar las categorías</option>`;
      return;
    }

    const items = await response.json();
    const options = Array.isArray(items) ? items : [];
    categorySelect.innerHTML = `<option value="">Seleccioná una categoría</option>`;

    options.forEach(item => {
      const option = document.createElement("option");
      option.value = item.id ? String(item.id) : "";
      option.textContent = item.name || "";
      if (option.value === currentValue) {
        option.selected = true;
      }
      categorySelect.appendChild(option);
    });

    if (currentValue && !options.some(item => String(item.id) === currentValue)) {
      categorySelect.value = "";
    }

    if (!String(categorySelect.value || "").trim() && categorySelect.options.length > 0) {
      categorySelect.selectedIndex = 0;
    }

    categorySelect.dataset.selectedCategoryId = categorySelect.value || "";
    enhanceSearchableSelects(form);
  }

  async function reloadDynamicCategoryFields(form) {
    const categorySelect = form.querySelector("[data-category-select]");
    const requiredContainer = form.querySelector("[data-dynamic-required-fields-container]");
    const optionalContainer = form.querySelector("[data-dynamic-optional-fields-container]");
    const requiredEmptyNode = form.querySelector("[data-dynamic-required-fields-empty]");
    const optionalEmptyNode = form.querySelector("[data-dynamic-optional-fields-empty]");
    const technicalPanel = form.querySelector("[data-technical-panel]");
    const shouldKeepTechnicalPanelVisible = () => (categorySelect.options?.length || 0) > 1;
    if (!categorySelect || !requiredContainer || !optionalContainer) return;

    const previousValues = new Map([
      ...collectDynamicFieldValues(requiredContainer),
      ...collectDynamicFieldValues(optionalContainer)
    ]);

    const categoryId = String(categorySelect.value || "").trim();
    const template = requiredContainer.dataset.categoryFieldsEndpointTemplate || optionalContainer.dataset.categoryFieldsEndpointTemplate || "";
    if (!categoryId || !template) {
      requiredContainer.querySelectorAll("[data-dynamic-field]").forEach(node => node.remove());
      optionalContainer.querySelectorAll("[data-dynamic-field]").forEach(node => node.remove());
      if (technicalPanel) {
        technicalPanel.hidden = !shouldKeepTechnicalPanelVisible();
      }
      if (requiredEmptyNode) {
        requiredEmptyNode.hidden = false;
        requiredEmptyNode.textContent = "Seleccioná una categoría para completar los datos mínimos adicionales.";
      }
      if (optionalEmptyNode) {
        optionalEmptyNode.hidden = false;
        optionalEmptyNode.textContent = "Seleccioná una categoría para cargar la ficha técnica opcional.";
      }
      return;
    }

    const endpoint = template.replace("__CATEGORY_ID__", encodeURIComponent(categoryId));
    const response = await fetch(endpoint, {
      headers: { "X-Requested-With": "fetch" }
    });

    requiredContainer.querySelectorAll("[data-dynamic-field]").forEach(node => node.remove());
    optionalContainer.querySelectorAll("[data-dynamic-field]").forEach(node => node.remove());

    if (!response.ok) {
      if (technicalPanel) {
        technicalPanel.hidden = false;
      }
      if (requiredEmptyNode) {
        requiredEmptyNode.hidden = false;
        requiredEmptyNode.textContent = "No se pudieron cargar los campos adicionales de esta categoría.";
      }
      if (optionalEmptyNode) {
        optionalEmptyNode.hidden = false;
        optionalEmptyNode.textContent = "No se pudo cargar la ficha técnica de esta categoría.";
      }
      return;
    }

    const payload = await response.json();
    const fields = Array.isArray(payload) ? payload : [];
    if (!fields.length) {
      if (technicalPanel) {
        technicalPanel.hidden = false;
      }
      if (requiredEmptyNode) {
        requiredEmptyNode.hidden = false;
        requiredEmptyNode.textContent = "Esta categoría no tiene campos adicionales configurados.";
      }
      if (optionalEmptyNode) {
        optionalEmptyNode.hidden = false;
        optionalEmptyNode.textContent = "Esta categoría no tiene ficha técnica opcional.";
      }
      return;
    }

    const requiredFields = fields.filter(field => field.mostrarEnDatosMinimos ?? field.obligatorio);
    const optionalFields = fields.filter(field => !(field.mostrarEnDatosMinimos ?? field.obligatorio));

    if (technicalPanel) {
      technicalPanel.hidden = optionalFields.length === 0;
    }

    if (requiredEmptyNode) {
      requiredEmptyNode.hidden = requiredFields.length > 0;
        if (!requiredFields.length) {
          requiredEmptyNode.textContent = "Esta categoría no tiene campos adicionales en datos mínimos.";
        }
    }

    if (optionalEmptyNode) {
      optionalEmptyNode.hidden = optionalFields.length > 0;
      if (!optionalFields.length) {
        optionalEmptyNode.textContent = "Esta categoría no tiene ficha técnica opcional.";
      }
    }

    requiredFields.forEach(field => {
      requiredContainer.appendChild(buildDynamicFieldNode(field, previousValues.get(field.nombreInterno), "required"));
    });

    optionalFields
      .slice()
      .sort(compareOptionalDynamicFields)
      .forEach(field => {
      optionalContainer.appendChild(buildDynamicFieldNode(field, previousValues.get(field.nombreInterno), "optional"));
      });

    enhanceSearchableSelects(form);
  }

  function enhanceSearchableSelects(root = document) {
    root.querySelectorAll("[data-searchable-select-root]").forEach(wrapper => {
      const select = wrapper.querySelector("select");
      if (select) {
        wrapper.parentNode?.insertBefore(select, wrapper);
      }
      wrapper.remove();
    });
    root.querySelectorAll(".searchable-select-native").forEach(select => {
      select.classList.remove("searchable-select-native");
    });

    root.querySelectorAll("select").forEach(select => {
      if (select.multiple || select.disabled) return;

      let options = Array.from(select.options || []);
      const searchableOptions = options.filter(option => String(option.value || "").trim() !== "");
      const shouldAlwaysBeSearchable = select.matches("[data-category-select]");
      if (searchableOptions.length <= 10 && !shouldAlwaysBeSearchable) return;

      const hasEmptyOption = options.some(option => String(option.value || "").trim() === "");
      if (!hasEmptyOption && !String(select.dataset.selectedCategoryId || select.value || "").trim()) {
        const emptyOption = document.createElement("option");
        emptyOption.value = "";
        emptyOption.textContent = "Debe seleccionar";
        emptyOption.selected = true;
        select.insertBefore(emptyOption, select.firstChild);
        options = Array.from(select.options || []);
      }

      const wrapper = document.createElement("div");
      wrapper.className = "searchable-select";
      wrapper.dataset.searchableSelectRoot = "true";

      const input = document.createElement("input");
      input.type = "text";
      input.className = "searchable-select-input";
      input.placeholder = options[0]?.textContent?.trim() || "Buscar y seleccionar";
      input.autocomplete = "off";
      input.setAttribute("role", "combobox");
      input.setAttribute("aria-autocomplete", "list");
      input.setAttribute("aria-expanded", "false");

      const listId = `searchable-select-${createClientId()}`;
      const menu = document.createElement("div");
      menu.className = "searchable-select-menu";
      menu.id = listId;
      menu.hidden = true;
      menu.setAttribute("role", "listbox");
      input.setAttribute("aria-controls", listId);

      const searchableItems = searchableOptions.map(option => ({
        value: String(option.value || ""),
        label: String(option.textContent || "").trim()
      }));
      const emptyOptionLabel = select.options[0]?.textContent?.trim() || "Debe seleccionar";
      let activeIndex = -1;

      const normalizeSearchText = value => String(value || "").trim().toLocaleLowerCase("es");

      const syncInputFromSelect = () => {
        const selectedOption = select.selectedOptions?.[0];
        const selectedValue = String(selectedOption?.value || select.value || "").trim();
        input.value = selectedValue.length > 0
          ? (selectedOption?.textContent?.trim() || "")
          : "";
        input.placeholder = emptyOptionLabel;
      };

      const resetToSelectableState = () => {
        select.value = "";
        input.value = "";
        input.placeholder = emptyOptionLabel;
        select.dispatchEvent(new Event("change", { bubbles: true }));
      };

      const syncSearchableSelectErrorState = (showMessage = false) => {
        const container = select.closest("[data-field-container]");
        if (!container) return;
        const errorNode = container.querySelector(`[data-field-error="${select.dataset.internalName || container.getAttribute("data-field-container") || ""}"]`);
        const isRequired = select.getAttribute("aria-required") === "true" || select.name === "category";
        const hasValue = String(select.value || "").trim().length > 0;

        input.classList.toggle("input-invalid", isRequired && !hasValue);
        if (!isRequired || hasValue) {
          if (errorNode && errorNode.textContent === "Debe seleccionar una opción.") {
            errorNode.textContent = "";
          }
          if (!container.querySelector(".field-error:not(:empty)")) {
            container.classList.remove("field-invalid");
          }
          return;
        }

        if (!showMessage && !container.classList.contains("field-invalid")) {
          input.classList.remove("input-invalid");
          return;
        }

        container.classList.add("field-invalid");
        if (errorNode && !errorNode.textContent.trim()) {
          errorNode.textContent = "Debe seleccionar una opción.";
        }
      };

      const getFilteredItems = () => {
        const query = normalizeSearchText(input.value);
        if (!query) return searchableItems;

        return searchableItems.filter(item => normalizeSearchText(item.label).includes(query));
      };

      const closeMenu = () => {
        activeIndex = -1;
        menu.hidden = true;
        input.setAttribute("aria-expanded", "false");
        input.removeAttribute("aria-activedescendant");
      };

      const setActiveItem = index => {
        const items = Array.from(menu.querySelectorAll("[data-searchable-option]"));
        if (!items.length) {
          activeIndex = -1;
          input.removeAttribute("aria-activedescendant");
          return;
        }

        activeIndex = Math.max(0, Math.min(index, items.length - 1));
        items.forEach((item, itemIndex) => {
          item.classList.toggle("is-active", itemIndex === activeIndex);
        });

        const activeItem = items[activeIndex];
        input.setAttribute("aria-activedescendant", activeItem.id);
        activeItem.scrollIntoView({ block: "nearest" });
      };

      const selectItem = item => {
        if (!item?.value) return;

        if (select.value !== item.value) {
          select.value = item.value;
          select.dispatchEvent(new Event("change", { bubbles: true }));
        }

        syncInputFromSelect();
        syncSearchableSelectErrorState();
        closeMenu();
      };

      const renderMenu = () => {
        const filteredItems = getFilteredItems().slice(0, 80);
        menu.innerHTML = "";

        if (!filteredItems.length) {
          const emptyItem = document.createElement("div");
          emptyItem.className = "searchable-select-empty";
          emptyItem.textContent = "Sin resultados";
          menu.appendChild(emptyItem);
        } else {
          filteredItems.forEach((item, index) => {
            const option = document.createElement("button");
            option.type = "button";
            option.id = `${listId}-option-${index}`;
            option.className = "searchable-select-option";
            option.dataset.searchableOption = "true";
            option.setAttribute("role", "option");
            option.textContent = item.label;
            option.addEventListener("mousedown", event => event.preventDefault());
            option.addEventListener("click", () => selectItem(item));
            menu.appendChild(option);
          });
        }

        menu.hidden = false;
        input.setAttribute("aria-expanded", "true");
        activeIndex = -1;
      };

      const openMenu = () => {
        renderMenu();
      };

      const syncSelectFromInput = () => {
        const raw = String(input.value || "").trim();
        if (!raw) {
          select.value = "";
          select.dispatchEvent(new Event("change", { bubbles: true }));
          return;
        }

        const match = searchableItems.find(option =>
          option.label.localeCompare(raw, "es", { sensitivity: "base" }) === 0
        );

        if (!match) return;

        if (select.value !== match.value) {
          select.value = match.value;
          select.dispatchEvent(new Event("change", { bubbles: true }));
        }
      };

      input.addEventListener("change", syncSelectFromInput);
      input.addEventListener("click", openMenu);
      input.addEventListener("input", () => {
        if (!String(input.value || "").trim()) {
          select.value = "";
          select.dispatchEvent(new Event("change", { bubbles: true }));
        }

        renderMenu();
      });
      input.addEventListener("keydown", event => {
        if (event.key === "ArrowDown") {
          event.preventDefault();
          if (menu.hidden) {
            renderMenu();
          }
          setActiveItem(activeIndex + 1);
          return;
        }

        if (event.key === "ArrowUp") {
          event.preventDefault();
          if (menu.hidden) {
            renderMenu();
          }
          setActiveItem(activeIndex <= 0 ? menu.querySelectorAll("[data-searchable-option]").length - 1 : activeIndex - 1);
          return;
        }

        if (event.key === "Enter" && !menu.hidden) {
          const items = Array.from(menu.querySelectorAll("[data-searchable-option]"));
          const selectedButton = items[activeIndex];
          if (selectedButton) {
            event.preventDefault();
            selectedButton.click();
          }
          return;
        }

        if (event.key === "Escape") {
          closeMenu();
        }
      });
      input.addEventListener("blur", () => {
        window.setTimeout(() => {
          const raw = String(input.value || "").trim();
          if (!raw) {
            resetToSelectableState();
            syncSearchableSelectErrorState(true);
            closeMenu();
            return;
          }

          const match = searchableItems.find(option =>
            option.label.localeCompare(raw, "es", { sensitivity: "base" }) === 0
          );

          if (!match) {
            resetToSelectableState();
            syncSearchableSelectErrorState(true);
            closeMenu();
            return;
          }

          selectItem(match);
        }, 120);
      });
      select.addEventListener("change", () => {
        syncInputFromSelect();
        syncSearchableSelectErrorState();
      });
      syncInputFromSelect();
      syncSearchableSelectErrorState();

      select.classList.add("searchable-select-native");
      select.parentNode?.insertBefore(wrapper, select);
      wrapper.appendChild(input);
      wrapper.appendChild(menu);
      wrapper.appendChild(select);
    });
  }

  function collectDynamicFieldValues(container) {
    const values = new Map();
    container.querySelectorAll("[data-dynamic-field]").forEach(node => {
      const internalName = node.getAttribute("data-field-container");
      if (!internalName) return;

      const input = node.querySelector("[data-dynamic-input]");
      if (!input) return;

      if (input.type === "checkbox") {
        values.set(internalName, input.checked);
        return;
      }

      values.set(internalName, input.value);
    });

    return values;
  }

  function buildDynamicFieldNode(field, currentValue, mode = "optional") {
    const wrapper = document.createElement("label");
    wrapper.dataset.dynamicField = "true";
    wrapper.dataset.fieldContainer = field.nombreInterno || "";
    const isRequiredMode = mode === "required";
    const options = Array.isArray(field.opciones) ? field.opciones.filter(option => String(option || "").trim().length > 0) : [];
    const behavesAsSelect = options.length > 0;
    const effectiveFieldType = behavesAsSelect ? "lista" : String(field.tipoDato || "texto");
    const isHalfWidthOptional = !isRequiredMode && (effectiveFieldType === "numero" || effectiveFieldType === "booleano");
    const optionalWidthClass = isRequiredMode
      ? ""
      : (isHalfWidthOptional ? "dynamic-field-half" : "dynamic-field-full");
    const normalizedLabel = normalizeDynamicFieldLabel(
      field.obligatorio
        ? `${field.etiqueta} *`
        : field.etiqueta || field.nombreInterno || "Campo"
    );
    const example = String(field.ejemplo || "").trim();

    wrapper.className = isRequiredMode
      ? ""
      : (effectiveFieldType === "booleano"
        ? `dynamic-field-inline ${optionalWidthClass}`.trim()
        : `dynamic-field-inline ${optionalWidthClass}`.trim());

    const labelText = document.createElement("span");
    labelText.textContent = normalizedLabel;

    const error = document.createElement("span");
    error.className = "field-error";
    error.dataset.fieldError = field.nombreInterno || "";

    let input;
    switch (effectiveFieldType) {
      case "numero":
        input = document.createElement("input");
        input.type = "number";
        input.step = "1";
        input.value = currentValue ?? "";
        break;
      case "booleano":
        input = document.createElement("select");
        {
          const neutralOption = document.createElement("option");
          neutralOption.value = "";
          neutralOption.textContent = "Seleccionar";
          input.appendChild(neutralOption);

          const falseOption = document.createElement("option");
          falseOption.value = "false";
          falseOption.textContent = "No";
          input.appendChild(falseOption);

          const trueOption = document.createElement("option");
          trueOption.value = "true";
          trueOption.textContent = "Sí";
          input.appendChild(trueOption);

          input.value = String(currentValue) === "true"
            ? "true"
            : (String(currentValue) === "false" ? "false" : "");
        }
        break;
      case "lista":
        input = document.createElement("select");
        {
          const placeholder = document.createElement("option");
          placeholder.value = "";
          placeholder.textContent = "Seleccioná una opción";
          input.appendChild(placeholder);

          options.forEach(optionValue => {
            const option = document.createElement("option");
            option.value = String(optionValue || "");
            option.textContent = normalizeDynamicFieldLabel(String(optionValue || ""));
            if (option.value === String(currentValue ?? "")) {
              option.selected = true;
            }
            input.appendChild(option);
          });
        }
        break;
      default:
        input = document.createElement("input");
        input.type = "text";
        input.value = currentValue ?? "";
        break;
    }

    input.dataset.dynamicInput = "true";
    input.dataset.fieldId = String(field.id || "");
    input.dataset.fieldType = effectiveFieldType;
    input.dataset.internalName = String(field.nombreInterno || "");
    input.name = `dynamic_${field.nombreInterno || field.id || createClientId()}`;

    if (field.obligatorio) {
      input.setAttribute("aria-required", "true");
    }

    if ((effectiveFieldType === "texto" || effectiveFieldType === "numero") && example) {
      input.placeholder = normalizeDynamicFieldLabel(example);
    }

    const unit = normalizeDynamicFieldLabel(String(field.unidad || "").trim());
    if (!isRequiredMode && effectiveFieldType !== "booleano" && unit && !behavesAsSelect) {
      const fieldRow = document.createElement("div");
      fieldRow.className = "field-input-with-unit";
      fieldRow.appendChild(input);

      const unitBadge = document.createElement("span");
      unitBadge.className = "field-unit";
      unitBadge.textContent = `(${unit})`;
      fieldRow.appendChild(unitBadge);

      wrapper.appendChild(labelText);
      wrapper.appendChild(fieldRow);
      wrapper.appendChild(error);
      return wrapper;
    }

    wrapper.appendChild(labelText);
    wrapper.appendChild(input);
    wrapper.appendChild(error);
    return wrapper;
  }

  function compareOptionalDynamicFields(left, right) {
    const typeDiff = getOptionalDynamicFieldOrder(left) - getOptionalDynamicFieldOrder(right);
    if (typeDiff !== 0) {
      return typeDiff;
    }

    const leftOrder = Number.isFinite(Number(left?.orden)) ? Number(left.orden) : Number.MAX_SAFE_INTEGER;
    const rightOrder = Number.isFinite(Number(right?.orden)) ? Number(right.orden) : Number.MAX_SAFE_INTEGER;
    if (leftOrder !== rightOrder) {
      return leftOrder - rightOrder;
    }

    return String(left?.etiqueta || left?.nombreInterno || "")
      .localeCompare(String(right?.etiqueta || right?.nombreInterno || ""), "es", { sensitivity: "base" });
  }

  function getOptionalDynamicFieldOrder(field) {
    const options = Array.isArray(field?.opciones) ? field.opciones.filter(option => String(option || "").trim().length > 0) : [];
    if (options.length > 0) {
      return 0;
    }

    switch (String(field?.tipoDato || "").toLowerCase()) {
      case "lista":
        return 0;
      case "numero":
        return 1;
      case "booleano":
        return 2;
      case "texto":
        return 3;
      default:
        return 4;
    }
  }

  function normalizeDynamicFieldLabel(value) {
    return String(value || "")
      .replace(/\bAntiguedad\b/gi, matchCase("Antigüedad"))
      .replace(/\bBanios\b/gi, matchCase("Baños"))
      .replace(/\bAnios\b/gi, matchCase("Años"));
  }

  function matchCase(replacement) {
    return source => {
      if (source === source.toUpperCase()) {
        return replacement.toUpperCase();
      }

      if (source[0] === source[0]?.toUpperCase()) {
        return replacement[0].toUpperCase() + replacement.slice(1);
      }

      return replacement.toLowerCase();
    };
  }

  function wireCreateImageUploader(form) {
    const dropzone = form.querySelector("[data-image-dropzone]");
    const input = form.querySelector("[data-create-images-input]");
    const pickButton = form.querySelector("[data-create-images-pick]");
    const clearButton = form.querySelector("[data-create-images-clear]");
    const previews = form.querySelector("[data-image-previews]");
    const countNode = form.querySelector("[data-image-count]");
    const imagesCsvInput = form.querySelector('input[name="imagesCsv"]');
    const feedback = document.getElementById("create-feedback");

    const state = [];
    let uploadChain = Promise.resolve();

    const seedExistingImages = () => {
      const existingUrls = String(imagesCsvInput?.value || "")
        .split(",")
        .map(item => item.trim())
        .filter(Boolean);

      existingUrls.forEach(url => {
        state.push({
          id: createClientId(),
          file: { name: "Imagen actual" },
          previewUrl: url,
          uploadedUrl: url
        });
      });
    };

    const updateHiddenValue = () => {
      if (imagesCsvInput) {
        imagesCsvInput.value = state
          .map(item => item.uploadedUrl)
          .filter(Boolean)
          .join(",");
      }
    };

    const render = () => {
      if (countNode) {
        countNode.textContent = `${state.length}/11 imágenes`;
      }

      if (!previews) return;

      if (!state.length) {
        previews.innerHTML = `<div class="upload-preview upload-preview-empty"><span>Las imágenes que subas aparecerán acá.</span></div>`;
        updateHiddenValue();
        return;
      }

      previews.innerHTML = state.map((item, index) => `
        <article class="upload-preview ${index === 0 ? "main" : ""}">
          <img src="${escapeAttribute(item.previewUrl)}" alt="${escapeAttribute(item.file.name)}" />
          <span class="gallery-badge">${index === 0 ? "Principal" : `#${index + 1}`}</span>
          <span class="upload-status">${item.uploadedUrl ? "Listo" : "Subiendo..."}</span>
          <button type="button" class="gallery-nav gallery-nav-next upload-action" data-upload-action="remove" data-upload-id="${item.id}" aria-label="Quitar imagen">&times;</button>
        </article>
      `).join("");

      updateHiddenValue();
    };

    const uploadFiles = async files => {
      const validFiles = Array.from(files)
        .filter(file => file.type.startsWith("image/"))
        .slice(0, Math.max(0, 11 - state.length));

      if (!validFiles.length) {
        return;
      }

      const items = validFiles.map(file => ({
        id: createClientId(),
        file,
        previewUrl: URL.createObjectURL(file),
        uploadedUrl: null
      }));

      state.push(...items);
      render();

      const currentUpload = uploadChain.then(async () => {
        const formData = new FormData();
        items.forEach(item => formData.append("files", item.file));

        const result = await uploadImagesRequest(formData);
        if (!result.ok) {
          items.forEach(item => {
            const index = state.findIndex(x => x.id === item.id);
            if (index >= 0) {
              URL.revokeObjectURL(state[index].previewUrl);
              state.splice(index, 1);
            }
          });
          render();
          throw new Error(result.message || "No se pudieron subir las imÃ¡genes.");
        }

        const urls = Array.isArray(result.urls) ? result.urls : [];
        urls.forEach((url, index) => {
          if (items[index]) {
            items[index].uploadedUrl = url;
          }
        });
        render();
      });

      try {
        uploadChain = currentUpload.catch(() => {});
        await currentUpload;
      } catch (error) {
        if (feedback) {
          feedback.innerHTML = `<div class="status-banner warning">${escapeHtml(error.message || "No se pudieron subir las imÃ¡genes.")}</div>`;
        }
      }
    };

    const removeById = id => {
      const index = state.findIndex(item => item.id === id);
      if (index < 0) return;
      URL.revokeObjectURL(state[index].previewUrl);
      state.splice(index, 1);
      render();
    };

    pickButton?.addEventListener("click", event => {
      event.preventDefault();
      input?.click();
    });

    clearButton?.addEventListener("click", event => {
      event.preventDefault();
      while (state.length) {
        const item = state.pop();
        URL.revokeObjectURL(item.previewUrl);
      }
      if (input) {
        input.value = "";
      }
      render();
    });

    input?.addEventListener("change", () => {
      if (input.files?.length) {
        uploadFiles(input.files).finally(() => {
          input.value = "";
        });
      }
    });

    dropzone?.addEventListener("dragover", event => {
      event.preventDefault();
      dropzone.classList.add("is-dragover");
    });

    dropzone?.addEventListener("dragleave", () => {
      dropzone.classList.remove("is-dragover");
    });

    dropzone?.addEventListener("drop", event => {
      event.preventDefault();
      dropzone.classList.remove("is-dragover");
      const files = event.dataTransfer?.files;
      if (files?.length) {
        uploadFiles(files);
      }
    });

    previews?.addEventListener("click", event => {
      const button = event.target.closest("[data-upload-action]");
      if (!button) return;

      const id = button.getAttribute("data-upload-id");
      if (!id) return;

      if (button.getAttribute("data-upload-action") === "remove") {
        removeById(id);
      }
    });

    seedExistingImages();
    render();

    return {
      waitForUploads: () => uploadChain,
      hasPendingFiles: () => state.some(item => !item.uploadedUrl)
    };
  }

  function wireCreateVideoUploader(form) {
    const dropzone = form.querySelector("[data-video-dropzone]");
    const input = form.querySelector("[data-create-video-input]");
    const pickButton = form.querySelector("[data-create-video-pick]");
    const clearButton = form.querySelector("[data-create-video-clear]");
    const previews = form.querySelector("[data-video-previews]");
    const statusNode = form.querySelector("[data-video-status]");
    const videoUrlInput = form.querySelector('input[name="videoUrl"]');
    const feedback = document.getElementById("create-feedback");

    let state = null;
    let uploadChain = Promise.resolve();

    const seedExistingVideo = () => {
      const existingUrl = String(videoUrlInput?.value || "").trim();
      if (!existingUrl) return;

      state = {
        id: createClientId(),
        file: { name: "Video actual" },
        previewUrl: existingUrl,
        uploadedUrl: existingUrl
      };
    };

    const updateHiddenValue = () => {
      if (videoUrlInput) {
        videoUrlInput.value = state?.uploadedUrl || "";
      }
    };

    const render = () => {
      if (statusNode) {
        statusNode.textContent = state
          ? (state.uploadedUrl ? "Video listo" : "Subiendo video...")
          : "Sin video";
      }

      if (!previews) return;

      if (!state) {
        previews.innerHTML = `<div class="upload-preview upload-preview-empty upload-preview-video-empty"><span>Si subis un video, se mostrara primero en la publicacion.</span></div>`;
        updateHiddenValue();
        return;
      }

      previews.innerHTML = `
        <article class="upload-preview upload-preview-video main">
          <video src="${escapeAttribute(state.previewUrl)}" preload="metadata" muted playsinline controls></video>
          <span class="gallery-badge">Video principal</span>
          <span class="upload-status">${state.uploadedUrl ? "Listo" : "Subiendo..."}</span>
          <button type="button" class="gallery-nav gallery-nav-next upload-action" data-video-action="remove" aria-label="Quitar video">&times;</button>
        </article>
      `;

      updateHiddenValue();
    };

    const clearState = () => {
      if (state?.previewUrl) {
        URL.revokeObjectURL(state.previewUrl);
      }

      state = null;
      if (input) {
        input.value = "";
      }
      render();
    };

    const validateVideoDuration = file => new Promise((resolve, reject) => {
      const tempUrl = URL.createObjectURL(file);
      const probe = document.createElement("video");
      probe.preload = "metadata";
      probe.onloadedmetadata = () => {
        const duration = Number(probe.duration || 0);
        URL.revokeObjectURL(tempUrl);
        if (!Number.isFinite(duration) || duration <= 0) {
          reject(new Error("No pudimos leer la duracion del video."));
          return;
        }

        if (duration > 60) {
          reject(new Error("El video no puede durar mas de 1 minuto."));
          return;
        }

        resolve();
      };
      probe.onerror = () => {
        URL.revokeObjectURL(tempUrl);
        reject(new Error("No pudimos procesar ese archivo de video."));
      };
      probe.src = tempUrl;
    });

    const uploadFile = async file => {
      if (!file?.type?.startsWith("video/")) {
        throw new Error("Selecciona un archivo de video valido.");
      }

      await validateVideoDuration(file);
      clearState();

      state = {
        id: createClientId(),
        file,
        previewUrl: URL.createObjectURL(file),
        uploadedUrl: null
      };
      render();

      const currentUpload = uploadChain.then(async () => {
        const formData = new FormData();
        formData.append("file", file);

        const result = await uploadVideoRequest(formData);
        if (!result.ok || !result.url) {
          clearState();
          throw new Error(result.message || "No se pudo subir el video.");
        }

        if (state) {
          state.uploadedUrl = result.url;
        }
        render();
      });

      uploadChain = currentUpload.catch(() => {});
      await currentUpload;
    };

    const consumeFileList = files => {
      const file = Array.from(files || []).find(item => item.type.startsWith("video/"));
      if (!file) {
        return;
      }

      uploadFile(file).catch(error => {
        clearState();
        if (feedback) {
          feedback.innerHTML = `<div class="status-banner warning">${escapeHtml(error.message || "No se pudo subir el video.")}</div>`;
        }
      });
    };

    pickButton?.addEventListener("click", event => {
      event.preventDefault();
      input?.click();
    });

    clearButton?.addEventListener("click", event => {
      event.preventDefault();
      clearState();
    });

    input?.addEventListener("change", () => {
      if (input.files?.length) {
        consumeFileList(input.files);
      }
    });

    dropzone?.addEventListener("dragover", event => {
      event.preventDefault();
      dropzone.classList.add("is-dragover");
    });

    dropzone?.addEventListener("dragleave", () => {
      dropzone.classList.remove("is-dragover");
    });

    dropzone?.addEventListener("drop", event => {
      event.preventDefault();
      dropzone.classList.remove("is-dragover");
      const files = event.dataTransfer?.files;
      if (files?.length) {
        consumeFileList(files);
      }
    });

    previews?.addEventListener("click", event => {
      const button = event.target.closest("[data-video-action='remove']");
      if (!button) return;
      clearState();
    });

    seedExistingVideo();
    render();

    return {
      waitForUploads: () => uploadChain,
      hasPendingFiles: () => Boolean(state && !state.uploadedUrl)
    };
  }

  async function wireInfiniteGalleryFeeds(root = document) {
    const feeds = Array.from(root.querySelectorAll("[data-gallery-feed='true']"));
    for (const feed of feeds) {
      if (feed.dataset.galleryFeedBound === "true") continue;
      feed.dataset.galleryFeedBound = "true";
      await initInfiniteGalleryFeed(feed, {
        desktopVisibleRows: Number(feed.dataset.galleryDesktopVisibleRows || 3),
        desktopPreloadRows: Number(feed.dataset.galleryDesktopPreloadRows || 3),
        mobilePreloadItems: Number(feed.dataset.galleryMobilePreloadItems || 20)
      });
    }
  }

  async function initInfiniteGalleryFeed(feed, options) {
    const rail = feed.querySelector(".gallery-rail");
    const loader = feed.querySelector("[data-gallery-loader]");
    const sentinel = feed.querySelector("[data-gallery-sentinel]");
    const endpoint = feed.dataset.galleryEndpoint || "";
    if (!rail || !loader || !sentinel || !endpoint) return;

    const state = {
      endpoint,
      offset: 0,
      hasMore: true,
      loading: false,
      observer: null
    };

    const syncLoader = message => {
      loader.textContent = message;
      loader.hidden = false;
    };

    const getConfig = () => {
      const mobile = isMobileGalleryAutoplayContext();
      if (mobile) {
        const mobileBatch = Math.max(1, options.mobilePreloadItems || 20);
        return {
          initialLimit: mobileBatch,
          appendLimit: mobileBatch,
          rootMargin: "1200px 0px"
        };
      }

      const columns = getGalleryColumnCount(rail);
      const visibleRows = Math.max(1, options.desktopVisibleRows || 3);
      const preloadRows = Math.max(1, options.desktopPreloadRows || 3);
      return {
        initialLimit: columns * (visibleRows + preloadRows),
        appendLimit: columns * preloadRows,
        rootMargin: "1600px 0px"
      };
    };

    const loadMore = async () => {
      if (state.loading || !state.hasMore) return;

      state.loading = true;
      syncLoader(state.offset === 0 ? "Cargando publicaciones..." : "Cargando mas publicaciones...");
      const config = getConfig();
      const limit = state.offset === 0 ? config.initialLimit : config.appendLimit;

      try {
        const url = new URL(state.endpoint, window.location.origin);
        url.searchParams.set("offset", String(state.offset));
        url.searchParams.set("limit", String(limit));

        const response = await fetch(url.toString(), {
          headers: { "X-Requested-With": "fetch" }
        });

        if (!response.ok) {
          throw new Error(`Gallery request failed with status ${response.status}`);
        }

        const payload = await response.json();
        const items = Array.isArray(payload?.items) ? payload.items : [];
        rail.insertAdjacentHTML("beforeend", items.map((item, index) => buildGalleryCard(item, state.offset + index === 0)).join(""));
        state.offset = Number(payload?.nextOffset ?? (state.offset + items.length));
        state.hasMore = Boolean(payload?.hasMore);

        if (!items.length && state.offset === 0) {
          syncLoader("No hay publicaciones para esta busqueda.");
          sentinel.hidden = true;
        } else if (!items.length && !state.hasMore) {
          syncLoader("No hay mas publicaciones para mostrar.");
        } else if (!state.hasMore) {
          syncLoader("Llegaste al final de la galeria.");
          sentinel.hidden = true;
        } else {
          loader.hidden = true;
          sentinel.hidden = false;
        }

        wireGalleryCards();
        syncMobileGalleryVideoAutoplay(document);
      } catch (error) {
        console.error(error);
        syncLoader("No se pudieron cargar mas publicaciones.");
      } finally {
        state.loading = false;
      }
    };

    const observeMore = () => {
      state.observer?.disconnect?.();
      const config = getConfig();
      state.observer = new IntersectionObserver(entries => {
        if (entries.some(entry => entry.isIntersecting)) {
          loadMore().catch(console.error);
        }
      }, {
        root: null,
        rootMargin: config.rootMargin,
        threshold: 0
      });
      state.observer.observe(sentinel);
    };

    observeMore();
    await loadMore();
    window.addEventListener("resize", observeMore, { passive: true });
  }

  function getGalleryColumnCount(rail) {
    const styles = window.getComputedStyle(rail);
    const gap = Number.parseFloat(styles.columnGap || styles.gap || "16") || 16;
    const minCardWidth = 220;
    const availableWidth = rail.clientWidth || rail.parentElement?.clientWidth || window.innerWidth;
    return Math.max(1, Math.floor((availableWidth + gap) / (minCardWidth + gap)));
  }

  function buildGalleryCard(item, isFirstCard, options = {}) {
    const title = escapeHtml(item?.title || "");
    const galleryTitle = escapeHtml(item?.galleryTitle || item?.title || "");
    const publicationId = escapeAttribute(item?.id || "");
    const publicationCode = escapeAttribute(item?.publicationCode || "");
    const detailsUrl = escapeAttribute(item?.detailsUrl || "#");
    const price = escapeHtml(item?.price || "");
    const videoUrl = escapeAttribute(item?.videoUrl || "");
    const showReportButton = options.showReportButton !== false;
    const images = Array.isArray(item?.images) && item.images.length
      ? item.images
      : ["/images/logo4.png"];
    const escapedImages = images.map(image => escapeAttribute(image || "/images/logo4.png"));
    const firstImage = escapedImages[0];
    const mediaCount = escapedImages.length + (videoUrl ? 1 : 0);
    const isFavorite = Boolean(item?.isFavorite);
    const suggestedListName = escapeAttribute(item?.groupName || "Inmuebles");
    const navButtons = mediaCount > 1
      ? `
          <button type="button" class="gallery-nav gallery-nav-prev" data-direction="-1" aria-label="Foto anterior">&#8249;</button>
          <button type="button" class="gallery-nav gallery-nav-next" data-direction="1" aria-label="Foto siguiente">&#8250;</button>
        `
      : "";

    return `
      <article class="listing-card listing-card-compact"${isFirstCard ? ' id="gallery-first"' : ""}>
        <a href="${detailsUrl}" class="card-image-wrap publication-preview-trigger" data-publication-id="${publicationId}" data-details-url="/api/content/details/${publicationId}" data-images="${escapedImages.join("|||")}" data-video-url="${videoUrl}" data-media-index="0">
          ${videoUrl
            ? `<video src="${videoUrl}" class="gallery-carousel-video" preload="metadata" muted playsinline></video><button type="button" class="gallery-play-toggle" data-gallery-play-toggle="true" aria-label="Reproducir video"></button><button type="button" class="gallery-audio-toggle" data-gallery-audio-toggle="true" aria-label="Activar audio">Activar audio</button>`
            : `<img src="${firstImage}" alt="${title}" class="gallery-carousel-image" loading="lazy" decoding="async" />`}
          <span class="gallery-badge">${price}</span>
          ${showReportButton ? `<button type="button" class="gallery-action-button gallery-report-overlay report-trigger" data-publication-id="${publicationId}" data-publication-code="${publicationCode}" data-publication-title="${title}" aria-label="Denunciar ${title}">Denunciar</button>` : ""}
          ${navButtons}
          <span class="gallery-title-overlay">${galleryTitle}</span>
          <button type="button" class="favorite-toggle gallery-favorite-corner ${isFavorite ? "is-active" : ""}" data-favorite-toggle="true" data-publication-id="${publicationId}" data-publication-title="${title}" data-suggested-list-name="${suggestedListName}" title="Añadir a mi lista de favoritos" aria-label="Añadir a mi lista de favoritos">${renderFavoriteIcon(isFavorite)}</button>
        </a>
      </article>
    `;
  }
  function wireGalleryCards() {
    document.querySelectorAll(".gallery-nav").forEach(button => {
      if (button.dataset.bound === "true") return;

      button.dataset.bound = "true";
      button.addEventListener("click", event => {
        event.preventDefault();
        event.stopPropagation();

        const card = button.closest(".card-image-wrap");
        const direction = Number(button.dataset.direction || 1);
        advanceGalleryMedia(card, direction);
      });
    });

    document.querySelectorAll("[data-gallery-audio-toggle='true']").forEach(button => {
      if (button.dataset.bound === "true") return;

      button.dataset.bound = "true";
      button.addEventListener("click", event => {
        event.preventDefault();
        event.stopPropagation();

        const card = button.closest(".card-image-wrap");
        toggleGalleryVideoAudio(card);
      });
    });

    document.querySelectorAll("[data-gallery-play-toggle='true']").forEach(button => {
      if (button.dataset.bound === "true") return;

      button.dataset.bound = "true";
      button.addEventListener("click", event => {
        event.preventDefault();
        event.stopPropagation();

        const card = button.closest(".card-image-wrap");
        toggleGalleryVideoPlayback(card);
      });
    });

    syncMobileGalleryVideoAutoplay(document);
    document.querySelectorAll(".card-image-wrap .gallery-carousel-video").forEach(video => {
      bindGalleryVideoState(video);
      syncGalleryVideoVisualState(video.closest(".card-image-wrap"));
    });
  }

  function wireDynamicGalleryCards() {
    if (document.body.dataset.dynamicGalleryBound === "true") return;

    document.body.dataset.dynamicGalleryBound = "true";
    const autoplayVisibleVideos = () => syncMobileGalleryVideoAutoplay(document);
    let scrollResumeTimer = null;

    document.addEventListener("click", event => {
      const button = event.target.closest(".gallery-nav");
      if (!button || !button.closest(".map-popup-card")) return;

      event.preventDefault();
      event.stopPropagation();

      const card = button.closest(".card-image-wrap");
      const direction = Number(button.dataset.direction || 1);
      advanceGalleryMedia(card, direction);
    });

    document.addEventListener("click", event => {
      const button = event.target.closest("[data-gallery-audio-toggle='true']");
      if (!button || !button.closest(".map-popup-card")) return;

      event.preventDefault();
      event.stopPropagation();

      const card = button.closest(".card-image-wrap");
      toggleGalleryVideoAudio(card);
    });

    document.addEventListener("click", event => {
      const button = event.target.closest("[data-gallery-play-toggle='true']");
      if (!button || !button.closest(".map-popup-card")) return;

      event.preventDefault();
      event.stopPropagation();

      const card = button.closest(".card-image-wrap");
      toggleGalleryVideoPlayback(card);
    });

    document.addEventListener("mouseover", event => {
      const video = event.target.closest(".gallery-carousel-video");
      if (!video) return;
      if (!video.closest(".card-image-wrap, .map-popup-card")) return;
      video.play?.().catch(() => {});
    });

    document.addEventListener("mouseout", event => {
      const video = event.target.closest(".gallery-carousel-video");
      if (!video) return;
      if (!video.closest(".card-image-wrap, .map-popup-card")) return;
      video.pause?.();
      syncGalleryVideoVisualState(video.closest(".card-image-wrap"));
    });

    document.addEventListener("visibilitychange", () => {
      if (!document.hidden) {
        autoplayVisibleVideos();
      }
    });

    document.addEventListener("scroll", () => {
      pauseGalleryVideosForScroll(document);
      window.clearTimeout(scrollResumeTimer);
      scrollResumeTimer = window.setTimeout(() => {
        autoplayVisibleVideos();
      }, 160);
    }, { passive: true });
    window.addEventListener("resize", autoplayVisibleVideos);
  }

  function advanceGalleryMedia(card, direction) {
    if (!card) return;

    const images = (card.dataset.images || "")
      .split("|||")
      .map(x => x.trim())
      .filter(Boolean);
    const videoUrl = (card.dataset.videoUrl || "").trim();
    const mediaCount = images.length + (videoUrl ? 1 : 0);

    if (mediaCount <= 1) return;

    const currentIndex = Number(card.dataset.mediaIndex || 0);
    const nextIndex = (currentIndex + direction + mediaCount) % mediaCount;
    const playToggle = card.querySelector(".gallery-play-toggle");
    const currentVideo = card.querySelector(".gallery-carousel-video");
    const currentImage = card.querySelector(".gallery-carousel-image");

    if (currentVideo) {
      currentVideo.pause?.();
    }

    if (nextIndex === 0 && videoUrl) {
      if (currentImage) {
        currentImage.remove();
      }

      let video = currentVideo;
      if (!video) {
        video = document.createElement("video");
        video.className = "gallery-carousel-video";
        video.preload = "metadata";
        video.muted = true;
        video.playsInline = true;
        bindGalleryVideoState(video);
        const anchor = playToggle || card.querySelector(".gallery-badge") || card.firstChild;
        card.insertBefore(video, anchor);
      }

      video.src = videoUrl;
      tryAutoplayGalleryVideo(video);
    } else {
      if (currentVideo) {
        currentVideo.remove();
      }

        let image = currentImage;
      if (!image) {
        image = document.createElement("img");
        image.className = "gallery-carousel-image";
        image.alt = card.querySelector(".gallery-flag")?.dataset.publicationTitle || "";
        const anchor = playToggle || card.querySelector(".gallery-badge") || card.firstChild;
        card.insertBefore(image, anchor);
      }

      const imageIndex = videoUrl ? nextIndex - 1 : nextIndex;
      image.src = images[imageIndex] || images[0] || "";
    }

    card.dataset.mediaIndex = String(nextIndex);
    syncGalleryVideoAudioButton(card);
    syncGalleryVideoVisualState(card);
  }

  function isMobileGalleryAutoplayContext() {
    return window.matchMedia?.("(max-width: 768px)")?.matches
      || window.matchMedia?.("(pointer: coarse)")?.matches
      || navigator.maxTouchPoints > 0;
  }

  function isMobileMapInteractionContext() {
    return window.matchMedia?.("(max-width: 780px)")?.matches
      || window.matchMedia?.("(pointer: coarse)")?.matches
      || navigator.maxTouchPoints > 0;
  }

  function tryAutoplayGalleryVideo(video) {
    if (!video || !isMobileGalleryAutoplayContext()) return;
    video.muted = true;
    video.playsInline = true;
    video.autoplay = true;
    video.play?.().catch(() => {});
    syncGalleryVideoVisualState(video.closest(".card-image-wrap"));
  }

  function syncMobileGalleryVideoAutoplay(root = document) {
    if (!isMobileGalleryAutoplayContext()) return;

    root.querySelectorAll(".card-image-wrap .gallery-carousel-video").forEach(video => {
      const rect = video.getBoundingClientRect();
      const isVisible = rect.bottom > 0
        && rect.right > 0
        && rect.top < window.innerHeight
        && rect.left < window.innerWidth;

      if (isVisible) {
        bindGalleryVideoState(video);
        tryAutoplayGalleryVideo(video);
        syncGalleryVideoAudioButton(video.closest(".card-image-wrap"));
      } else {
        video.pause?.();
        syncGalleryVideoVisualState(video.closest(".card-image-wrap"));
      }
    });
  }

  function pauseGalleryVideosForScroll(root = document) {
    root.querySelectorAll(".card-image-wrap .gallery-carousel-video").forEach(video => {
      if (!video.paused) {
        video.pause?.();
      }
      syncGalleryVideoVisualState(video.closest(".card-image-wrap"));
    });
  }

  function toggleGalleryVideoAudio(card) {
    const video = card?.querySelector(".gallery-carousel-video");
    const button = card?.querySelector("[data-gallery-audio-toggle='true']");
    if (!video || !button) return;

    video.muted = !video.muted;
    if (!video.paused) {
      video.play?.().catch(() => {});
    } else {
      video.play?.().catch(() => {});
    }

    syncGalleryVideoAudioButton(card);
  }

  function toggleGalleryVideoPlayback(card) {
    const video = card?.querySelector(".gallery-carousel-video");
    if (!video) return;

    if (video.paused) {
      video.play?.().catch(() => {});
    } else {
      video.pause?.();
    }

    syncGalleryVideoVisualState(card);
  }

  function syncGalleryVideoAudioButton(card) {
    const button = card?.querySelector("[data-gallery-audio-toggle='true']");
    const video = card?.querySelector(".gallery-carousel-video");
    if (!button) return;

    const showingVideo = Boolean(video);
    button.hidden = !showingVideo;
    if (!showingVideo) return;

    const isMuted = video.muted;
    button.textContent = isMuted ? "Activar audio" : "Silenciar";
    button.setAttribute("aria-label", isMuted ? "Activar audio" : "Silenciar");
  }

  function bindGalleryVideoState(video) {
    if (!video || video.dataset.galleryStateBound === "true") return;

    video.dataset.galleryStateBound = "true";
    const sync = () => syncGalleryVideoVisualState(video.closest(".card-image-wrap"));
    video.addEventListener("play", sync);
    video.addEventListener("pause", sync);
    video.addEventListener("ended", sync);
  }

  function syncGalleryVideoVisualState(card) {
    const video = card?.querySelector(".gallery-carousel-video");
    const playToggle = card?.querySelector(".gallery-play-toggle");
    if (!playToggle) return;

    const showingVideo = Boolean(video);
    playToggle.hidden = !showingVideo || !video.paused;
    playToggle.setAttribute("aria-label", showingVideo && !video.paused ? "Pausar video" : "Reproducir video");
  }

  async function initRealtimeChat() {
    if (document.body?.dataset.userAuthenticated !== "true") {
      return;
    }

    await refreshChatUnreadCount();
  }

  async function refreshChatUnreadCount() {
    if (document.body?.dataset.userAuthenticated !== "true") {
      return;
    }

    const url = buildChatServiceUrl("/api/chat/unread-count");
    if (!url) {
      return;
    }

    try {
      const response = await fetch(url, {
        headers: { "X-Requested-With": "fetch" },
        credentials: "include"
      });
      if (!response.ok) return;
      const payload = await response.json();
      syncChatUnreadBadges(Number(payload?.unreadCount || 0));
    } catch (error) {
      console.warn(buildChatNetworkErrorMessage(url, error));
    }
  }

  function syncChatUnreadBadges(unreadCount) {
    const safeCount = Math.max(0, Number(unreadCount || 0));
    const unreadLabel = safeCount <= 0
      ? "Mensajes"
      : safeCount === 1
        ? "1 mensaje sin leer"
        : `${safeCount} mensajes sin leer`;

    document.querySelectorAll("[data-chat-unread-count]").forEach(node => {
      node.textContent = String(safeCount);
      node.hidden = safeCount <= 0;
    });

    document.querySelectorAll("[data-chat-mailbox-button]").forEach(button => {
      const state = safeCount <= 0
        ? "0"
        : safeCount <= 3
          ? "1"
          : safeCount <= 9
            ? "2"
            : "3";
      button.dataset.chatUnreadState = state;
      button.setAttribute("aria-label", unreadLabel);
      button.setAttribute("title", unreadLabel);
    });
  }

  function wireChatExperience(root = document) {
    wireStartChatButtons(root);
  }

  function wireStartChatButtons(root = document) {
    if (document.body.dataset.chatStartBound === "true") return;

    document.body.dataset.chatStartBound = "true";
    document.addEventListener("click", async event => {
      const button = event.target.closest("[data-start-chat='true']");
      if (!button) return;

      const publicationId = Number(button.dataset.publicationId || 0);
      if (publicationId <= 0) return;

      event.preventDefault();
      button.disabled = true;
      try {
        const url = buildChatServiceUrl("/api/chat/conversations");
        if (!url) {
          throw new Error("Configura la URL del servicio de chat.");
        }

        const response = await fetch(url, {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
            "X-Requested-With": "fetch"
          },
          body: JSON.stringify({ publicationId }),
          credentials: "include"
        });

        const payload = await response.json().catch(() => ({}));
        if (!response.ok) {
          throw new Error(payload?.message || "No se pudo abrir el chat.");
        }

        window.location.href = payload.redirectUrl || "/Mensajes";
      } catch (error) {
        const fallbackMessage = buildChatNetworkErrorMessage(url, error);
        window.alert(error?.message && error.message !== "Failed to fetch" ? error.message : fallbackMessage);
      } finally {
        button.disabled = false;
      }
    });
  }

  function buildChatServiceUrl(path) {
    const baseUrl = String(chatConfig.baseUrl || "").trim().replace(/\/+$/, "");
    if (!baseUrl) {
      return "";
    }

    if (!path) {
      return baseUrl;
    }

    return `${baseUrl}${path.startsWith("/") ? path : `/${path}`}`;
  }

  function buildChatNetworkErrorMessage(url, error) {
    const rawMessage = String(error?.message || "").trim();
    if (rawMessage && rawMessage !== "Failed to fetch") {
      return rawMessage;
    }

    const origin = getOriginLabel(url);
    if (origin && isLocalhostOrigin(origin)) {
      return `No se pudo conectar al servicio de chat en ${origin}. En debug inicia Ventagram.ChatService junto con Ventagram.Web.`;
    }

    return "No se pudo conectar con el servicio de chat.";
  }

  function getOriginLabel(url) {
    try {
      return new URL(url).origin;
    } catch {
      return "";
    }
  }

  function isLocalhostOrigin(origin) {
    try {
      const parsed = new URL(origin);
      return parsed.hostname === "localhost" || parsed.hostname === "127.0.0.1";
    } catch {
      return false;
    }
  }

  function serializeCreateForm(form) {
    const value = name => form.querySelector(`[name="${name}"]`)?.value ?? "";
    const checked = name => Boolean(form.querySelector(`[name="${name}"]`)?.checked);
    const noLocation = checked("noLocation");
    const dynamicFields = Array.from(form.querySelectorAll("[data-dynamic-input]"))
      .map(input => {
        const fieldId = Number(input.dataset.fieldId || 0);
        const fieldType = input.dataset.fieldType || "texto";
        const payload = { fieldId, valueText: null, valueNumber: null, valueBoolean: null };

        if (fieldType === "booleano") {
          payload.valueBoolean = input.value === ""
            ? null
            : String(input.value).toLowerCase() === "true";
        } else if (fieldType === "numero") {
          payload.valueNumber = input.value === "" ? null : Number(input.value);
        } else {
          payload.valueText = input.value === "" ? null : input.value;
        }

        return payload;
      })
      .filter(item => item.fieldId > 0);

    return {
      group: Number(value("group") || 0),
      categoryId: Number(value("category") || 0),
      title: value("title"),
      price: Number(value("price") || 0),
      currency: value("currency") || "ARS",
      locality: noLocation ? "" : value("locality"),
      shortDescription: value("shortDescription"),
      longDescription: value("longDescription") || null,
      imagesCsv: value("imagesCsv"),
      videoUrl: value("videoUrl") || null,
      contactEmail: null,
      contactName: null,
      contactPhone: null,
      featured: checked("featured"),
      latitude: noLocation ? null : numberOrNull(value("latitude")),
      longitude: noLocation ? null : numberOrNull(value("longitude")),
      address: noLocation ? null : value("address") || null,
      noLocation,
      propertyType: null,
      operation: null,
      zone: null,
      totalAreaM2: null,
      coveredAreaM2: null,
      roomsOrBedrooms: null,
      bathrooms: null,
      garageSpaces: null,
      ageYears: null,
      expenses: null,
      condition: null,
      mortgageEligible: false,
      professionalUseAllowed: false,
      services: null,
      amenities: null,
      vehicleType: null,
      brand: null,
      model: null,
      year: null,
      kilometers: null,
      fuel: null,
      transmission: null,
      version: null,
      color: null,
      licensePlate: null,
      engine: null,
      traction: null,
      doors: null,
      ownersCount: null,
      acceptsTrade: false,
      financingAvailable: false,
      equipment: null,
      generalCondition: null,
      subcategory: null,
      itemCondition: null,
      sku: null,
      stock: null,
      measure: null,
      weight: null,
      dimensions: null,
      warranty: null,
      shipping: null,
      dynamicFields,
      publisherMode: "Account"
    };
  }

  function numberOrNull(value) {
    return value === "" ? null : Number(value);
  }
})();


