(() => {
  let ventagramFlashMessage = "";
  let maptilerSdkPromise = null;
  const maptilerKeyHealth = new Map();

  document.addEventListener("DOMContentLoaded", async () => {
    wirePhoneMasks(document);
    wireReportModal();
    wirePublicationPreviewModal();
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
    wireGalleryCards();
    wireDynamicGalleryCards();
    wireReportForm();
    wireCreateForm();
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

      const result = await response.json();
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

  async function initContentMaps() {
    const homeMap = document.getElementById("map");
    const publicationMap = document.querySelector("[data-publication-map]");
    const createMap = document.querySelector("[data-create-map]");
    if (!homeMap && !publicationMap && !createMap) return;

    const sdk = await loadMaptilerSdk();
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

  function loadMaptilerSdk() {
    if (maptilerSdkPromise) {
      return maptilerSdkPromise;
    }

    maptilerSdkPromise = new Promise((resolve, reject) => {
      if (!document.querySelector('link[data-maptiler-css="true"]')) {
        const link = document.createElement("link");
        link.dataset.maptilerCss = "true";
        link.rel = "stylesheet";
        link.href = "https://cdn.maptiler.com/maptiler-sdk-js/latest/maptiler-sdk.css";
        document.head.appendChild(link);
      }

      if (window.maptilersdk) {
        resolve(window.maptilersdk);
        return;
      }

      const existingScript = document.querySelector('script[data-maptiler-sdk="true"]');
      if (existingScript) {
        existingScript.addEventListener("load", () => resolve(window.maptilersdk), { once: true });
        existingScript.addEventListener("error", () => reject(new Error("No se pudo cargar MapTiler.")), { once: true });
        return;
      }

      const script = document.createElement("script");
      script.dataset.maptilerSdk = "true";
      script.src = "https://cdn.maptiler.com/maptiler-sdk-js/latest/maptiler-sdk.umd.min.js";
      script.onload = () => resolve(window.maptilersdk);
      script.onerror = () => reject(new Error("No se pudo cargar MapTiler."));
      document.head.appendChild(script);
    });

    return maptilerSdkPromise;
  }

  async function initHomeMap(mapElement, sdk) {
    if (mapElement.dataset.mapInitialized === "true") return;
    const apiKey = mapElement.dataset.maptilerKey;
    if (!apiKey) return;
    const mapMode = mapElement.dataset.mapMode || "home";

    const markers = JSON.parse(mapElement.dataset.markers || "[]");
    if (!markers.length) return;

    const keyState = await ensureMapTilerKeyState(apiKey);
    if (!keyState.ok) {
      renderMapPlaceholder(
        mapElement,
        "Mapa no disponible",
        keyState.message || "No se pudo cargar MapTiler para esta vista."
      );
      mapElement.dataset.mapInitialized = "true";
      return;
    }

    sdk.config.apiKey = apiKey;
    mapElement.innerHTML = "";

    const instance = new sdk.Map({
      container: mapElement,
      style: sdk.MapStyle.STREETS,
      language: "es",
      center: [markers[0].lng, markers[0].lat],
      zoom: mapMode === "detail" ? 17.5 : 5
    });

    const bounds = new sdk.LngLatBounds();
    markers.forEach(marker => {
      const popup = new sdk.Popup({ offset: 24 }).setHTML(buildMapGalleryPopup(marker));
      new sdk.Marker({ color: "#ff5a5f" })
        .setLngLat([marker.lng, marker.lat])
        .setPopup(popup)
        .addTo(instance);

      bounds.extend([marker.lng, marker.lat]);
    });

    if (markers.length > 1) {
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
    const apiKey = mapElement.dataset.maptilerKey;
    if (!apiKey) return;

    const form = mapElement.closest("form");
    if (!form) return;

    const latitudeInput = form.querySelector('input[name="latitude"]');
    const longitudeInput = form.querySelector('input[name="longitude"]');
    const localityInput = form.querySelector('input[name="locality"]');
    const addressInput = form.querySelector('input[name="address"]');
    const titleInput = form.querySelector('input[name="title"]');
    const searchInput = form.querySelector('input[name="locationSearch"]');
    const noLocationInput = form.querySelector("[data-create-no-location]");
    const searchButton = form.querySelector("[data-create-address-search]");
    const summary = form.querySelector("[data-create-location-summary]");
    let noLocationMode = Boolean(noLocationInput?.checked);

    const keyState = await ensureMapTilerKeyState(apiKey);
    if (!keyState.ok) {
      enableCreateMapFallback(mapElement, noLocationInput, searchInput, searchButton, summary, keyState.message);
      mapElement.dataset.mapInitialized = "true";
      return;
    }

    sdk.config.apiKey = apiKey;
    mapElement.innerHTML = "";

    const defaultCenter = getCreateMapCenter(latitudeInput?.value, longitudeInput?.value);
    const instance = new sdk.Map({
      container: mapElement,
      style: sdk.MapStyle.STREETS,
      language: "es",
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
        searchInput.disabled = enabled;
      }

      if (searchButton) {
        searchButton.disabled = enabled;
      }

      if (enabled) {
        if (latitudeInput) latitudeInput.value = "";
        if (longitudeInput) longitudeInput.value = "";
        if (localityInput) localityInput.value = "";
        if (addressInput) addressInput.value = "";
        if (searchInput) searchInput.value = "";
        if (summary) {
          summary.textContent = "Sin ubicación disponible";
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
        summary.textContent = "Elegí una ubicación en el mapa o buscala por dirección.";
      }
    };

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
      summary.textContent = "Elegí una ubicación en el mapa o buscala por dirección.";
    }

    marker.on("dragend", async () => {
      const position = marker.getLngLat();
      await resolveCreateLocationFromCoordinates(form, sdk, position.lng, position.lat, syncLocation);
    });

    instance.on("click", async event => {
      await resolveCreateLocationFromCoordinates(form, sdk, event.lngLat.lng, event.lngLat.lat, syncLocation);
    });

    const runSearch = async () => {
      if (noLocationMode) {
        return;
      }

      const query = String(searchInput?.value || "").trim();
      if (!query) return;

      const results = await geocodeCreateLocation(apiKey, query);
      const feature = results?.[0];
      if (!feature) {
        if (summary) summary.textContent = "No encontramos esa dirección. Probá con otra búsqueda.";
        return;
      }

      syncLocation({
        lat: feature.center[1],
        lng: feature.center[0],
        locality: extractLocalityFromFeature(feature),
        address: feature.place_name,
        searchValue: feature.place_name
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

  async function ensureMapTilerKeyState(apiKey) {
    if (maptilerKeyHealth.has(apiKey)) {
      return maptilerKeyHealth.get(apiKey);
    }

    const validationPromise = fetch(`https://api.maptiler.com/maps/streets-v2/style.json?key=${encodeURIComponent(apiKey)}`)
      .then(response => {
        if (response.ok) {
          return { ok: true, message: "" };
        }

        if (response.status === 401 || response.status === 403) {
          return {
            ok: false,
            message: "La clave de MapTiler no autoriza este origen. Si entras por la IP local, crea una clave que permita 192.168.100.88 o quita la restriccion a localhost."
          };
        }

        return {
          ok: false,
          message: `MapTiler respondio ${response.status}.`
        };
      })
      .catch(() => ({
        ok: false,
        message: "No se pudo conectar con MapTiler."
      }));

    maptilerKeyHealth.set(apiKey, validationPromise);
    return validationPromise;
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
    if (lat !== null && lng !== null) {
      return { center: [lng, lat], zoom: 15 };
    }

    return { center: [-64.1888, -31.4201], zoom: 4.5 };
  }

  async function resolveCreateLocationFromCoordinates(form, sdk, lng, lat, syncLocation) {
    const apiKey = form.querySelector("[data-create-map]")?.dataset.maptilerKey;
    if (!apiKey) return;

    const result = await reverseGeocodeCreateLocation(apiKey, lng, lat);
    const feature = result?.[0];
    syncLocation({
      lat,
      lng,
      locality: feature ? extractLocalityFromFeature(feature) : "",
      address: feature?.place_name || "",
      searchValue: feature?.place_name || ""
    });
  }

  async function geocodeCreateLocation(apiKey, query) {
    const response = await fetch(`https://api.maptiler.com/geocoding/${encodeURIComponent(query)}.json?key=${encodeURIComponent(apiKey)}&language=es&limit=1`);
    if (!response.ok) return [];
    const payload = await response.json();
    return payload?.features || [];
  }

  async function reverseGeocodeCreateLocation(apiKey, lng, lat) {
    const response = await fetch(`https://api.maptiler.com/geocoding/${lng},${lat}.json?key=${encodeURIComponent(apiKey)}&language=es&limit=1`);
    if (!response.ok) return [];
    const payload = await response.json();
    return payload?.features || [];
  }

  function extractLocalityFromFeature(feature) {
    const context = Array.isArray(feature?.context) ? feature.context : [];
    const localityNode = context.find(item => {
      const id = String(item?.id || "");
      return id.startsWith("place.") || id.startsWith("locality.") || id.startsWith("municipality.") || id.startsWith("region.");
    });

    return localityNode?.text || feature?.text || feature?.place_name || "";
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
      pieces.push(`Lat ${Number(location.lat).toFixed(5)} · Lng ${Number(location.lng).toFixed(5)}`);
    }

    return `Ubicación seleccionada: ${pieces.join(" · ")}`;
  }

  function syncCreateTitle(form) {
    const category = String(form.querySelector('[name="category"]')?.value || "").trim();
    const locality = String(form.querySelector('input[name="locality"]')?.value || "").trim();
    const titleInput = form.querySelector('input[name="title"]');
    if (!titleInput) return;

    if (category && locality) {
      titleInput.value = `${category} en ${locality}`;
      return;
    }

    titleInput.value = category || "Nueva publicación";
  }

  function buildMapGalleryPopup(marker) {
    const title = escapeHtml(marker.title || "");
    const code = escapeHtml(marker.code || "");
    const price = escapeHtml(marker.price || "");
    const detailsUrl = escapeAttribute(marker.detailsUrl || "#");
    const publicationId = escapeAttribute(marker.id || "");
    const images = Array.isArray(marker.images) && marker.images.length
      ? marker.images
      : [marker.image || "/images/logo4.png"];
    const escapedImages = images.map(image => escapeAttribute(image || "/images/logo4.png"));
    const firstImage = escapedImages[0];
    const galleryTitle = escapeHtml(String(marker.title || "").split(" - oportunidad")[0]);
    const navButtons = escapedImages.length > 1
      ? `
          <button type="button" class="gallery-nav gallery-nav-prev" data-direction="-1" aria-label="Foto anterior">&#8249;</button>
          <button type="button" class="gallery-nav gallery-nav-next" data-direction="1" aria-label="Foto siguiente">&#8250;</button>
        `
      : "";

    return `
      <article class="map-popup-card listing-card listing-card-compact">
        <a href="${detailsUrl}" class="card-image-wrap map-popup-image-wrap publication-preview-trigger" data-publication-id="${publicationId}" data-details-url="/api/content/details/${publicationId}">
          <img src="${firstImage}" alt="${title}" class="gallery-carousel-image" data-images="${escapedImages.join("|||")}" data-index="0" />
          <span class="gallery-badge">${price}</span>
          ${navButtons}
          <button type="button" class="gallery-flag report-trigger" data-publication-id="${publicationId}" data-publication-code="${code}" data-publication-title="${title}" aria-label="Denunciar ${title}">Denunciar</button>
          <span class="gallery-title-overlay">${galleryTitle}</span>
        </a>
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
      title.textContent = "Detalle de publicación";
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

    document.addEventListener("click", async event => {
      const trigger = event.target.closest(".publication-preview-trigger");
      if (!trigger) return;
      if (event.target.closest(".gallery-nav") || event.target.closest(".report-trigger")) {
        event.preventDefault();
        return;
      }

      event.preventDefault();

      const detailsUrl = trigger.getAttribute("data-details-url") || trigger.getAttribute("href");
      if (!detailsUrl) return;

      title.textContent = "Cargando publicación";
      body.innerHTML = `<div class="preview-modal-loading">Cargando detalle…</div>`;

      let response;
      try {
        response = await fetch(detailsUrl, {
          headers: { "X-Requested-With": "fetch" }
        });
      } catch {
        title.textContent = "No se pudo cargar";
        body.innerHTML = `<section class="empty-state"><h2>Error al abrir la publicación</h2><p>Revisá la conexión e intentá nuevamente.</p></section>`;
        modalElement.hidden = false;
        modalElement.classList.add("is-open");
        document.body.classList.add("preview-open");
        return;
      }

      if (!response.ok) {
        title.textContent = "No se pudo cargar";
        body.innerHTML = `<section class="empty-state"><h2>Error al abrir la publicación</h2><p>Intentá nuevamente en unos segundos.</p></section>`;
        modalElement.hidden = false;
        modalElement.classList.add("is-open");
        document.body.classList.add("preview-open");
        return;
      }

      body.innerHTML = await response.text();
      const publicationTitle = body.querySelector(".detail-hero h1")?.textContent?.trim();
      title.textContent = stripOpportunitySuffix(publicationTitle) || "Detalle de publicación";
      modalElement.hidden = false;
      modalElement.classList.add("is-open");
      document.body.classList.add("preview-open");
      await initContentMaps();
    });
  }

  function wireCreateForm() {
    const form = document.getElementById("createPublicationForm");
    if (!form || form.dataset.bound === "true") return;

    form.dataset.bound = "true";
    syncCreateTitle(form);

    const groupInput = form.querySelector('[name="group"]');
    const categorySelect = form.querySelector("[data-category-select]");
    const localityInput = form.querySelector('input[name="locality"]');
    const addressInput = form.querySelector('input[name="address"]');
    [categorySelect, localityInput, addressInput].forEach(input => {
      input?.addEventListener("input", () => syncCreateTitle(form));
      input?.addEventListener("change", () => syncCreateTitle(form));
    });

    groupInput?.addEventListener("change", async () => {
      await reloadCategoryOptions(form);
      syncCreateTitle(form);
    });

    const uploader = wireCreateImageUploader(form);

    form.addEventListener("submit", async event => {
      event.preventDefault();
      clearCreateFormErrors(form);

      await uploader.waitForUploads();
      const payload = serializeCreateForm(form);
      const feedback = document.getElementById("create-feedback");

      if (uploader.hasPendingFiles()) {
        if (feedback) {
          feedback.innerHTML = `<div class="status-banner warning">Espera a que termine la subida de imagenes.</div>`;
        }
        return;
      }

      const response = await fetch("/api/content/create", {
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

    reloadCategoryOptions(form, true);
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
      Group: "group",
      Category: "category",
      Price: "price",
      Currency: "currency",
      Locality: "locationSearch",
      Latitude: "locationSearch",
      Longitude: "locationSearch",
      ShortDescription: "shortDescription",
      LongDescription: "longDescription",
      ImagesCsv: "imagesCsv"
    };

    if (map[bare]) return map[bare];
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
    errors.forEach(({ field, message }) => {
      const container = form.querySelector(`[data-field-container="${field}"]`);
      const errorNode = form.querySelector(`[data-field-error="${field}"]`);
      container?.classList.add("field-invalid");
      container?.querySelectorAll("input, select, textarea").forEach(node => {
        node.classList.add("input-invalid");
      });
      if (errorNode) {
        errorNode.textContent = message;
      }
    });
  }

  function focusFirstCreateError(form, errors) {
    const firstError = errors[0];
    if (!firstError) return;

    const container = form.querySelector(`[data-field-container="${firstError.field}"]`);
    if (!container) return;

    container.scrollIntoView({ behavior: "smooth", block: "center" });

    const target = container.querySelector("input, select, textarea, button") || container;
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

    const currentValue = preserveCurrentSelection
      ? (categorySelect.dataset.selectedCategory || categorySelect.value || "").trim()
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
      option.value = item.name || "";
      option.textContent = item.name || "";
      if (option.value === currentValue) {
        option.selected = true;
      }
      categorySelect.appendChild(option);
    });

    if (currentValue && !options.some(item => item.name === currentValue)) {
      categorySelect.value = "";
    }

    categorySelect.dataset.selectedCategory = categorySelect.value || "";
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
          <span class="upload-status">${item.uploadedUrl ? "Listo" : "Subiendo…"}</span>
          <button type="button" class="gallery-nav gallery-nav-prev upload-action" data-upload-action="primary" data-upload-id="${item.id}" aria-label="Marcar como principal">★</button>
          <button type="button" class="gallery-nav gallery-nav-next upload-action" data-upload-action="remove" data-upload-id="${item.id}" aria-label="Quitar imagen">×</button>
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
          throw new Error(result.message || "No se pudieron subir las imágenes.");
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
          feedback.innerHTML = `<div class="status-banner warning">${escapeHtml(error.message || "No se pudieron subir las imágenes.")}</div>`;
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

    const makePrimary = id => {
      const index = state.findIndex(item => item.id === id);
      if (index <= 0) return;
      const [item] = state.splice(index, 1);
      state.unshift(item);
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
      } else if (button.getAttribute("data-upload-action") === "primary") {
        makePrimary(id);
      }
    });

    render();

    return {
      waitForUploads: () => uploadChain,
      hasPendingFiles: () => state.some(item => !item.uploadedUrl)
    };
  }

  function wireGalleryCards() {
    document.querySelectorAll(".gallery-nav").forEach(button => {
      if (button.dataset.bound === "true") return;

      button.dataset.bound = "true";
      button.addEventListener("click", event => {
        event.preventDefault();
        event.stopPropagation();

        const card = button.closest(".card-image-wrap");
        const image = card?.querySelector(".gallery-carousel-image");
        if (!image) return;

        const images = (image.dataset.images || "")
          .split("|||")
          .map(x => x.trim())
          .filter(Boolean);

        if (images.length <= 1) return;

        const direction = Number(button.dataset.direction || 1);
        const currentIndex = Number(image.dataset.index || 0);
        const nextIndex = (currentIndex + direction + images.length) % images.length;

        image.src = images[nextIndex];
        image.dataset.index = String(nextIndex);
      });
    });
  }

  function wireDynamicGalleryCards() {
    if (document.body.dataset.dynamicGalleryBound === "true") return;

    document.body.dataset.dynamicGalleryBound = "true";
    document.addEventListener("click", event => {
      const button = event.target.closest(".gallery-nav");
      if (!button || !button.closest(".map-popup-card")) return;

      event.preventDefault();
      event.stopPropagation();

      const card = button.closest(".card-image-wrap");
      const image = card?.querySelector(".gallery-carousel-image");
      if (!image) return;

      const images = (image.dataset.images || "")
        .split("|||")
        .map(x => x.trim())
        .filter(Boolean);

      if (images.length <= 1) return;

      const direction = Number(button.dataset.direction || 1);
      const currentIndex = Number(image.dataset.index || 0);
      const nextIndex = (currentIndex + direction + images.length) % images.length;

      image.src = images[nextIndex];
      image.dataset.index = String(nextIndex);
    });
  }

  function serializeCreateForm(form) {
    const value = name => form.querySelector(`[name="${name}"]`)?.value ?? "";
    const checked = name => Boolean(form.querySelector(`[name="${name}"]`)?.checked);
    const noLocation = checked("noLocation");

    return {
      group: Number(value("group") || 0),
      category: value("category"),
      title: value("title"),
      price: Number(value("price") || 0),
      currency: value("currency") || "ARS",
      locality: noLocation ? "" : value("locality"),
      shortDescription: value("shortDescription"),
      longDescription: value("longDescription") || null,
      imagesCsv: value("imagesCsv"),
      contactEmail: null,
      contactName: null,
      contactPhone: null,
      featured: checked("featured"),
      videoUrl: null,
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
      extraAttributesRaw: null,
      publisherMode: "Account"
    };
  }

  function numberOrNull(value) {
    return value === "" ? null : Number(value);
  }
})();


